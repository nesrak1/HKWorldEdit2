using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Assets.Bundler
{
    //crawl dependencies for game files -> editor files
    public class ReferenceCrawlerEditor
    {
        //we use two files to isolate textures, audioclips, shaders, etc. because unity will corrupt them
        const string SCENE_LEVEL_NAME = "editorscene"; //give a name to the memory bundle which has no file name
        const string ASSET_LEVEL_NAME = "editorasset";
        public Dictionary<AssetID, AssetID> references; //build space to editor space
        public List<AssetsReplacer> sceneReplacers;
        public List<AssetsReplacer> assetReplacers;
        public List<AssetsReplacer> sceneMonoReplacers;

        private Dictionary<AssetID, Tk2dInfo> tk2dFromGoLookup; //gameobject to rect
        private ushort tk2dSpriteScriptIndex;

        private AssetsManager am;
        private int sceneId;
        private int assetId;
        public ReferenceCrawlerEditor(AssetsManager am)
        {
            this.am = am;
            sceneId = 1;
            assetId = 1;
            references = new Dictionary<AssetID, AssetID>();
            sceneReplacers = new List<AssetsReplacer>();
            assetReplacers = new List<AssetsReplacer>();
            sceneMonoReplacers = new List<AssetsReplacer>();
            tk2dFromGoLookup = new Dictionary<AssetID, Tk2dInfo>();
            tk2dSpriteScriptIndex = 0xffff;
        }
        public void SetReferences(AssetsFileInstance inst, AssetFileInfoEx inf)
        {
            AssetTypeValueField baseField = am.GetATI(inst.file, inf).GetBaseField();
            SetReferencesRecurse(inst, baseField);
        }
        public void ReplaceReferences(AssetsFileInstance inst, AssetFileInfoEx inf, long pathId)
        {
            AssetTypeValueField baseField = am.GetATI(inst.file, inf).GetBaseField();
            ReplaceReferencesRecurse(inst, baseField, inf);
            FixAsset(inst, baseField, inf);
            byte[] baseFieldData;
            using (MemoryStream ms = new MemoryStream())
            using (AssetsFileWriter w = new AssetsFileWriter(ms))
            {
                w.bigEndian = false;
                baseField.Write(w);
                baseFieldData = ms.ToArray();
            }
            AssetsReplacer replacer = new AssetsReplacerFromMemory(0, pathId, (int)inf.curFileType, 0xFFFF, baseFieldData);
            if (IsAsset(inf))
                assetReplacers.Add(replacer);
            else if (inf.curFileType == 0x72)
                sceneMonoReplacers.Add(replacer);
            else
                sceneReplacers.Add(replacer);
        }
        public void AddReplacer(byte[] data, int type, ushort monoType, bool isAsset)
        {
            int id = isAsset ? assetId : sceneId;
            AssetsReplacer replacer = new AssetsReplacerFromMemory(0, id, type, monoType, data);
            if (IsAsset(type))
                assetReplacers.Add(replacer);
            else if (type == 0x72)
                sceneMonoReplacers.Add(replacer);
            else
                sceneReplacers.Add(replacer);
            if (isAsset) assetId++; else sceneId++;
        }
        public void AddReference(AssetID aid, bool isAsset)
        {
            string name = isAsset ? ASSET_LEVEL_NAME : SCENE_LEVEL_NAME;
            int id = isAsset ? assetId : sceneId;
            AssetID newAid = new AssetID(name, id);
            references.Add(aid, newAid);
            if (isAsset) assetId++; else sceneId++;
        }
        private void SetReferencesRecurse(AssetsFileInstance inst, AssetTypeValueField field)
        {
            foreach (AssetTypeValueField child in field.children)
            {
                //not a value (ie not an int)
                if (!child.templateField.hasValue)
                {
                    //not null
                    if (child == null)
                        return;
                    //not array of values either
                    if (child.templateField.isArray && child.templateField.children[1].valueType != EnumValueTypes.ValueType_None)
                        break;
                    string typeName = child.templateField.type;
                    //is a pptr
                    if (typeName.StartsWith("PPtr<") && typeName.EndsWith(">") && child.childrenCount == 2)
                    {
                        int fileId = child.Get("m_FileID").GetValue().AsInt();
                        long pathId = child.Get("m_PathID").GetValue().AsInt64();

                        //not a null pptr
                        if (pathId == 0)
                            continue;

                        AssetID aid = ConvertToAssetID(inst, fileId, pathId);

                        AssetExternal ext = am.GetExtAsset(inst, fileId, pathId);

                        //not already visited and not a gameobject or monobehaviour
                        if (references.ContainsKey(aid) || ext.info.curFileType == 0x01 || ext.info.curFileType == 0x72)
                            continue;

                        AddReference(aid, IsAsset(ext.info));

                        //recurse through dependencies
                        SetReferencesRecurse(ext.file, ext.instance.GetBaseField());
                    }
                    SetReferencesRecurse(inst, child);
                }
            }
        }
        private void ReplaceReferencesRecurse(AssetsFileInstance inst, AssetTypeValueField field, AssetFileInfoEx inf)
        {
            foreach (AssetTypeValueField child in field.children)
            {
                //not a value (ie int)
                if (!child.templateField.hasValue)
                {
                    //not null
                    if (child == null)
                        return;
                    //not array of values either
                    if (child.templateField.isArray && child.templateField.children[1].valueType != EnumValueTypes.ValueType_None)
                        break;
                    string typeName = child.templateField.type;
                    //is a pptr
                    if (typeName.StartsWith("PPtr<") && typeName.EndsWith(">") && child.childrenCount == 2)
                    {
                        int fileId = child.Get("m_FileID").GetValue().AsInt();
                        long pathId = child.Get("m_PathID").GetValue().AsInt64();

                        //not a null pptr
                        if (pathId == 0)
                            continue;

                        AssetID aid = ConvertToAssetID(inst, fileId, pathId);
                        //not already visited
                        if (references.ContainsKey(aid))
                        {
                            AssetID id = references[aid];
                            //normally, I would've just checked if the path names
                            //matched up, but this is faster than looking up names
                            //I check type of this asset and compare with the name
                            //of the assetid to see if it should be itself or if
                            //it should be the dependency file
                            bool isSelfAsset = IsAsset(inf);
                            bool isDepAsset = id.fileName == ASSET_LEVEL_NAME;
                            int newFileId = isDepAsset ^ isSelfAsset ? 1 : 0;

                            child.Get("m_FileID").GetValue().Set(newFileId);
                            child.Get("m_PathID").GetValue().Set(id.pathId);
                        }
                        else
                        {
                            ///////////////
                            AssetsFileInstance depInst = ConvertToInstance(inst, fileId);
                            AssetFileInfoEx depInf = depInst.table.GetAssetInfo(pathId);
                            if (depInf.curFileType == 0x72)
                            {
                                ushort scriptIndex = depInst.file.typeTree.unity5Types[depInf.curFileTypeOrIndex].scriptIndex;
                                if (tk2dSpriteScriptIndex == 0xffff)
                                {
                                    AssetTypeValueField monoBase = am.GetATI(depInst.file, depInf).GetBaseField();
                                    AssetExternal scriptBaseExt = am.GetExtAsset(depInst, monoBase.Get("m_Script"));
                                    if (scriptBaseExt.instance != null)
                                    {
                                        AssetTypeValueField scriptBase = scriptBaseExt.instance.GetBaseField();
                                        string scriptName = scriptBase.Get("m_ClassName").GetValue().AsString();
                                        if (scriptName == "tk2dSprite")
                                        {
                                            tk2dSpriteScriptIndex = scriptIndex;
                                        }
                                    }
                                }
                                if (tk2dSpriteScriptIndex == depInst.file.typeTree.unity5Types[depInf.curFileTypeOrIndex].scriptIndex)
                                {
                                    string managedPath = Path.Combine(Path.GetDirectoryName(depInst.path), "Managed");
                                    AssetTypeValueField spriteBase = am.GetMonoBaseFieldCached(depInst, depInf, managedPath);
                                    int spriteId = spriteBase.Get("_spriteId").GetValue().AsInt();

                                    AssetExternal colBaseExt = am.GetExtAsset(depInst, spriteBase.Get("collection"));
                                    AssetsFileInstance colInst = colBaseExt.file;
                                    AssetTypeValueField colBase = am.GetMonoBaseFieldCached(colInst, colBaseExt.info, managedPath);
                                    AssetTypeValueField spriteDefinitions = colBase.Get("spriteDefinitions")[spriteId];

                                    AssetTypeValueField positionsField = spriteDefinitions.Get("positions");
                                    AssetTypeValueField uvsField = spriteDefinitions.Get("uvs");
                                    AssetTypeValueField indicesField = spriteDefinitions.Get("indices");

                                    Vector3[] positions = new Vector3[positionsField.GetChildrenCount()];
                                    Vector2[] uvs = new Vector2[uvsField.GetChildrenCount()];
                                    int[] indices = new int[indicesField.GetChildrenCount()];

                                    for (int i = 0; i < positions.Length; i++)
                                    {
                                        AssetTypeValueField positionField = positionsField[i];
                                        positions[i] = new Vector3()
                                        {
                                            x = positionField.Get("x").GetValue().AsFloat(),
                                            y = positionField.Get("y").GetValue().AsFloat(),
                                            z = positionField.Get("z").GetValue().AsFloat()
                                        };
                                    }
                                    for (int i = 0; i < uvs.Length; i++)
                                    {
                                        AssetTypeValueField uvField = uvsField[i];
                                        uvs[i] = new Vector2()
                                        {
                                            x = uvField.Get("x").GetValue().AsFloat(),
                                            y = uvField.Get("y").GetValue().AsFloat()
                                        };
                                    }
                                    for (int i = 0; i < indices.Length; i++)
                                    {
                                        AssetTypeValueField indexField = indicesField[i];
                                        indices[i] = indexField.GetValue().AsInt();
                                    }

                                    AssetID thisAid = ConvertToAssetID(inst, 0, inf.index);
                                    tk2dFromGoLookup[thisAid] = new Tk2dInfo(positions, uvs, indices);
                                }
                            }
                            ///////////////
                            child.Get("m_FileID").GetValue().Set(0);
                            child.Get("m_PathID").GetValue().Set(0);
                        }
                    }
                    ReplaceReferencesRecurse(inst, child, inf);
                }
            }
        }

        private AssetID ConvertToAssetID(AssetsFileInstance inst, int fileId, long pathId)
        {
            return new AssetID(ConvertToInstance(inst, fileId).path, pathId);
        }

        private AssetsFileInstance ConvertToInstance(AssetsFileInstance inst, int fileId)
        {
            if (fileId == 0)
                return inst;
            else
                return inst.dependencies[fileId - 1];
        }

        private void FixAsset(AssetsFileInstance inst, AssetTypeValueField field, AssetFileInfoEx inf)
        {
            if (inf.curFileType == 0x01) //fix gameobject
            {
                AssetTypeValueField Array = field.Get("m_Component").Get("Array");
                //remove all null pointers
                List<AssetTypeValueField> newFields = Array.children.Where(f =>
                    f.children[0].children[1].GetValue().AsInt64() != 0
                ).ToList();

                //add editdiffer monobehaviour
                AssetID aid = ConvertToAssetID(inst, 0, inf.index);

                newFields.Add(CreatePPtrField(0, sceneId)); //this will be pathId that the below will go into
                AddReplacer(CreateEditDifferMonoBehaviour(references[aid].pathId, Array, aid), 0x72, 0x0000, false);

                if (tk2dFromGoLookup.ContainsKey(aid))
                {
                    newFields.Add(CreatePPtrField(0, sceneId)); //ditto
                    AddReplacer(CreateTk2DEmulatorMonoBehaviour(references[aid].pathId, tk2dFromGoLookup[aid]), 0x72, 0x0001, false);
                }

                int newSize = newFields.Count;
                Array.SetChildrenList(newFields.ToArray());
                Array.GetValue().Set(new AssetTypeArray() { size = newSize });
            }
            else if (inf.curFileType == 0x1c) //fix texture2d
            {
                AssetTypeValueField path = field.Get("m_StreamData").Get("path");
                string pathString = path.GetValue().AsString();
                string directory = Path.GetDirectoryName(inst.path);
                string fixedPath = Path.Combine(directory, pathString);
                path.GetValue().Set(fixedPath);
            }
            else if (inf.curFileType == 0x53) //fix audioclip
            {
                AssetTypeValueField path = field.Get("m_Resource").Get("m_Source");
                string pathString = path.GetValue().AsString();
                string directory = Path.GetDirectoryName(inst.path);
                string fixedPath = Path.Combine(directory, pathString);
                path.GetValue().Set(fixedPath);
            }
        }

        private byte[] CreateEditDifferMonoBehaviour(long goPid, AssetTypeValueField componentArray, AssetID origGoPptr)
        {
            byte[] data;
            using (MemoryStream ms = new MemoryStream())
            using (AssetsFileWriter w = new AssetsFileWriter(ms))
            {
                w.bigEndian = false;
                w.Write(0);
                w.Write(goPid);
                w.Write(1);
                w.Write(2);
                w.Write((long)11500000);
                w.WriteCountStringInt32("");
                w.Align();

                w.Write(0);
                w.Write(origGoPptr.pathId);
                w.Write(origGoPptr.pathId);
                w.Write(0);

                int componentArrayLength = componentArray.GetValue().AsArray().size;
                w.Write(componentArrayLength);
                for (int i = 0; i < componentArrayLength; i++)
                {
                    AssetTypeValueField component = componentArray[i].Get("component");
                    int m_FileID = component.Get("m_FileID").GetValue().AsInt();
                    long m_PathID = component.Get("m_PathID").GetValue().AsInt64();
                    if (m_PathID == 0) //removed (monobehaviour)
                        w.Write((long)-1);
                    else if (m_FileID == 0) //correct file
                        w.Write(m_PathID);
                    else //another file (shouldn't happen?)
                        w.Write((long)0);
                }

                w.Write(0/*rand.Next()*/);
                data = ms.ToArray();
            }
            return data;
        }

        private byte[] CreateTk2DEmulatorMonoBehaviour(long goPid, Tk2dInfo tk2dInfo)
        {
            byte[] data;
            using (MemoryStream ms = new MemoryStream())
            using (AssetsFileWriter w = new AssetsFileWriter(ms))
            {
                w.bigEndian = false;
                w.Write(0);
                w.Write(goPid);
                w.Write(1);
                w.Write(3);
                w.Write((long)11500000);
                w.WriteCountStringInt32("");
                w.Align();

                w.Write(tk2dInfo.positions.Length);
                for (int i = 0; i < tk2dInfo.positions.Length; i++)
                {
                    Vector3 position = tk2dInfo.positions[i];
                    w.Write(position.x);
                    w.Write(position.y);
                    w.Write(position.z);
                }
                w.Align();

                w.Write(tk2dInfo.uvs.Length);
                for (int i = 0; i < tk2dInfo.uvs.Length; i++)
                {
                    Vector3 uv = tk2dInfo.uvs[i];
                    w.Write(uv.x);
                    w.Write(uv.y);
                }
                w.Align();

                w.Write(tk2dInfo.indices.Length);
                for (int i = 0; i < tk2dInfo.indices.Length; i++)
                {
                    w.Write(tk2dInfo.indices[i]);
                }
                w.Align();

                data = ms.ToArray();
            }
            return data;
        }

        //time to update the api I guess
        private AssetTypeValueField CreatePPtrField(int fileId, long pathId)
        {
            AssetTypeTemplateField pptrTemp = new AssetTypeTemplateField();
            pptrTemp.FromClassDatabase(am.classFile, AssetHelper.FindAssetClassByID(am.classFile, 0x01), 5); //[5] PPtr<Component> component
            AssetTypeValueField fileVal = new AssetTypeValueField()
            {
                templateField = pptrTemp.children[0],
                childrenCount = 0,
                children = null,
                value = new AssetTypeValue(EnumValueTypes.ValueType_Int32, fileId)
            };
            AssetTypeValueField pathVal = new AssetTypeValueField()
            {
                templateField = pptrTemp.children[1],
                childrenCount = 0,
                children = null,
                value = new AssetTypeValue(EnumValueTypes.ValueType_Int64, pathId)
            };
            AssetTypeValueField pptrVal = new AssetTypeValueField()
            {
                templateField = pptrTemp,
                childrenCount = 2,
                children = new AssetTypeValueField[] { fileVal, pathVal },
                value = new AssetTypeValue(EnumValueTypes.ValueType_None, null)
            };
            return pptrVal;
        }

        private bool IsAsset(AssetFileInfoEx inf)
        {
            return inf.curFileType == 0x1c || inf.curFileType == 0x30 || inf.curFileType == 0x53;
        }
        private bool IsAsset(int id)
        {
            return id == 0x1c || id == 0x30 || id == 0x53;
        }
    }
}
