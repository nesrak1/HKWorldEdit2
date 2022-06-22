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
        //creates a lookup where assets from different built game files
        //with different fileIds and pathIds can be converted to a list
        //of pathIds in order with fileId 0 or 1 depending on the asset type
        //(stored in the references dictionary)
        public void SetReferences(AssetsFileInstance inst, AssetFileInfoEx inf)
        {
            AssetTypeValueField baseField = am.GetATI(inst.file, inf).GetBaseField();
            SetReferencesRecurse(inst, baseField);
        }
        //replaces the PPtr entries in each asset with the new pathId
        //(.Key with .Value in the references dictionary) and fixes some
        //specific problems with assets like GameObject or Texture2D
        //(stored in sceneReplacers and assetReplacers)
        public void ReplaceReferences(AssetsFileInstance inst, AssetFileInfoEx inf, long pathId)
        {
            AssetTypeValueField baseField = am.GetATI(inst.file, inf).GetBaseField();

            FixAssetPre(inst, baseField, inf);
            ReplaceReferencesRecurse(inst, baseField, inf);
            FixAssetPost(inst, baseField, inf);

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
                            /////////////// todo move to another method
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

                                    if (colInst != null)
                                    {
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

        private void FixAssetPre(AssetsFileInstance inst, AssetTypeValueField field, AssetFileInfoEx inf)
        {
            if (inf.curFileType == 0xd5) //fix sprite
            {
                AssetTypeValueField renderDataKey = field.Get("m_RenderDataKey");
                AssetTypeValueField spriteAtlas = field.Get("m_SpriteAtlas");
                long spriteAtlasPathId = spriteAtlas.Get("m_PathID").GetValue().AsInt64();

                uint rid0 = renderDataKey.Get("first")[0].GetValue().AsUInt();
                uint rid1 = renderDataKey.Get("first")[1].GetValue().AsUInt();
                uint rid2 = renderDataKey.Get("first")[2].GetValue().AsUInt();
                uint rid3 = renderDataKey.Get("first")[3].GetValue().AsUInt();

                //editor can't read these for whatever reason
                if (spriteAtlasPathId != 0)
                {
                    AssetExternal spriteAtlasExt = am.GetExtAsset(inst, spriteAtlas);
                    AssetTypeValueField spriteAtlasBase = spriteAtlasExt.instance.GetBaseField();
                    AssetTypeValueField renderDataMap = spriteAtlasBase.Get("m_RenderDataMap").Get("Array");
                    int renderDataMapCount = renderDataMap.GetValue().AsArray().size;

                    int renderDataIndex = -1;
                    for (int i = 0; i < renderDataMapCount; i++)
                    {
                        AssetTypeValueField renderDataMapKey = renderDataMap[i].Get("first");

                        uint thisrid0 = renderDataMapKey.Get("first")[0].GetValue().AsUInt();
                        uint thisrid1 = renderDataMapKey.Get("first")[1].GetValue().AsUInt();
                        uint thisrid2 = renderDataMapKey.Get("first")[2].GetValue().AsUInt();
                        uint thisrid3 = renderDataMapKey.Get("first")[3].GetValue().AsUInt();

                        if (thisrid0 == rid0 && thisrid1 == rid1 && thisrid2 == rid2 && thisrid3 == rid3)
                        {
                            renderDataIndex = i;
                            break;
                        }
                    }

                    if (renderDataIndex != -1)
                    {
                        AssetTypeValueField spriteAtlasRD = renderDataMap[renderDataIndex].Get("second");
                        AssetTypeValueField spriteRD = field.Get("m_RD");

                        //texture
                        AssetTypeValueField spriteAtlasTexture = spriteAtlasRD.Get("texture");
                        AssetTypeValueField spriteTexture = spriteRD.Get("texture");
                        spriteTexture.Get("m_FileID").GetValue().Set(spriteAtlasTexture.Get("m_FileID").GetValue().AsInt());
                        spriteTexture.Get("m_PathID").GetValue().Set(spriteAtlasTexture.Get("m_PathID").GetValue().AsInt64());
                        //alphaTexture
                        AssetTypeValueField spriteAtlasAlphaTexture = spriteAtlasRD.Get("alphaTexture");
                        AssetTypeValueField spriteAlphaTexture = spriteRD.Get("alphaTexture");
                        spriteAlphaTexture.Get("m_FileID").GetValue().Set(spriteAtlasAlphaTexture.Get("m_FileID").GetValue().AsInt());
                        spriteAlphaTexture.Get("m_PathID").GetValue().Set(spriteAtlasAlphaTexture.Get("m_PathID").GetValue().AsInt64());
                        //textureRect
                        AssetTypeValueField spriteAtlasTextureRect = spriteAtlasRD.Get("textureRect");
                        AssetTypeValueField spriteTextureRect = spriteRD.Get("textureRect");
                        spriteTextureRect.Get("x").GetValue().Set(spriteAtlasTextureRect.Get("x").GetValue().AsFloat());
                        spriteTextureRect.Get("y").GetValue().Set(spriteAtlasTextureRect.Get("y").GetValue().AsFloat());
                        spriteTextureRect.Get("width").GetValue().Set(spriteAtlasTextureRect.Get("width").GetValue().AsFloat());
                        spriteTextureRect.Get("height").GetValue().Set(spriteAtlasTextureRect.Get("height").GetValue().AsFloat());
                        ////textureRectOffset
                        AssetTypeValueField spriteAtlasTextureRectOffset = spriteAtlasRD.Get("textureRectOffset");
                        AssetTypeValueField spriteTextureRectOffset = spriteRD.Get("textureRectOffset");
                        spriteTextureRectOffset.Get("x").GetValue().Set(spriteAtlasTextureRectOffset.Get("x").GetValue().AsFloat());
                        spriteTextureRectOffset.Get("y").GetValue().Set(spriteAtlasTextureRectOffset.Get("y").GetValue().AsFloat());
                        //atlasRectOffset
                        AssetTypeValueField spriteAtlasAtlasRectOffset = spriteAtlasRD.Get("atlasRectOffset");
                        AssetTypeValueField spriteAtlasRectOffset = spriteRD.Get("atlasRectOffset");
                        spriteAtlasRectOffset.Get("x").GetValue().Set(spriteTextureRectOffset.Get("x").GetValue().AsFloat());
                        spriteAtlasRectOffset.Get("y").GetValue().Set(spriteTextureRectOffset.Get("y").GetValue().AsFloat());
                        spriteAtlasRectOffset.Get("x").GetValue().Set(spriteAtlasAtlasRectOffset.Get("x").GetValue().AsFloat());
                        spriteAtlasRectOffset.Get("y").GetValue().Set(spriteAtlasAtlasRectOffset.Get("y").GetValue().AsFloat());
                        //uvTransform
                        AssetTypeValueField spriteAtlasUvTransform = spriteAtlasRD.Get("uvTransform");
                        AssetTypeValueField spriteUvTransform = spriteRD.Get("uvTransform");
                        spriteUvTransform.Get("x").GetValue().Set(spriteAtlasUvTransform.Get("x").GetValue().AsFloat());
                        spriteUvTransform.Get("y").GetValue().Set(spriteAtlasUvTransform.Get("y").GetValue().AsFloat());
                        spriteUvTransform.Get("z").GetValue().Set(spriteAtlasUvTransform.Get("z").GetValue().AsFloat());
                        spriteUvTransform.Get("w").GetValue().Set(spriteAtlasUvTransform.Get("w").GetValue().AsFloat());
                        //downscaleMultiplier
                        AssetTypeValueField spriteAtlasDownscapeMultiplier = spriteAtlasRD.Get("downscaleMultiplier");
                        AssetTypeValueField spriteDownscapeMultiplier = spriteRD.Get("downscaleMultiplier");
                        spriteDownscapeMultiplier.GetValue().Set(spriteAtlasDownscapeMultiplier.GetValue().AsFloat());
                        //settingsRaw
                        AssetTypeValueField spriteAtlasSettingsRaw = spriteAtlasRD.Get("settingsRaw");
                        AssetTypeValueField spriteSettingsRaw = spriteRD.Get("settingsRaw");
                        spriteSettingsRaw.GetValue().Set(spriteAtlasSettingsRaw.GetValue().AsFloat());

                        spriteAtlas.Get("m_FileID").GetValue().Set(0);
                        spriteAtlas.Get("m_PathID").GetValue().Set((long)0);
                    }
                    //else
                    //{
                    //    Debug.Log("exhausted sprite search");
                    //}
                }
            }
        }

        private void FixAssetPost(AssetsFileInstance inst, AssetTypeValueField field, AssetFileInfoEx inf)
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
            /*new*//*inf.curFileType == 0x5b || inf.curFileType == 0x4a || inf.curFileType == 0x28f3fdef */
        }
        private bool IsAsset(int id)
        {
            return id == 0x1c || id == 0x30 || id == 0x53;
            /*these first two weren't here, was this intentional? id == 0x28f3fdef*/
        }
    }
}
