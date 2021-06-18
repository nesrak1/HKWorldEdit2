using Assets.Bundler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Assets.Editor.Bundler
{
    [Serializable]
    public class DiffData
    {
        public List<GameObjectChange> goChanges;
        public List<GameObjectAddition> goAdditions;
    }
    [Serializable]
    public class GameObjectChange
    {
        public GameObjectChangeFlags flags;
        public DetailsChange detailChanges; //DetailsChanged
        public List<ComponentAdd> componentAdds; //ComponentsChanged
        public List<FieldDiff> componentChanges; //ComponentsChanged
        public long newParentId; //ParentChanged
    }
    [Serializable]
    public class DetailsChange
    {
        public string name;
        public int tag;
        public int layer;
        public bool disabled;
    }
    [Serializable]
    public class ComponentAdd
    {
        
    }
    [Serializable]
    public class FieldDiff
    {
        
    }
    [Serializable]
    public class GameObjectAddition
    {
        public long bundleId; //pathId in this bundle
        public long sceneId; //pathId to original scene
        public List<GameObjectAdditionDependency> dependencies;
    }
    [Serializable]
    public class GameObjectAdditionDependency
    {
        public long bundleId; //pathId in this bundle
        public long sceneId; //pathId to original scene
    }
    public enum GameObjectChangeFlags
    {
        None,
        Deleted = 1,
        DetailsChanged = 2,
        ComponentsChanged = 4,
        ParentChanged = 8
    }
}
