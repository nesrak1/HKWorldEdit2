using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Editor
{
    [ExecuteInEditMode]
    public class Tk2dEmu : MonoBehaviour
    {
        public Vector3[] vertices;
        public Vector2[] uvs;
        public int[] indices;

        [SerializeField]
        int instanceId = 0;
        public void Awake()
        {
            if (Application.isPlaying)
                return;
            if (instanceId == 0)
            {
                instanceId = GetInstanceID();
                MeshFilter meshFilter = GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh == null)
                {
                    Mesh mesh = new Mesh();

                    mesh.vertices = vertices;
                    mesh.triangles = indices;
                    mesh.normals = new Vector3[4]
                    {
                        -Vector3.forward,
                        -Vector3.forward,
                        -Vector3.forward,
                        -Vector3.forward
                    };
                    mesh.uv = uvs;

                    meshFilter.sharedMesh = mesh;
                }
                return;
            }
        }
    }
}
