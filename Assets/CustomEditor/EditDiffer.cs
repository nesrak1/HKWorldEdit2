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
                return;
            }
            if (instanceId != GetInstanceID() && GetInstanceID() < 0)
            {
                pathId = NextPathID();
                instanceId = GetInstanceID();
                newAsset = true;
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
