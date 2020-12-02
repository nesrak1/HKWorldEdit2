using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Assets.Bundler
{
    public static class MonoClassFixes
    {
        public static AssetTypeValueField GetMonoBaseFieldCached(this AssetsManager am, AssetsFileInstance inst, AssetFileInfoEx info, string managedPath, List<string> fileNames, Dictionary<AssetID, long> aidToPid)
        {
            AssetsFile file = inst.file;
            ushort scriptIndex = file.typeTree.unity5Types[info.curFileTypeOrIndex].scriptIndex;
            if (scriptIndex != 0xFFFF && inst.templateFieldCache.ContainsKey(scriptIndex))
            {
                AssetTypeTemplateField baseTemplateField = inst.templateFieldCache[scriptIndex];
                AssetTypeInstance baseAti = new AssetTypeInstance(baseTemplateField, file.reader, info.absoluteFilePos);
                return baseAti.GetBaseField();
            }
            else
            {
                AssetTypeTemplateField baseField = new AssetTypeTemplateField();
                baseField.FromClassDatabase(am.classFile, AssetHelper.FindAssetClassByID(am.classFile, info.curFileType), 0);
                AssetTypeInstance mainAti = new AssetTypeInstance(baseField, file.reader, info.absoluteFilePos);
                if (file.typeTree.unity5Types[info.curFileTypeOrIndex].scriptIndex != 0xFFFF)
                {
                    AssetTypeValueField m_Script = mainAti.GetBaseField().Get("m_Script");
                    int m_ScriptFileId = m_Script.Get("m_FileID").GetValue().AsInt();
                    long m_ScriptPathId = m_Script.Get("m_PathID").GetValue().AsInt64();
                    AssetID id = new AssetID(fileNames[-m_ScriptFileId], m_ScriptPathId);
                    long m_ScriptRealPathId = aidToPid[id];
                    AssetTypeInstance scriptAti = am.GetExtAsset(inst, 0, m_ScriptRealPathId).instance;
                    string scriptName = scriptAti.GetBaseField().Get("m_Name").GetValue().AsString();
                    string assemblyName = scriptAti.GetBaseField().Get("m_AssemblyName").GetValue().AsString();
                    string assemblyPath = Path.Combine(managedPath, assemblyName);
                    Console.WriteLine("checking " + scriptName + " in " + assemblyName + " from id " + info.index);
                    if (File.Exists(assemblyPath))
                    {
                        MonoDeserializer mc = new MonoDeserializer();
                        mc.Read(scriptName, assemblyPath, inst.file.header.format);
                        List<AssetTypeTemplateField> monoTemplateFields = mc.children;

                        AssetTypeTemplateField[] templateField = baseField.children.Concat(monoTemplateFields).ToArray();
                        baseField.children = templateField;
                        baseField.childrenCount = baseField.children.Length;

                        mainAti = new AssetTypeInstance(baseField, file.reader, info.absoluteFilePos);
                    }
                }

                AssetTypeValueField baseValueField = mainAti.GetBaseField();
                inst.templateFieldCache[scriptIndex] = baseValueField.templateField;
                return baseValueField;
            }
        }
        public static AssetExternal GetExtAsset(this AssetsManager am, AssetsFileInstance relativeTo, int fileId, long pathId, bool onlyGetInfo = false)
        {
            AssetExternal ext = new AssetExternal();
            if (fileId == 0 && pathId == 0)
            {
                ext.info = null;
                ext.instance = null;
                ext.file = null;
            }
            else if (fileId != 0)
            {
                AssetsFileInstance dep = relativeTo.dependencies[fileId - 1];
                ext.info = dep.table.GetAssetInfo(pathId);
                if (!onlyGetInfo)
                    ext.instance = am.GetATI(dep.file, ext.info);
                else
                    ext.instance = null;
                ext.file = dep;
            }
            else
            {
                ext.info = relativeTo.table.GetAssetInfo(pathId);
                if (!onlyGetInfo)
                    ext.instance = am.GetATI(relativeTo.file, ext.info);
                else
                    ext.instance = null;
                ext.file = relativeTo;
            }
            return ext;
        }
    }
}
