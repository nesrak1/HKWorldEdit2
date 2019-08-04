using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Assets.Bundler
{
    public class ReferenceCrawler
    {
        //we don't touch this file
        //if you want to improve this with a pr that would be great
        public static void CrawlPPtrs(AssetsManager am, AssetsFileInstance inst, ulong startingId, List<string> fileNames, Dictionary<AssetID, byte[]> depIds)
        {
            AssetFileInfoEx info = inst.table.getAssetInfo(startingId);
            AssetTypeValueField baseField;
            if (info.curFileType == 0x72)
                baseField = am.GetMonoBaseFieldCached(inst, info, Path.Combine(Path.GetDirectoryName(inst.path), "Managed"));
            else
                baseField = am.GetATI(inst.file, info).GetBaseField();
            RecurseType(am, inst, info, baseField, fileNames, depIds, 0);
        }
        public static void CrawlReplacePPtrs(AssetsManager am, AssetsFileInstance inst, ulong startingId, List<string> fileNames, Dictionary<AssetID, byte[]> depIds, Dictionary<AssetID, long> aidToPid, Dictionary<long, long> gidToMid)
        {
            AssetFileInfoEx info = inst.table.getAssetInfo(startingId);
            AssetTypeValueField baseField;
            if (info.curFileType == 0x72)
                baseField = am.GetMonoBaseFieldCached(inst, info, Path.Combine(Path.GetDirectoryName(inst.path), "Managed"), fileNames, aidToPid);
            else
                baseField = am.GetATI(inst.file, info).GetBaseField();
            RecurseTypeReplace(am, inst, info, baseField, fileNames, depIds, aidToPid, gidToMid, 0);
        }
        //thats a nice function, be a shame if you ran out of memory
        private static void RecurseType(AssetsManager am, AssetsFileInstance inst, AssetFileInfoEx info, AssetTypeValueField field, List<string> fileNames, Dictionary<AssetID, byte[]> ids, int depth)
        {
            string p = new string(' ', depth);
            foreach (AssetTypeValueField child in field.pChildren)
            {
                //Console.WriteLine(p + child.templateField.type + " " + child.templateField.name);
                if (!child.templateField.hasValue)
                {
                    if (child == null)
                        return;
                    string typeName = child.templateField.type;
                    if (typeName.StartsWith("PPtr<") && typeName.EndsWith(">") && child.childrenCount == 2)
                    {
                        int fileId = child.Get("m_FileID").GetValue().AsInt();
                        long pathId = child.Get("m_PathID").GetValue().AsInt64();

                        if (pathId == 0)
                            continue;

                        AssetsFileInstance depInst = null;
                        if (fileId == 0)
                        {
                            depInst = inst;
                        }
                        else
                        {
                            if (inst.dependencies.Count > 0 && inst.dependencies[0] == null)
                                Console.WriteLine("dependency null for " + inst.name);
                            depInst = inst.dependencies[fileId - 1];
                        }

                        string depName = depInst.name;
                        if (!fileNames.Contains(depName))
                        {
                            fileNames.Add(depName);
                        }

                        AssetID id = new AssetID(depInst.name, pathId);
                        if (!ids.ContainsKey(id))
                        {
                            AssetsManager.AssetExternal depExt = am.GetExtAsset(inst, child);
                            AssetFileInfoEx depInfo = depExt.info;

                            if (depInfo.curFileType == 1 ||
                                depInfo.curFileType == 4 ||
                                depInfo.curFileType == 21 ||
                                depInfo.curFileType == 23 ||
                                depInfo.curFileType == 28 ||
                                depInfo.curFileType == 33 ||
                                depInfo.curFileType == 43 ||
                                depInfo.curFileType == 48 ||
                                depInfo.curFileType == 212 ||
                                depInfo.curFileType == 213)
                            {
                                ids.Add(id, null);
                                AssetTypeValueField depBaseField;
                                if (depInfo.curFileType != 0x72)
                                    depBaseField = depExt.instance.GetBaseField();
                                else
                                    depBaseField = am.GetMonoBaseFieldCached(depExt.file, depInfo, Path.Combine(Path.GetDirectoryName(inst.path), "Managed"));

                                RecurseType(am, depInst, depInfo, depBaseField, fileNames, ids, 0);
                            }
                        }
                        //make fileId negative to mark for replacement
                        //we do the changes here since we're already iterating over each field
                        child.Get("m_FileID").GetValue().Set(-fileNames.IndexOf(depName));
                    }
                    RecurseType(am, inst, info, child, fileNames, ids, depth + 1);
                }
            }
            if (depth == 0)
            {
                byte[] assetData;
                if (info.curFileType == 28)
                {
                    assetData = FixTexture2DFast(inst, info);
                }
                else
                {
                    using (MemoryStream ms = new MemoryStream())
                    using (AssetsFileWriter w = new AssetsFileWriter(ms))
                    {
                        w.bigEndian = false;
                        field.Write(w);
                        assetData = ms.ToArray();
                    }
                }
                AssetID thisId = new AssetID(inst.name, (long)info.index);
                ids[thisId] = assetData;
            }
        }
        private static byte[] FixTexture2DFast(AssetsFileInstance inst, AssetFileInfoEx inf)
        {
            AssetsFileReader r = inst.file.reader;
            r.Position = inf.absoluteFilePos;
            r.Position += (ulong)r.ReadInt32() + 4;
            r.Align();
            r.Position += 0x48;
            r.Position += (ulong)r.ReadInt32() + 4;
            r.Align();
            r.Position += 0x8;
            ulong filePathPos = r.Position;
            int assetLengthMinusFP = (int)(filePathPos - inf.absoluteFilePos);
            string filePath = r.ReadCountStringInt32();
            string directory = Path.GetDirectoryName(inst.path);
            string fixedPath = Path.Combine(directory, filePath);

            byte[] newData = new byte[assetLengthMinusFP + 4 + fixedPath.Length];
            r.Position = inf.absoluteFilePos;
            //imo easier to write it with binary writer than manually copy the bytes
            using (MemoryStream ms = new MemoryStream())
            using (AssetsFileWriter w = new AssetsFileWriter(ms))
            {
                w.bigEndian = false;
                w.Write(r.ReadBytes(assetLengthMinusFP));
                w.WriteCountStringInt32(fixedPath);
                return ms.ToArray();
            }
        }
        private static void RecurseTypeReplace(AssetsManager am, AssetsFileInstance inst, AssetFileInfoEx info, AssetTypeValueField field, List<string> fileNames, Dictionary<AssetID, byte[]> ids, Dictionary<AssetID, long> aidToPid, Dictionary<long, long> gidToMid, int depth)
        {
            string p = new string(' ', depth);
            foreach (AssetTypeValueField child in field.pChildren)
            {
                //Console.WriteLine(p + child.templateField.type + " " + child.templateField.name);
                if (!child.templateField.hasValue)
                {
                    if (child == null)
                        return;
                    string typeName = child.templateField.type;
                    if (typeName.StartsWith("PPtr<") && typeName.EndsWith(">") && child.childrenCount == 2)
                    {
                        int fileId = child.Get("m_FileID").GetValue().AsInt();
                        long pathId = child.Get("m_PathID").GetValue().AsInt64();

                        if (pathId == 0) //removed asset
                            continue;
                        if (fileId > 0)
                            throw new Exception("file id was not set correctly!");

                        fileId = -fileId;
                        string fileName = fileNames[fileId];

                        AssetID actualId = new AssetID(fileName, pathId);
                        if (!aidToPid.ContainsKey(actualId))
                        {
                            Console.WriteLine("WARNING: MISSING ID FOR " + actualId.fileName + " " + actualId.pathId + " ON " + inst.name + " " + info.index);
                            child.Get("m_PathID").GetValue().Set(0);
                        }
                        else
                        {
                            child.Get("m_PathID").GetValue().Set(aidToPid[actualId]);
                        }
                        child.Get("m_FileID").GetValue().Set(0);
                    }
                    RecurseTypeReplace(am, inst, info, child, fileNames, ids, aidToPid, gidToMid, depth + 1);
                }
            }
            if (depth == 0)
            {
                byte[] assetData;
                if (info.curFileType == 1)
                {
                    assetData = FixGameObjectFast(inst, info, field, (ulong)gidToMid[(long)info.index]);
                }
                else
                {
                    using (MemoryStream ms = new MemoryStream())
                    using (AssetsFileWriter w = new AssetsFileWriter(ms))
                    {
                        w.bigEndian = false;
                        field.Write(w);
                        assetData = ms.ToArray();
                    }
                }
                AssetID thisId = new AssetID(inst.name, (long)info.index);
                ids[thisId] = assetData;
            }
        }
        //this one has to work differently since we already modified the value field
        //so we save it to a stream and read from it manually so it works faster
        //note it may be better to loop through all objects but this would be slower
        //in the future, all components should be added so we won't need this
        private static byte[] FixGameObjectFast(AssetsFileInstance inst, AssetFileInfoEx inf, AssetTypeValueField field, ulong editDifferPid)
        {
            //dump current data to ms
            using (MemoryStream fms = new MemoryStream())
            using (AssetsFileWriter fw = new AssetsFileWriter(fms))
            {
                fw.bigEndian = false;
                field.Write(fw);
                fms.Position = 0;

                AssetsFileReader r = new AssetsFileReader(fms);
                r.bigEndian = false;
                int componentSize = r.ReadInt32();
                List<AssetPPtr> pptrs = new List<AssetPPtr>();
                for (int i = 0; i < componentSize; i++)
                {
                    int fileId = r.ReadInt32();
                    long pathId = r.ReadInt64();
                
                    //this gets rid of assets that have no reference
                    if (!(fileId == 0 && pathId == 0))
                    {
                        pptrs.Add(new AssetPPtr((uint)fileId, (ulong)pathId));
                    }
                }
                //add reference to EditDiffer mb
                pptrs.Add(new AssetPPtr(0, editDifferPid));

                int assetLengthMinusCP = (int)(inf.curFileSize - 4 - (componentSize * 12));

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
                    w.Write(r.ReadBytes(assetLengthMinusCP));
                    return ms.ToArray();
                }
            }
        }
    }
}
