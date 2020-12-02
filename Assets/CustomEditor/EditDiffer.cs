using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Assets.Editor
{
    [ExecuteInEditMode]
    public class EditDiffer : MonoBehaviour
    {
        public int fileId;
        public long pathId;
        public long origPathId;
        public bool newAsset;
        public List<long> componentIds;
        //we don't want to serialize this, but it's already a dictionary so it works out
        public Dictionary<Component, long> componentMaps;
        public static long lastId = 0;
        public static HashSet<long> usedIds = new HashSet<long>();
        [SerializeField]
        int instanceId = 0;
        public void Awake()
        {
            if (Application.isPlaying)
                return;
            if (instanceId == 0)
            {
                instanceId = GetInstanceID();
                newAsset = false;
                componentMaps = new Dictionary<Component, long>();
                FillComponentInsts();
                return;
            }
            if (instanceId != GetInstanceID() && GetInstanceID() < 0)
            {
                pathId = NextPathID();
                instanceId = GetInstanceID();
                newAsset = true;
                componentIds = new List<long>();
                componentMaps = new Dictionary<Component, long>();
                FillComponentInsts();
            }
        }
        private void FillComponentInsts()
        {
            Component[] components = gameObject.GetComponents<Component>();
            componentIds = componentIds.Where(i => i != -1 && i != 0).ToList(); //filter unused
            int idsItr = 0;
            foreach (Component component in components)
            {
                if (component == this)
                    continue;
                if (component is Tk2dEmu)
                    continue;
                componentMaps.Add(component, componentIds[idsItr++]);
            }
        }
        public long NextPathID()
        {
            long nextPathId = 1;
            while (usedIds.Contains(nextPathId))
            {
                nextPathId++;
            }
            usedIds.Add(nextPathId);
            lastId = nextPathId;
            return nextPathId;
        }
    }
}
