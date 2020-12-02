using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Assets.Bundler
{
    [Serializable]
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
        public override string ToString()
        {
            return fileName + " " + pathId.ToString();
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

    public class EditDifferData
    {
        public int fileId;
        public long pathId;
        public long origPathId;
        public bool newAsset;
    }

    public class BidirDictionary<T1, T2> : IEnumerable<KeyValuePair<T1, T2>>
    {
        public Dictionary<T1, T2> forward;
        public Dictionary<T2, T1> backward;
        public BidirDictionary()
        {
            forward = new Dictionary<T1, T2>();
            backward = new Dictionary<T2, T1>();
        }
        public int Count
        {
            get
            {
                if (forward.Count != backward.Count)
                    throw new Exception("Sizes are inconsistent");
                return forward.Count;
            }
        }
        public void Add(T1 key, T2 value)
        {
            forward.Add(key, value);
            backward.Add(value, key);
        }
        public bool RemoveKey(T1 key)
        {
            bool success = true;
            if (forward.ContainsKey(key))
            {
                T2 value = forward[key];
                success &= forward.Remove(key);
                success &= backward.Remove(value);
            }
            else
            {
                success = false;
            }
            return success;
        }
        public bool RemoveValue(T2 value)
        {
            bool success = true;
            if (backward.ContainsKey(value))
            {
                T1 key = backward[value];
                success &= forward.Remove(key);
                success &= backward.Remove(value);
            }
            else
            {
                success = false;
            }
            return success;
        }
        public bool ContainsKey(T1 key)
        {
            return forward.ContainsKey(key) && backward.ContainsValue(key);
        }
        public bool ContainsValue(T2 value)
        {
            return forward.ContainsValue(value) && backward.ContainsKey(value);
        }
        public T2 GetValue(T1 key)
        {
            return forward[key];
        }
        public T1 GetKey(T2 value)
        {
            return backward[value];
        }

        public IEnumerator<KeyValuePair<T1, T2>> GetEnumerator()
        {
            return forward.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return forward.GetEnumerator();
        }
    }

    public class Tk2dInfo
    {
        public Vector3[] positions;
        public Vector2[] uvs;
        public int[] indices;

        public Tk2dInfo(Vector3[] positions, Vector2[] uvs, int[] indices)
        {
            this.positions = positions;
            this.uvs = uvs;
            this.indices = indices;
        }
    }
}
