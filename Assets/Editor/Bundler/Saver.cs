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
        const string ver = "2020.2.2f1";
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

        public static void GenerateDiffFile(AssetsManager am, AssetsFileInstance buildInst, AssetsFileInstance sceneInst, HKWEMeta meta)
        {
            EditorUtility.DisplayProgressBar("HKEdit", "Reading dependencies...", 0.5f);
            am.UpdateDependencies();

            ClassDatabaseFile cldb = am.classFile;

            DiffData result = new DiffData()
            {
                goChanges = new List<GameObjectChange>(),
                goAdditions = new List<GameObjectAddition>()
            };

            Dictionary<EditDifferData, long> differToSceneId = new Dictionary<EditDifferData, long>();
            Dictionary<long, EditDifferData> buildIdToDiffer = new Dictionary<long, EditDifferData>();
            List<EditDifferData> differData = new List<EditDifferData>();

            AssetsFileTable sceneTable = sceneInst.table;
            List<AssetFileInfoEx> sceneGos = sceneTable.GetAssetsOfType(0x01);
            for (int i = 0; i < sceneGos.Count; i++)
            {
                if (i % 100 == 0)
                    EditorUtility.DisplayProgressBar("HKEdit", "Finding diff IDs... (step 1/3)", (float)i / sceneGos.Count);
                AssetFileInfoEx inf = sceneGos[i];
                AssetTypeValueField baseField = am.GetATI(sceneInst.file, inf).GetBaseField();

                AssetTypeValueField editDifferMono = GetEDMono(am, sceneInst, baseField);

                EditDifferData diff = new EditDifferData()
                {
                    fileId = editDifferMono.Get("fileId").GetValue().AsInt(),
                    pathId = editDifferMono.Get("pathId").GetValue().AsInt64(),
                    origPathId = editDifferMono.Get("origPathId").GetValue().AsInt64(),
                    newAsset = editDifferMono.Get("newAsset").GetValue().AsBool()
                };

                buildIdToDiffer[diff.origPathId] = diff;
                differToSceneId[diff] = inf.index;
                differData.Add(diff);
            }

            //////////////////////////

            AssetsFileTable origTable = buildInst.table;
            List<AssetFileInfoEx> origGos = origTable.GetAssetsOfType(0x01);

            List<long> origDeletIds = new List<long>();
            //int nextBundleId = 1;

            //// == delete changes == //
            //for (int i = 0; i < origGos.Count; i++)
            //{
            //    if (i % 100 == 0)
            //        EditorUtility.DisplayProgressBar("HKEdit", "Checking for deletes... (step 2/3)", (float)i / origGos.Count);
            //    AssetFileInfoEx inf = sceneGos[i];
            //    if (!differData.Any(d => d.origPathId == inf.index))
            //    {
            //        GameObjectChange change = new GameObjectChange
            //        {
            //            flags = GameObjectChangeFlags.Deleted
            //        };
            //        result.goChanges.Add(change);
            //        origDeletIds.Add(inf.index);
            //    }
            //}

            // == add changes == //
            //to get this working in a built game, we need
            //built assets (ie pngs -> texture2d) the problem
            //is there's no easy way to direct unity to do that
            //without loading the scene and using unity's api
            //but we can pull out assets into a prefab and build
            //the prefab but there are problems with duplicate
            //dependencies being copied, so we pack them all
            //into one place so that doesn't happen
            //(for reference, in hkwe1, each gameobject got
            //its own prefab)

            //find dependencies
            ReferenceCrawlerBundle createdCrawler = new ReferenceCrawlerBundle(am); //assets created by the user in the ditor
            ReferenceCrawlerBundle existingCrawler = new ReferenceCrawlerBundle(am); //assets that already existed in the scene
            for (int i = 0; i < differData.Count; i++)
            {
                if (i % 100 == 0)
                    EditorUtility.DisplayProgressBar("HKEdit", "Checking for additions... (step 2/3)", (float)i / differData.Count);
                EditDifferData dat = differData[i];
                if (dat.newAsset)
                {
                    long sceneId = differToSceneId[dat];
                    AssetFileInfoEx inf = sceneInst.table.GetAssetInfo(sceneId);
                    createdCrawler.SetReferences(sceneInst, inf);
                    GameObjectAddition addition = new GameObjectAddition
                    {
                        bundleId = createdCrawler.GetNextId(), //?
                        sceneId = dat.pathId,
                        dependencies = new List<GameObjectAdditionDependency>()
                    };
                    //nextBundleId++;
                    foreach (KeyValuePair<AssetID, AssetID> goRef in createdCrawler.references)
                    {
                        addition.dependencies.Add(new GameObjectAdditionDependency
                        {
                            sceneId = goRef.Key.pathId,
                            bundleId = goRef.Value.pathId
                        });
                    }
                    result.goAdditions.Add(addition);
                }
                else
                {
                    long newPathId = differToSceneId[dat];
                    AssetFileInfoEx inf = sceneInst.table.GetAssetInfo(newPathId);
                    existingCrawler.SetReferences(sceneInst, inf);
                }
            }

            //load up all created assets into a prefab
            List<Type_0D> types = new List<Type_0D>();
            List<string> typeNames = new List<string>();

            foreach (AssetsReplacer rep in createdCrawler.sceneReplacers)
            {
                ClassDatabaseType clType = AssetHelper.FindAssetClassByID(cldb, (uint)rep.GetClassID());
                string clName = clType.name.GetString(cldb);
                if (!typeNames.Contains(clName))
                {
                    Type_0D type0d = C2T5.Cldb2TypeTree(cldb, clName);
                    type0d.classId = clType.classId;
                    types.Add(type0d);
                    typeNames.Add(clName);
                }
            }

            List<AssetsReplacer> replacers = new List<AssetsReplacer>();
            replacers.Add(CreatePrefabAsset(2)); //better hope id 2 is a gameobject
            replacers.AddRange(createdCrawler.sceneReplacers);

            AssetsFile createdFile = new AssetsFile(new AssetsFileReader(new MemoryStream(BundleCreator.CreateBlankAssets(ver, types))));

            byte[] data;
            using (MemoryStream ms = new MemoryStream())
            using (AssetsFileWriter writer = new AssetsFileWriter(ms))
            {
                createdFile.Write(writer, 0, replacers, 0);
                data = ms.ToArray();
            }
        }

        private static AssetsReplacer CreatePrefabAsset(long rootId)
        {
            //the 2017 cldb doesn't have prefab in it so
            //we're on our own with binary writer again

            MemoryStream ms = new MemoryStream();
            AssetsFileWriter writer = new AssetsFileWriter(ms);
            writer.bigEndian = false;
            writer.Write((uint)1);

            writer.Write(0);
            writer.Write((long)0);

            writer.Write(0);
            writer.Write(0);

            writer.Write(0);
            writer.Write((long)0);

            writer.Write(0);
            writer.Write(rootId);

            writer.Write((byte)0);
            writer.Align();

            return new AssetsReplacerFromMemory(0, 1, 0x3e9, 0xffff, ms.ToArray());
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
                    //TODO THIS ACTUALLY WONT WORK NOW THAT WE HAVE 2
                    ext = am.GetExtAsset(fileInst, component, false);
                    AssetTypeValueField monoBaseField = ext.instance.GetBaseField();
                    return monoBaseField;
                }
            }
            return null;
        }
    }
}
