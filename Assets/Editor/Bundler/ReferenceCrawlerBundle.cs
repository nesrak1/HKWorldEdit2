using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Assets.Bundler
{
    //crawl dependencies for editor bundle -> game files
    public class ReferenceCrawlerBundle
    {
        //the difference is here that we want to keep dependencies
        //intact which means fileIds can't be touched, only pathIds
        const string SCENE_LEVEL_NAME = "bundlescene";
        public Dictionary<AssetID, AssetID> references; //scene space to bundle space
        public List<AssetsReplacer> sceneReplacers;
        private AssetsManager am;
        private int sceneId;
        public ReferenceCrawlerBundle(AssetsManager am)
        {
            this.am = am;
            sceneId = 1;
            references = new Dictionary<AssetID, AssetID>();
            sceneReplacers = new List<AssetsReplacer>();
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
            //FixAsset(baseField, inf);
            byte[] baseFieldData = baseField.WriteToByteArray();
            AssetsReplacer replacer = new AssetsReplacerFromMemory(0, pathId, (int)inf.curFileType, 0xFFFF, baseFieldData);
            if (inf.curFileType != 0x72)
                sceneReplacers.Add(replacer);
        }
        public void AddReplacer(byte[] data, int type, ushort monoType)
        {
            AssetsReplacer replacer = new AssetsReplacerFromMemory(0, sceneId, type, monoType, data);
            if (type != 0x72)
                sceneReplacers.Add(replacer);
            sceneId++;
        }
        public void AddReference(AssetID aid)
        {
            AssetID newAid = new AssetID(SCENE_LEVEL_NAME, sceneId);
            references.Add(aid, newAid);
            sceneId++;
        }
        public int GetNextId()
        {
            int ret = sceneId;
            sceneId++;
            return ret;
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

                        //not already visited and not a monobehaviour
                        if (references.ContainsKey(aid) || ext.info.curFileType == 0x72)
                            continue;

                        AddReference(aid);

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

                            child.Get("m_FileID").GetValue().Set(0);
                            child.Get("m_PathID").GetValue().Set(id.pathId);
                        }
                        else
                        {
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
            string fileName;
            if (fileId == 0)
                fileName = inst.path;
            else
                fileName = inst.dependencies[fileId - 1].path;
            return new AssetID(fileName, pathId);
        }

        private void FixAsset(AssetTypeValueField field, AssetFileInfoEx inf)
        {
            if (inf.curFileType == 0x01) //fix gameobject
            {
                AssetTypeValueField Array = field.Get("m_Component").Get("Array");
                //remove all null pointers
                List<AssetTypeValueField> newFields = Array.children.Where(f =>
                    f.children[0].children[1].GetValue().AsInt64() != 0
                ).ToList();

                int newSize = newFields.Count;
                Array.SetChildrenList(newFields.ToArray());
                Array.GetValue().Set(new AssetTypeArray() { size = newSize });
            }
            else if (inf.curFileType == 0x1c) //fix texture2d
            {
                AssetTypeValueField path = field.Get("m_StreamData").Get("path");
                string pathString = path.GetValue().AsString();
                string fixedPath = Path.GetFileName(pathString);
                path.GetValue().Set(fixedPath);
            }
            else if (inf.curFileType == 0x53) //fix audioclip
            {
                AssetTypeValueField path = field.Get("m_Resource").Get("m_Source");
                string pathString = path.GetValue().AsString();
                string fixedPath = Path.GetFileName(pathString);
                path.GetValue().Set(fixedPath);
            }
        }
    }
}
