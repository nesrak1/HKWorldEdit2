using System;
using System.Collections.Generic;

namespace Assets.Bundler
{
    public class HKWEMeta
    {
        public short hkweVersion;
        public string levelName;
        public int levelIndex;
        public string diffPath;
        //public List<HKWEMetaAID> glblToLclMap;
    }
    //[Serializable]
    //public class HKWEMetaAID
    //{
    //    public AssetID global;
    //    public AssetID local;
    //    public HKWEMetaAID(AssetID global, AssetID local)
    //    {
    //        this.global = global;
    //        this.local = local;
    //    }
    //}
}
