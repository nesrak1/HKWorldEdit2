using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;

namespace Assets.Bundler
{
    public class Loader
    {
        const string ver = "2020.2.2f1";
        const int hkweVersion = 1;
        public static void GenerateLevelFiles(string levelPath)
        {
            AssetsManager am = new AssetsManager();
            EditorUtility.DisplayProgressBar("HKEdit", "Loading class database...", 0f);
            am.LoadClassDatabase("cldb.dat");
            am.useTemplateFieldCache = true;
            am.updateAfterLoad = false;
            EditorUtility.DisplayProgressBar("HKEdit", "Reading level file...", 0.25f);
            GenerateLevelFiles(am, am.LoadAssetsFile(levelPath, true));
        }

        public static void GenerateLevelFiles(AssetsManager am, AssetsFileInstance inst)
        {
            EditorUtility.DisplayProgressBar("HKEdit", "Reading dependencies...", 0.5f);
            am.UpdateDependencies();

            //quicker asset id lookup
            for (int i = 0; i < am.files.Count; i++)
            {
                AssetsFileInstance afi = am.files[i];
                if (i % 100 == 0)
                    EditorUtility.DisplayProgressBar("HKEdit", "Generating QLTs...", (float)i / am.files.Count);
                afi.table.GenerateQuickLookupTree();
            }

            ClassDatabaseFile cldb = am.classFile;
            AssetsFileTable table = inst.table;

            ReferenceCrawlerEditor crawler = new ReferenceCrawlerEditor(am);

            List<AssetFileInfoEx> initialGameObjects = table.GetAssetsOfType(0x01);
            for (int i = 0; i < initialGameObjects.Count; i++)
            {
                if (i % 100 == 0)
                    EditorUtility.DisplayProgressBar("HKEdit", "Finding necessary assets... (step 1/3)", (float)i / initialGameObjects.Count);
                AssetFileInfoEx inf = initialGameObjects[i];
                crawler.AddReference(new AssetID(inst.path, (long)inf.index), false);
                crawler.SetReferences(inst, inf);
            }

            Dictionary<AssetID, AssetID> glblToLcl = crawler.references;

            List<Type_0D> types = new List<Type_0D>();
            List<string> typeNames = new List<string>();

            Dictionary<string, AssetsFileInstance> fileToInst = am.files.ToDictionary(d => d.path);
            int j = 0;
            foreach (KeyValuePair<AssetID, AssetID> id in glblToLcl)
            {
                if (j % 100 == 0)
                    EditorUtility.DisplayProgressBar("HKEdit", "Reordering path ids... (step 2/3)", (float)j / glblToLcl.Count);
                AssetsFileInstance depInst = fileToInst[id.Key.fileName];
                AssetFileInfoEx depInf = depInst.table.GetAssetInfo(id.Key.pathId);

                ClassDatabaseType clType = AssetHelper.FindAssetClassByID(cldb, depInf.curFileType);
                string clName = clType.name.GetString(cldb);
                if (!typeNames.Contains(clName))
                {
                    Type_0D type0d = C2T5.Cldb2TypeTree(cldb, clName);
                    type0d.classId = (int)depInf.curFileType;
                    types.Add(type0d);
                    typeNames.Add(clName);
                }

                crawler.ReplaceReferences(depInst, depInf, id.Value.pathId);
                j++;
            }

            EditorUtility.DisplayProgressBar("HKEdit", "Saving scene... (step 3/3)", 1f);

            types.Add(CreateEditDifferTypeTree(cldb));
            types.Add(CreateTk2dEmuTypeTree(cldb));

            List<Type_0D> assetTypes = new List<Type_0D>()
            {
                C2T5.Cldb2TypeTree(cldb, 0x1c),
                C2T5.Cldb2TypeTree(cldb, 0x30),
                C2T5.Cldb2TypeTree(cldb, 0x53),
                //new
                /*C2T5.Cldb2TypeTree(cldb, 0x5b),
                C2T5.Cldb2TypeTree(cldb, 0x4a),*/
                //C2T5.Cldb2TypeTree(cldb, 0x28f3fdef)
            };

            string origFileName = Path.GetFileNameWithoutExtension(inst.path);

            string sceneGuid = CreateMD5(origFileName);

            string ExportedScenes = Path.Combine("Assets", "ExportedScenes");
            //circumvents "!BeginsWithCaseInsensitive(file.pathName, AssetDatabase::kAssetsPathWithSlash)" assertion
            string ExportedScenesData = "ExportedScenesData";

            CreateMetaFile(sceneGuid, Path.Combine(ExportedScenes, origFileName + ".unity.meta"));

            AssetsFile sceneFile = new AssetsFile(new AssetsFileReader(new MemoryStream(BundleCreator.CreateBlankAssets(ver, types))));
            AssetsFile assetFile = new AssetsFile(new AssetsFileReader(new MemoryStream(BundleCreator.CreateBlankAssets(ver, assetTypes))));

            byte[] sceneFileData;
            using (MemoryStream ms = new MemoryStream())
            using (AssetsFileWriter w = new AssetsFileWriter(ms))
            {
                w.bigEndian = false;
                //unity editor won't load whole assets files by guid, so we have to use hardcoded paths
                sceneFile.dependencies.dependencies = new List<AssetsFileDependency>()
                {
                    CreateDependency(ExportedScenesData + "/" + origFileName + "-data.assets"),
                    CreateScriptDependency(Constants.editDifferMsEditorScriptHash, Constants.editDifferLsEditorScriptHash),
                    CreateScriptDependency(Constants.tk2dEmuMsEditorScriptHash, Constants.tk2dEmuLsEditorScriptHash),
                };
                sceneFile.dependencies.dependencyCount = 3;
                sceneFile.preloadTable.items = new List<AssetPPtr>()
                {
                    new AssetPPtr(2, 11500000),
                    new AssetPPtr(3, 11500000)
                };
                sceneFile.preloadTable.len = 2;
                sceneFile.Write(w, 0, crawler.sceneReplacers.Concat(crawler.sceneMonoReplacers).ToList(), 0);
                sceneFileData = ms.ToArray();
            }
            byte[] assetFileData;
            using (MemoryStream ms = new MemoryStream())
            using (AssetsFileWriter w = new AssetsFileWriter(ms))
            {
                w.bigEndian = false;
                assetFile.dependencies.dependencies = new List<AssetsFileDependency>()
                {
                    //CreateDependency("Assets/" + origFileName + ".unity"),
                };
                assetFile.dependencies.dependencyCount = 0;
                assetFile.Write(w, 0, crawler.assetReplacers.ToList(), 0);
                assetFileData = ms.ToArray();
            }

            File.WriteAllBytes(Path.Combine(ExportedScenes, origFileName + ".unity"), sceneFileData);
            File.WriteAllBytes(Path.Combine(ExportedScenesData, origFileName + "-data.assets"), assetFileData);
            File.WriteAllText(Path.Combine(ExportedScenesData, origFileName + ".metadata"), CreateHKWEMetaFile(am, inst));

            EditorUtility.ClearProgressBar();
        }

        private static string CreateMD5(string input)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        private static void CreateMetaFile(string guid, string path)
        {
            File.WriteAllText(path, @"fileFormatVersion: 2
guid: " + guid + @"
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
");
        }

        private static string CreateHKWEMetaFile(AssetsManager am, AssetsFileInstance inst)
        {
            int levelIndex = GetLevelIndex(inst);
            string levelName = GetLevelName(am, levelIndex);
            HKWEMeta meta = new HKWEMeta()
            {
                hkweVersion = hkweVersion,
                levelName = levelName,
                levelIndex = levelIndex,
                diffPath = inst.path
            };
            return EditorJsonUtility.ToJson(meta);
        }

        private static int GetLevelIndex(AssetsFileInstance inst)
        {
            string fileName = Path.GetFileNameWithoutExtension(inst.path);
            string fileNumber = Regex.Replace(fileName, "[^0-9.]", "");
            return int.Parse(fileNumber);
        }

        private static string GetLevelName(AssetsManager am, int index)
        {
            AssetsFileInstance inst = am.files.FirstOrDefault(f => f.name == "globalgamemanagers");
            if (inst == null)
                return string.Empty;
            AssetFileInfoEx bsInf = inst.table.GetAssetsOfType(0x8D).FirstOrDefault(); //BuildSettings
            if (bsInf == null)
                return string.Empty;
            AssetTypeValueField bsBaseField = am.GetATI(inst.file, bsInf).GetBaseField();
            AssetTypeValueField scenes = bsBaseField.Get("scenes").Get("Array");
            return scenes[index].GetValue().AsString();
        }

        private static AssetsFileDependency CreateDependency(string path)
        {
            return new AssetsFileDependency()
            {
                guid = new AssetsFileDependency.GUID128()
                {
                    mostSignificant = 0,
                    leastSignificant = 0
                },
                type = 0,
                assetPath = path,
                bufferedPath = "",
            };
        }

        private static AssetsFileDependency CreateScriptDependency(long mostSignificant, long leastSignificant)
        {
            return new AssetsFileDependency()
            {
                guid = new AssetsFileDependency.GUID128()
                {
                    mostSignificant = mostSignificant,
                    leastSignificant = leastSignificant
                },
                type = 3,
                assetPath = "",
                bufferedPath = "",
            };
        }

        public static AssetTypeValueField GetBaseField(AssetsManager am, AssetsFile file, AssetFileInfoEx info)
        {
            AssetTypeInstance ati = am.GetATI(file, info);
            return ati.GetBaseField();
        }

        /////////////////////////////////////////////////////////

        private static Type_0D CreateEditDifferTypeTree(ClassDatabaseFile cldb)
        {
            Type_0D type = C2T5.Cldb2TypeTree(cldb, 0x72);
            type.scriptIndex = 0x0000;
            type.scriptHash1 = Constants.editDifferScriptNEHash[0];
            type.scriptHash2 = Constants.editDifferScriptNEHash[1];
            type.scriptHash3 = Constants.editDifferScriptNEHash[2];
            type.scriptHash4 = Constants.editDifferScriptNEHash[3];

            TypeTreeEditor editor = new TypeTreeEditor(type);
            TypeField_0D baseField = type.typeFieldsEx[0];

            editor.AddField(baseField, editor.CreateTypeField("unsigned int", "fileId", 1, 4, 0, false));
            editor.AddField(baseField, editor.CreateTypeField("UInt64", "pathId", 1, 8, 0, false));
            editor.AddField(baseField, editor.CreateTypeField("UInt64", "origPathId", 1, 8, 0, false));
            editor.AddField(baseField, editor.CreateTypeField("UInt8", "newAsset", 1, 1, 0, true));
            uint componentIds = editor.AddField(baseField, editor.CreateTypeField("vector", "componentIds", 1, -1, 0, false, false, Flags.AnyChildUsesAlignBytesFlag));
            uint Array = editor.AddField(editor.type.typeFieldsEx[componentIds], editor.CreateTypeField("Array", "Array", 2, -1, 0, true, true));
            editor.AddField(editor.type.typeFieldsEx[Array], editor.CreateTypeField("int", "size", 3, 4, 0, false));
            editor.AddField(editor.type.typeFieldsEx[Array], editor.CreateTypeField("SInt64", "data", 3, 8, 0, false));
            editor.AddField(baseField, editor.CreateTypeField("int", "instanceId", 1, 4, 0, false));

            type = editor.SaveType();
            return type;
        }

        private static Type_0D CreateTk2dEmuTypeTree(ClassDatabaseFile cldb)
        {
            Type_0D type = C2T5.Cldb2TypeTree(cldb, 0x72);
            type.scriptIndex = 0x0001;
            type.scriptHash1 = Constants.tk2dEmuScriptNEHash[0];
            type.scriptHash2 = Constants.tk2dEmuScriptNEHash[1];
            type.scriptHash3 = Constants.tk2dEmuScriptNEHash[2];
            type.scriptHash4 = Constants.tk2dEmuScriptNEHash[3];

            TypeTreeEditor editor = new TypeTreeEditor(type);
            TypeField_0D baseField = type.typeFieldsEx[0];

            uint vertices = editor.AddField(baseField, editor.CreateTypeField("vector", "vertices", 1, -1, 0, false, false, Flags.AnyChildUsesAlignBytesFlag));
            uint Array = editor.AddField(editor.type.typeFieldsEx[vertices], editor.CreateTypeField("Array", "Array", 2, -1, 0, true, true));
            editor.AddField(editor.type.typeFieldsEx[Array], editor.CreateTypeField("int", "size", 3, 4, 0, false));
            uint data = editor.AddField(editor.type.typeFieldsEx[Array], editor.CreateTypeField("Vector3", "data", 3, -1, 0, false));
            editor.AddField(editor.type.typeFieldsEx[data], editor.CreateTypeField("float", "x", 4, 4, 0, false));
            editor.AddField(editor.type.typeFieldsEx[data], editor.CreateTypeField("float", "y", 4, 4, 0, false));
            editor.AddField(editor.type.typeFieldsEx[data], editor.CreateTypeField("float", "z", 4, 4, 0, false));
            uint uvs = editor.AddField(baseField, editor.CreateTypeField("vector", "uvs", 1, -1, 0, false, false, Flags.AnyChildUsesAlignBytesFlag));
            Array = editor.AddField(editor.type.typeFieldsEx[uvs], editor.CreateTypeField("Array", "Array", 2, -1, 0, true, true));
            editor.AddField(editor.type.typeFieldsEx[Array], editor.CreateTypeField("int", "size", 3, 4, 0, false));
            data = editor.AddField(editor.type.typeFieldsEx[Array], editor.CreateTypeField("Vector2", "data", 3, -1, 0, false));
            editor.AddField(editor.type.typeFieldsEx[data], editor.CreateTypeField("float", "x", 4, 4, 0, false));
            editor.AddField(editor.type.typeFieldsEx[data], editor.CreateTypeField("float", "y", 4, 4, 0, false));
            uint indices = editor.AddField(baseField, editor.CreateTypeField("vector", "indices", 1, -1, 0, false, false, Flags.AnyChildUsesAlignBytesFlag));
            Array = editor.AddField(editor.type.typeFieldsEx[indices], editor.CreateTypeField("Array", "Array", 2, -1, 0, true, true));
            editor.AddField(editor.type.typeFieldsEx[Array], editor.CreateTypeField("int", "size", 3, 4, 0, false));
            editor.AddField(editor.type.typeFieldsEx[Array], editor.CreateTypeField("int", "data", 3, 4, 0, false));

            type = editor.SaveType();
            return type;
        }
    }
}
