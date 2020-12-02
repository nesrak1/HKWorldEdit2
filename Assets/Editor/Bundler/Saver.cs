using Assets.Editor;
using Assets.Editor.Bundler;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

/*
gameobject
  added:
    add full obj to bundle
  cloned:
    add full obj to bundle
    check for ptrs to original assets
  removed:
    only need to add to info
  details changed: (ie name, layer/tag)
    only need to add to info
  child of original obj:
    add full obj to bundle and info of parent
  original obj is child:
    wtf no please don't but probably info

component
  added:
    add component to dummy obj and add local deps
  removed:
    only need to add to info
  values changed:
    diff values and add local deps
*/

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
            am.LoadClassDatabase("cldb.dat");
            am.useTemplateFieldCache = true;
            EditorUtility.DisplayProgressBar("HKEdit", "Reading level files...", 0.25f);
            GenerateDiffFile(am, am.LoadAssetsFile(levelPath, false), am.LoadAssetsFile(newLevelPath, false), meta);
        }

        public static void GenerateDiffFile(AssetsManager am, AssetsFileInstance origInst, AssetsFileInstance newInst, HKWEMeta meta)
        {
            EditorUtility.DisplayProgressBar("HKEdit", "Reading dependencies...", 0.5f);
            am.UpdateDependencies();

            DiffData result = new DiffData()
            {
                goChanges = new List<GameObjectChange>(),
                goAdditions = new List<GameObjectAddition>()
            };

            Dictionary<EditDifferData, long> differToNewId = new Dictionary<EditDifferData, long>();
            Dictionary<long, EditDifferData> origIdToDiffer = new Dictionary<long, EditDifferData>();
            List<EditDifferData> differData = new List<EditDifferData>();

            AssetsFileTable newTable = newInst.table;
            List<AssetFileInfoEx> newGos = newTable.GetAssetsOfType(0x01);
            for (int i = 0; i < newGos.Count; i++)
            {
                if (i % 100 == 0)
                    EditorUtility.DisplayProgressBar("HKEdit", "Finding diff IDs... (step 1/3)", (float)i / newGos.Count);
                AssetFileInfoEx inf = newGos[i];
                AssetTypeValueField baseField = am.GetATI(newInst.file, inf).GetBaseField();

                AssetTypeValueField editDifferMono = GetEDMono(am, newInst, baseField);

                EditDifferData diff = new EditDifferData()
                {
                    fileId = editDifferMono.Get("fileId").GetValue().AsInt(),
                    pathId = editDifferMono.Get("pathId").GetValue().AsInt64(),
                    origPathId = editDifferMono.Get("origPathId").GetValue().AsInt64(),
                    newAsset = editDifferMono.Get("newAsset").GetValue().AsBool()
                };

                origIdToDiffer[diff.origPathId] = diff;
                differData.Add(diff);
            }

            //////////////////////////
            
            AssetsFileTable origTable = origInst.table;
            List<AssetFileInfoEx> origGos = origTable.GetAssetsOfType(0x01);

            List<long> origDeletIds = new List<long>();
            int nextBundleId = 1;

            // == delete changes == //
            for (int i = 0; i < origGos.Count; i++)
            {
                if (i % 100 == 0)
                    EditorUtility.DisplayProgressBar("HKEdit", "Checking for deletes... (step 2/3)", (float)i / origGos.Count);
                AssetFileInfoEx inf = newGos[i];
                if (!differData.Any(d => d.origPathId == inf.index))
                {
                    GameObjectChange change = new GameObjectChange
                    {
                        flags = GameObjectChangeFlags.Deleted
                    };
                    result.goChanges.Add(change);
                    origDeletIds.Add(inf.index);
                }
            }

            // == add changes == //
            for (int i = 0; i < differData.Count; i++)
            {
                if (i % 100 == 0)
                    EditorUtility.DisplayProgressBar("HKEdit", "Checking for additions... (step 2/3)", (float)i / differData.Count);
                EditDifferData dat = differData[i];
                if (dat.newAsset)
                {
                    ReferenceCrawlerBundle crawler = new ReferenceCrawlerBundle(am);
                    long newPathId = differToNewId[dat];
                    AssetFileInfoEx inf = newInst.table.GetAssetInfo(newPathId);
                    crawler.SetReferences(newInst, inf);
                    GameObjectAddition addition = new GameObjectAddition
                    {
                        bundleId = nextBundleId,
                        parentId = dat.pathId,
                        dependencies = new List<GameObjectAdditionDependency>()
                    };
                    nextBundleId++;
                    foreach (KeyValuePair<AssetID, AssetID> goRef in crawler.references)
                    {
                        addition.dependencies.Add(new GameObjectAdditionDependency
                        {
                            parentId = goRef.Key.pathId,
                            bundleId = goRef.Value.pathId
                        });
                    }
                    result.goAdditions.Add(addition);
                }
                else
                {
                    ReferenceCrawlerBundle crawler = new ReferenceCrawlerBundle(am);
                    long newPathId = differToNewId[dat];
                    AssetFileInfoEx inf = newInst.table.GetAssetInfo(newPathId);
                    crawler.SetReferences(newInst, inf);
                }
            }
        }

        private static AssetTypeValueField GetEDMono(AssetsManager am, AssetsFileInstance fileInst, AssetTypeValueField goBaseField)
        {
            AssetTypeValueField m_Components = goBaseField.Get("m_Components").Get("Array");
            for (int i = 0; i < m_Components.GetValue().AsArray().size; i++)
            {
                AssetTypeValueField component = m_Components[i];
                AssetExternal ext = am.GetExtAsset(fileInst, component, true);
                if (ext.info.curFileType == 0x72)
                {
                    //todo, check if this is the right monob
                    //as there's only one this is fine for now
                    //but I still hate this
                    ext = am.GetExtAsset(fileInst, component, false);
                    AssetTypeValueField monoBaseField = ext.instance.GetBaseField();
                    return monoBaseField;
                }
            }
            return null;
        }
    }
}
