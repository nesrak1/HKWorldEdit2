/*using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace Assets.Bundler
{
    public class LoaderOld
    {
        const string ver = "2017.4.10f1";
        const int hkweVersion = 1;
        public static byte[] CreateBundleFromLevel(string levelPath)
        {
            AssetsManager am = new AssetsManager();
            EditorUtility.DisplayProgressBar("HKEdit", "Loading class database...", 0f);
            am.LoadClassPackage("cldb.dat");
            am.useTemplateFieldCache = true;
            EditorUtility.DisplayProgressBar("HKEdit", "Reading assets files...", 0.5f);
            return CreateBundleFromLevel(am, am.LoadAssetsFile(levelPath, true));
        }
        
        public static byte[] CreateBundleFromLevel(AssetsManager am, AssetsFileInstance inst)
        {
            EditorUtility.DisplayProgressBar("HKEdit", "Reading Files...", 0f);
            am.UpdateDependencies();

            //quicker asset id lookup
            for (int i = 0; i < am.files.Count; i++)
            {
                AssetsFileInstance afi = am.files[i];
                if (i % 100 == 0)
                    EditorUtility.DisplayProgressBar("HKEdit", "Generating QLTs", (float)i / am.files.Count);
                afi.table.GenerateQuickLookupTree();
            }

            //setup
            AssetsFile file = inst.file;
            AssetsFileTable table = inst.table;

            string folderName = Path.GetDirectoryName(inst.path);

            List<AssetFileInfoEx> infos = table.pAssetFileInfo.ToList();

            List<string> fileNames = new List<string>();
            Dictionary<AssetID, byte[]> deps = new Dictionary<AssetID, byte[]>();

            fileNames.Add(inst.name);
            
            //add own ids to list so we don't reread them
            foreach (AssetFileInfoEx info in infos)
            {
                if (info.curFileType != 1)
                    continue;
                AssetID id = new AssetID(inst.name, (long)info.index);
                deps.Add(id, null);
            }
            
            //look through each field in each asset in this file
            for (int i = 0; i < infos.Count; i++)
            {
                AssetFileInfoEx info = infos[i];
                if (info.curFileType != 1)
                    continue;
                if (i % 100 == 0)
                    EditorUtility.DisplayProgressBar("HKEdit", "Crawling PPtrs", (float)i / infos.Count);
                ReferenceCrawler.CrawlPPtrs(am, inst, info.index, fileNames, deps);
            }

            //add typetree data for dependencies
            long curId = 1;
            List<Type_0D> types = new List<Type_0D>();
            List<string> typeNames = new List<string>();
            List<AssetsReplacer> assets = new List<AssetsReplacer>();
            Dictionary<string, AssetsFileInstance> insts = new Dictionary<string, AssetsFileInstance>();
            //asset id is our custom id that uses filename/pathid instead of fileid/pathid
            //asset id to path id
            Dictionary<AssetID, long> aidToPid = new Dictionary<AssetID, long>();
            //script id to mono id
            Dictionary<ScriptID, ushort> sidToMid = new Dictionary<ScriptID, ushort>();
            uint lastId = 0;
            ushort nextMonoId = 0;
            int depCount = 0;
            foreach (KeyValuePair<AssetID, byte[]> dep in deps)
            {
                if (depCount % 100 == 0)
                    EditorUtility.DisplayProgressBar("HKEdit", "Fixing Dependencies", (float)depCount / deps.Keys.Count);
                AssetID id = dep.Key;
                byte[] assetData = dep.Value;
                AssetsFileInstance afInst = null;
                if (insts.ContainsKey(id.fileName))
                    afInst = insts[id.fileName];
                else
                    afInst = am.files.First(f => f.name == id.fileName);
                if (afInst == null)
                    continue;
                AssetFileInfoEx inf = afInst.table.getAssetInfo((ulong)id.pathId);
                if (lastId != inf.curFileType)
                {
                    lastId = inf.curFileType;
                }

                ClassDatabaseType clType = AssetHelper.FindAssetClassByID(am.classFile, inf.curFileType);
                string clName = clType.name.GetString(am.classFile);
                ushort monoIndex = 0xFFFF;
                if (inf.curFileType != 0x72)
                {
                    if (!typeNames.Contains(clName))
                    {
                        Type_0D type0d = C2T5.Cldb2TypeTree(am.classFile, clName);
                        type0d.classId = (int)inf.curFileType; //?
                        types.Add(type0d);
                        typeNames.Add(clName);
                    }
                }
                else
                {
                    //unused for now
                    AssetTypeValueField baseField = am.GetATI(afInst.file, inf).GetBaseField();
                    AssetTypeValueField m_Script = baseField.Get("m_Script");
                    AssetTypeValueField scriptBaseField = am.GetExtAsset(afInst, m_Script).instance.GetBaseField();
                    string m_ClassName = scriptBaseField.Get("m_ClassName").GetValue().AsString();
                    string m_Namespace = scriptBaseField.Get("m_Namespace").GetValue().AsString();
                    string m_AssemblyName = scriptBaseField.Get("m_AssemblyName").GetValue().AsString();
                    ScriptID sid = new ScriptID(m_ClassName, m_Namespace, m_AssemblyName);
                    if (!sidToMid.ContainsKey(sid))
                    {
                        MonoClass mc = new MonoClass();
                        mc.Read(m_ClassName, Path.Combine(Path.Combine(Path.GetDirectoryName(inst.path), "Managed"), m_AssemblyName), afInst.file.header.format);

                        Type_0D type0d = C2T5.Cldb2TypeTree(am.classFile, clName);
                        TemplateFieldToType0D typeConverter = new TemplateFieldToType0D();

                        TypeField_0D[] monoFields = typeConverter.TemplateToTypeField(mc.children, type0d);

                        type0d.pStringTable = typeConverter.stringTable;
                        type0d.stringTableLen = (uint)type0d.pStringTable.Length;
                        type0d.scriptIndex = nextMonoId;
                        type0d.pTypeFieldsEx = type0d.pTypeFieldsEx.Concat(monoFields).ToArray();
                        type0d.typeFieldsExCount = (uint)type0d.pTypeFieldsEx.Length;
                        
                        types.Add(type0d);
                        sidToMid.Add(sid, nextMonoId);
                        nextMonoId++;
                    }
                    monoIndex = sidToMid[sid];
                }
                aidToPid.Add(id, curId);
                AssetsReplacer rep = new AssetsReplacerFromMemory(0, (ulong)curId, (int)inf.curFileType, monoIndex, assetData);
                assets.Add(rep);
                curId++;
                depCount++;
            }

            byte[] blankData = BundleCreator.CreateBlankAssets(ver, types);
            AssetsFile blankFile = new AssetsFile(new AssetsFileReader(new MemoryStream(blankData)));

            EditorUtility.DisplayProgressBar("HKEdit", "Writing first file...", 0f);
            byte[] data = null;
            using (MemoryStream ms = new MemoryStream())
            using (AssetsFileWriter writer = new AssetsFileWriter(ms))
            {
                blankFile.Write(writer, 0, assets.ToArray(), 0);
                data = ms.ToArray();
            }

            //File.WriteAllBytes("debug.assets", data);

            MemoryStream msn = new MemoryStream(data);
            AssetsManager amn = new AssetsManager();
            
            amn.classFile = am.classFile;
            AssetsFileInstance instn = amn.LoadAssetsFile(msn, ((FileStream)inst.file.reader.BaseStream).Name, false);

            instn.table.GenerateQuickLookupTree();

            deps.Clear();

            List<AssetsReplacer> assetsn = new List<AssetsReplacer>();

            //gameobject id to mono id
            Dictionary<long, long> gidToMid = new Dictionary<long, long>();
            long nextBehaviourId = (long)instn.table.pAssetFileInfo.Max(i => i.index) + 1;

            CreateEditDifferTypeTree(amn.classFile, instn);
            CreateSceneMetadataTypeTree(amn.classFile, instn);

            Random rand = new Random();
            rand.Next();
            foreach (KeyValuePair<AssetID, long> kvp in aidToPid)
            {
                AssetFileInfoEx inf = instn.table.getAssetInfo((ulong)kvp.Value);
                if (inf.curFileType == 0x01)
                {
                    gidToMid.Add(kvp.Value, nextBehaviourId);
                    assetsn.Add(CreateEditDifferMonoBehaviour(kvp.Value, kvp.Key, nextBehaviourId++, rand));
                }
            }

            for (int i = 0; i < instn.table.pAssetFileInfo.Length; i++)
            {
                AssetFileInfoEx inf = instn.table.pAssetFileInfo[i];
                if (i % 100 == 0)
                    EditorUtility.DisplayProgressBar("HKEdit", "Crawling PPtrs", (float)i / instn.table.pAssetFileInfo.Length);
                ReferenceCrawler.CrawlReplacePPtrs(amn, instn, inf.index, fileNames, deps, aidToPid, gidToMid);
            }

            //add monoscript assets to preload table to make unity happy
            List<AssetPPtr> preloadPptrs = new List<AssetPPtr>();
            preloadPptrs.Add(new AssetPPtr(1, 11500000));
            preloadPptrs.Add(new AssetPPtr(2, 11500000));
            foreach (KeyValuePair<AssetID, byte[]> dep in deps)
            {
                AssetID id = dep.Key;
                byte[] assetData = dep.Value;
                long pid = id.pathId;

                if (pid == 1)
                    assetData = AddMetadataMonobehaviour(assetData, nextBehaviourId);

                AssetFileInfoEx inf = instn.table.getAssetInfo((ulong)pid);
                ushort monoId = instn.file.typeTree.pTypes_Unity5[inf.curFileTypeOrIndex].scriptIndex;
                assetsn.Add(new AssetsReplacerFromMemory(0, (ulong)pid, (int)inf.curFileType, monoId, assetData));
                if (inf.curFileType == 0x73)
                    preloadPptrs.Add(new AssetPPtr(0, (ulong)pid));
            }

            List<long> usedIds = assetsn.Select(a => (long)a.GetPathID()).ToList();
            //will break if no gameobjects but I don't really care at this point
            assetsn.Add(CreateSceneMetadataMonoBehaviour(1, nextBehaviourId++, inst.name, usedIds));

            instn.file.preloadTable.items = preloadPptrs.ToArray();
            instn.file.preloadTable.len = (uint)instn.file.preloadTable.items.Length;

            //add dependencies to monobehaviours
            List<AssetsFileDependency> fileDeps = new List<AssetsFileDependency>();
            AddScriptDependency(fileDeps, Constants.editDifferMsEditorScriptHash, Constants.editDifferLsEditorScriptHash);
            AddScriptDependency(fileDeps, Constants.sceneMetadataMsEditorScriptHash, Constants.sceneMetadataLsEditorScriptHash);

            instn.file.dependencies.pDependencies = fileDeps.ToArray();
            instn.file.dependencies.dependencyCount = (uint)fileDeps.Count;

            EditorUtility.DisplayProgressBar("HKEdit", "Writing second file...", 0f);
            byte[] datan = null;
            using (MemoryStream ms = new MemoryStream())
            using (AssetsFileWriter writer = new AssetsFileWriter(ms))
            {
                instn.file.Write(writer, 0, assetsn.ToArray(), 0);
                datan = ms.ToArray();
            }

            EditorUtility.ClearProgressBar();

            return datan;
        }

        public static AssetTypeValueField GetBaseField(AssetsManager am, AssetsFile file, AssetFileInfoEx info)
        {
            AssetTypeInstance ati = am.GetATI(file, info);
            return ati.GetBaseField();
        }

        private static byte[] GetBundleData(string bunPath, int index)
        {
            AssetsFileReader r = new AssetsFileReader(File.Open(bunPath, FileMode.Open, FileAccess.Read, FileShare.Read));
            AssetsBundleFile bun = new AssetsBundleFile();
            bun.Read(r, true);

            //if the bundle doesn't have this section return empty
            if (index >= bun.bundleInf6.dirInf.Length)
                return new byte[0];

            AssetsBundleDirectoryInfo06 dirInf = bun.bundleInf6.dirInf[index];
            int start = (int)(bun.bundleHeader6.GetFileDataOffset() + dirInf.offset);
            int length = (int)dirInf.decompressedSize;
            byte[] data;
            r.BaseStream.Position = start;
            data = r.ReadBytes(length);
            return data;
        }

        private static AssetFileInfoEx FindGameObject(AssetsManager am, AssetsFileInstance inst, string name)
        {
            foreach (AssetFileInfoEx info in inst.table.pAssetFileInfo)
            {
                if (info.curFileType == 0x01)
                {
                    ClassDatabaseType type = AssetHelper.FindAssetClassByID(am.classFile, info.curFileType);
                    string infoName = AssetHelper.GetAssetNameFast(inst.file, am.classFile, info);
                    if (infoName == name)
                    {
                        return info;
                    }
                }
            }
            return null;
        }

        private static void AddScriptDependency(List<AssetsFileDependency> fileDeps, long mostSignificant, long leastSignificant)
        {
            fileDeps.Add(new AssetsFileDependency()
            {
                guid = new AssetsFileDependency.GUID128()
                {
                    mostSignificant = mostSignificant,
                    leastSignificant = leastSignificant
                },
                type = 3,
                assetPath = "",
                bufferedPath = "",
            });
        }

        private static byte[] AddMetadataMonobehaviour(byte[] data, long behaviourId)
        {
            //it seems unity is so broken that after something other than
            //gameobjects are added to the asset list, you can't add any
            //monobehaviour components to gameobjects after it or it crashes
            //anyway, since I'm stuck on this one and I can't really push
            //to the beginning of the list, I'll just put the info onto
            //the first gameobject in the scene

            using (MemoryStream fms = new MemoryStream(data))
            using (AssetsFileReader fr = new AssetsFileReader(fms))
            {
                fr.bigEndian = false;
                int componentSize = fr.ReadInt32();
                List<AssetPPtr> pptrs = new List<AssetPPtr>();
                for (int i = 0; i < componentSize; i++)
                {
                    int fileId = fr.ReadInt32();
                    long pathId = fr.ReadInt64();

                    //this gets rid of assets that have no reference
                    if (!(fileId == 0 && pathId == 0))
                    {
                        pptrs.Add(new AssetPPtr((uint)fileId, (ulong)pathId));
                    }
                }
                //add reference to Metadata mb
                pptrs.Add(new AssetPPtr(0, (ulong)behaviourId));

                int assetLengthMinusCP = (int)(data.Length - 4 - (componentSize * 12));

                using (MemoryStream ms = new MemoryStream())
                using (AssetsFileWriter w = new AssetsFileWriter(ms))
                {
                    w.bigEndian = false;
                    w.Write(pptrs.Count);
                    foreach (AssetPPtr pptr in pptrs)
                    {
                        w.Write(pptr.fileID);
                        w.Write(pptr.pathID);
                    }
                    w.Write(fr.ReadBytes(assetLengthMinusCP));
                    return ms.ToArray();
                }
            }
        }

        /////////////////////////////////////////////////////////
        // nope nothing to see here
        /////////////////////////////////////////////////////////

        private static AssetsReplacer CreateEditDifferMonoBehaviour(long goPid, AssetID origGoPptr, long id, Random rand)
        {
            byte[] data;
            using (MemoryStream ms = new MemoryStream())
            using (AssetsFileWriter w = new AssetsFileWriter(ms))
            {
                w.bigEndian = false;
                w.Write(0);
                w.Write(goPid);
                w.Write(1);
                w.Write(1);
                w.Write((long)11500000);
                w.WriteCountStringInt32("");
                w.Align();

                w.Write(0);
                w.Write(origGoPptr.pathId);
                w.Write(origGoPptr.pathId);
                w.Write(0);
                w.Write(rand.Next());
                data = ms.ToArray();
            }
            return new AssetsReplacerFromMemory(0, (ulong)id, 0x72, 0x0000, data);
        }

        private static AssetsReplacer CreateSceneMetadataMonoBehaviour(long goPid, long id, string sceneName, List<long> usedIds)
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

                w.WriteCountStringInt32(sceneName);
                w.Align();
                w.Write(usedIds.Count);
                foreach (long usedId in usedIds)
                {
                    w.Write(usedId);
                }
                w.Align();
                w.Write(hkweVersion);
                data = ms.ToArray();
            }
            return new AssetsReplacerFromMemory(0, (ulong)id, 0x72, 0x0001, data);
        }

        private static AssetsReplacer CreateSceneMetadataGameObject(long tfPid, long mbPid, long id)
        {
            byte[] data;
            using (MemoryStream ms = new MemoryStream())
            using (AssetsFileWriter w = new AssetsFileWriter(ms))
            {
                w.bigEndian = false;
                w.Write(2);
                w.Write(0);
                w.Write(tfPid);
                w.Write(0);
                w.Write(mbPid);
                w.Write(0);
                w.Align();
                w.WriteCountStringInt32("<//Hkwe Scene Metadata//>");
                w.Align();
                w.Write((ushort)0);
                w.Write((byte)1);
                data = ms.ToArray();
            }
            return new AssetsReplacerFromMemory(0, (ulong)id, 0x01, 0xFFFF, data);
        }

        private static AssetsReplacer CreateSceneMetadataTransform(long goPid, long id)
        {
            byte[] data;
            using (MemoryStream ms = new MemoryStream())
            using (AssetsFileWriter w = new AssetsFileWriter(ms))
            {
                w.bigEndian = false;
                w.Write(0);
                w.Write(goPid);
                w.Write(0f);
                w.Write(0f);
                w.Write(0f);
                w.Write(1f);
                w.Write(0f);
                w.Write(0f);
                w.Write(0f);
                w.Write(1f);
                w.Write(1f);
                w.Write(1f);
                w.Write(0);
                w.Write(0);
                w.Write((long)0);
                data = ms.ToArray();
            }
            return new AssetsReplacerFromMemory(0, (ulong)id, 0x04, 0xFFFF, data);
        }

        /////////////////////////////////////////////////////////

        private static void CreateEditDifferTypeTree(ClassDatabaseFile cldb, AssetsFileInstance inst)
        {
            Type_0D type = C2T5.Cldb2TypeTree(cldb, 0x72);
            type.scriptIndex = 0x0000;
            type.unknown1 = Constants.editDifferScriptNEHash[0];
            type.unknown2 = Constants.editDifferScriptNEHash[1];
            type.unknown3 = Constants.editDifferScriptNEHash[2];
            type.unknown4 = Constants.editDifferScriptNEHash[3];

            TypeTreeEditor editor = new TypeTreeEditor(type);
            TypeField_0D baseField = type.pTypeFieldsEx[0];

            editor.AddField(baseField, editor.CreateTypeField("unsigned int", "fileId", 1, 4, 0, false));
            editor.AddField(baseField, editor.CreateTypeField("UInt64", "pathId", 1, 8, 0, false));
            editor.AddField(baseField, editor.CreateTypeField("UInt64", "origPathId", 1, 8, 0, false));
            editor.AddField(baseField, editor.CreateTypeField("UInt8", "newAsset", 1, 1, 0, true));
            editor.AddField(baseField, editor.CreateTypeField("int", "instanceId", 1, 4, 0, false));
            type = editor.SaveType();
            
            inst.file.typeTree.pTypes_Unity5 = inst.file.typeTree.pTypes_Unity5.Concat(new Type_0D[] { type }).ToArray();
            inst.file.typeTree.fieldCount++;
        }

        private static void CreateSceneMetadataTypeTree(ClassDatabaseFile cldb, AssetsFileInstance inst)
        {
            Type_0D type = C2T5.Cldb2TypeTree(cldb, 0x72);
            type.scriptIndex = 0x0001;
            type.unknown1 = Constants.sceneMetadataScriptNEHash[0];
            type.unknown2 = Constants.sceneMetadataScriptNEHash[1];
            type.unknown3 = Constants.sceneMetadataScriptNEHash[2];
            type.unknown4 = Constants.sceneMetadataScriptNEHash[3];

            TypeTreeEditor editor = new TypeTreeEditor(type);
            TypeField_0D baseField = type.pTypeFieldsEx[0];

            uint sceneName = editor.AddField(baseField, editor.CreateTypeField("string", "sceneName", 1, uint.MaxValue, 0, false, false, Flags.AnyChildUsesAlignBytesFlag));
            uint Array = editor.AddField(editor.type.pTypeFieldsEx[sceneName], editor.CreateTypeField("Array", "Array", 2, uint.MaxValue, 0, true, true, Flags.HideInEditorMask));
            editor.AddField(editor.type.pTypeFieldsEx[Array], editor.CreateTypeField("int", "size", 3, 4, 0, false, false, Flags.HideInEditorMask));
            editor.AddField(editor.type.pTypeFieldsEx[Array], editor.CreateTypeField("char", "data", 3, 1, 0, false, false, Flags.HideInEditorMask));
            uint usedIds = editor.AddField(baseField, editor.CreateTypeField("vector", "usedIds", 1, uint.MaxValue, 0, false, false, Flags.AnyChildUsesAlignBytesFlag));
            uint Array2 = editor.AddField(editor.type.pTypeFieldsEx[usedIds], editor.CreateTypeField("Array", "Array", 2, uint.MaxValue, 0, true, true));
            editor.AddField(editor.type.pTypeFieldsEx[Array2], editor.CreateTypeField("int", "size", 3, 4, 0, false));
            editor.AddField(editor.type.pTypeFieldsEx[Array2], editor.CreateTypeField("SInt64", "data", 3, 8, 0, false));
            editor.AddField(baseField, editor.CreateTypeField("int", "hkweVersion", 1, 4, 0, false));
            type = editor.SaveType();
            
            inst.file.typeTree.pTypes_Unity5 = inst.file.typeTree.pTypes_Unity5.Concat(new Type_0D[] { type }).ToArray();
            inst.file.typeTree.fieldCount++;
        }
    }

    public class AssetID
    {
        public string fileName;
        public long pathId;
        public AssetID(string fileName, long pathId)
        {
            this.fileName = fileName;
            this.pathId = pathId;
        }
        public override bool Equals(object obj)
        {
            if (obj.GetType() != typeof(AssetID))
                return false;
            AssetID assetID = obj as AssetID;
            if (fileName == assetID.fileName &&
                pathId == assetID.pathId)
                return true;
            return false;
        }
        public override int GetHashCode()
        {
            int hash = 17;

            hash = hash * 23 + fileName.GetHashCode();
            hash = hash * 23 + pathId.GetHashCode();
            return hash;
        }
    }

    public class ScriptID
    {
        public string scriptName;
        public string scriptNamespace;
        public string scriptFileName;
        public ScriptID(string scriptName, string scriptNamespace, string scriptFileName)
        {
            this.scriptName = scriptName;
            this.scriptNamespace = scriptNamespace;
            this.scriptFileName = scriptFileName;
        }
        public override bool Equals(object obj)
        {
            if (obj.GetType() != typeof(ScriptID))
                return false;
            ScriptID scriptID = obj as ScriptID;
            if (scriptName == scriptID.scriptName &&
                scriptNamespace == scriptID.scriptNamespace &&
                scriptFileName == scriptID.scriptFileName)
                return true;
            return false;
        }
        public override int GetHashCode()
        {
            int hash = 17;

            hash = hash * 23 + scriptName.GetHashCode();
            hash = hash * 23 + scriptNamespace.GetHashCode();
            hash = hash * 23 + scriptFileName.GetHashCode();
            return hash;
        }
    }
}
*/