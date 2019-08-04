using Assets.Bundler;
using Assets.Editor;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoad
{
    //[MenuItem("HKEdit/Build Bundle", priority = 0)]
    //public static void CreateScene()
    //{
    //    BuildPipeline.BuildAssetBundles("Assets", BuildAssetBundleOptions.UncompressedAssetBundle, BuildTarget.StandaloneWindows);
    //}
    //[MenuItem("HKEdit/Load Bundle", priority = 0)]
    //public static void OpenScene()
    //{
    //    AssetBundle.UnloadAllAssetBundles(true);
    //    AssetBundle.LoadFromFile("Assets/testscenebundle_.unity3d");
    //    //EditorSceneManager.OpenScene("testscene");
    //    //GameObject go = new GameObject();
    //    //go.AddComponent<SceneLoader>().sceneName = "testscene";
    //}
    [MenuItem("HKEdit/Load Scene v2", priority = 0)]
    public static void CreateScene()
    {
        string path = EditorUtility.OpenFilePanel("Open level file", "", "");
        if (path.Length != 0)
        {
            string expScenesDir = Path.Combine(Application.dataPath, "ExportedScenes");
            string newFileName = Path.GetFileName(path) + ".unity";
            string destPath = Path.Combine(expScenesDir, newFileName);
            string localDestPath = "Assets/ExportedScenes/" + newFileName;
            if (File.Exists(destPath))
            {
                bool choice = EditorUtility.DisplayDialog(
                    "Scene already loaded", 
                    "This scene has already been converted. Do you want to continue working on the scene or discard and reload?",
                    "Continue Working", "Discard and Reload");
                if (choice)
                {
                    EditorSceneManager.OpenScene(localDestPath);
                    return;
                }
            }

            byte[] scene = Loader.CreateBundleFromLevel(path);
            if (scene == null)
                return;
            File.WriteAllBytes(destPath, scene);

            AssetDatabase.Refresh();

            EditorSceneManager.OpenScene(localDestPath);

            EditLevelMetadata metadata = Object.FindObjectOfType<EditLevelMetadata>();
            EditDiffer.usedIds.Clear();
            EditDiffer.lastId = 1;
            foreach (long usedId in metadata.usedIds)
            {
                EditDiffer.usedIds.Add(usedId);
                if (usedId > EditDiffer.lastId)
                    EditDiffer.lastId = usedId + 1;
            }
            Object.DestroyImmediate(metadata);
        }
    }
}
//[ExecuteInEditMode]
//public class SceneLoader : MonoBehaviour
//{
//    public string sceneName = "";
//    public void Start()
//    {
//        SceneManager.CreateScene("testscene");
//        
//    }
//}
