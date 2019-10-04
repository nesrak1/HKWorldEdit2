using Assets.Editor;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;

namespace Assets.Bundler
{
    public class Saver
    {
        const string ver = "2017.4.10f1";
        public static void GenerateDiffFile(string newLevelPath, string metaPath)
        {
            string metadata = File.ReadAllText(metaPath);
            HKWEMeta meta = new HKWEMeta();
            EditorJsonUtility.FromJsonOverwrite(metadata, meta);
            string levelPath = meta.diffPath;

            AssetsManager am = new AssetsManager();
            EditorUtility.DisplayProgressBar("HKEdit", "Loading class database...", 0f);
            am.LoadClassPackage("cldb.dat");
            am.useTemplateFieldCache = true;
            EditorUtility.DisplayProgressBar("HKEdit", "Reading level files...", 0.25f);
            GenerateDiffFile(am, am.LoadAssetsFile(levelPath, false), am.LoadAssetsFile(newLevelPath, false), meta);
        }

        public static void GenerateDiffFile(AssetsManager am, AssetsFileInstance inst, AssetsFileInstance newInst, HKWEMeta meta)
        {
            EditorUtility.DisplayProgressBar("HKEdit", "Reading dependencies...", 0.5f);
            am.UpdateDependencies();

            Dictionary<AssetID, AssetID> newToOldIds = new Dictionary<AssetID, AssetID>();

            AssetsFileTable newTable = newInst.table;

            List<AssetFileInfoEx> initialGameObjects = newTable.GetAssetsOfType(0x01);
            for (int i = 0; i < initialGameObjects.Count; i++)
            {
                if (i % 100 == 0)
                    EditorUtility.DisplayProgressBar("HKEdit", "Finding diff IDs... (step 1/3)", (float)i / initialGameObjects.Count);
                AssetFileInfoEx inf = initialGameObjects[i];
                AssetTypeValueField baseField = am.GetATI(newInst.file, inf).GetBaseField();

                AssetTypeValueField editDifferMono = GetEDMono(am, newInst, baseField);

                EditDifferData diff = new EditDifferData()
                {
                    fileId = editDifferMono.Get("fileId").GetValue().AsInt(),
                    pathId = editDifferMono.Get("pathId").GetValue().AsInt64(),
                    origPathId = editDifferMono.Get("origPathId").GetValue().AsInt64(),
                    newAsset = editDifferMono.Get("newAsset").GetValue().AsBool()
                };
            }
        }

        private static AssetTypeValueField GetEDMono(AssetsManager am, AssetsFileInstance fileInst, AssetTypeValueField goBaseField)
        {
            AssetTypeValueField m_Components = goBaseField.Get("m_Components").Get("Array");
            for (uint i = 0; i < m_Components.GetValue().AsArray().size; i++)
            {
                AssetTypeValueField component = m_Components[i];
                AssetsManager.AssetExternal ext = am.GetExtAsset(fileInst, component, true);
                if (ext.info.curFileType == 0x72)
                {
                    ext = am.GetExtAsset(fileInst, component, true, true);
                    AssetTypeValueField monoBaseField = ext.instance.GetBaseField();
                    return monoBaseField;
                }
            }
            return null;
        }
    }
}
