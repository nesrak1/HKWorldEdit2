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
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.Text;

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
        OpenScene(path);
    }

    [MenuItem("HKEdit/Open Scene By Name", priority = 0)]
    public static void OpenSceneByName()
    {
        AssetsManager am = new AssetsManager();
        am.LoadClassPackage(Path.Combine(Application.dataPath, "cldb.dat"));

        string gameDataPath = GetGamePath();

        AssetsFileInstance inst = am.LoadAssetsFile(Path.Combine(gameDataPath, "globalgamemanagers"), false);
        AssetFileInfoEx buildSettings = inst.table.getAssetInfo(11);

        List<string> scenes = new List<string>();
        AssetTypeValueField baseField = am.GetATI(inst.file, buildSettings).GetBaseField();
        AssetTypeValueField sceneArray = baseField.Get("scenes").Get("Array");
        for (uint i = 0; i < sceneArray.GetValue().AsArray().size; i++)
        {
            scenes.Add(sceneArray[i].GetValue().AsString() + "[" + i + "]");
        }
        SceneSelector sel = SceneSelector.ShowDialog(am, scenes, gameDataPath);
    }

    public static void OpenScene(string path)
    {
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

    private static string GetGamePath()
    {
        string gamePath = SteamHelper.FindHollowKnightPath();

        if (gamePath == "" || !Directory.Exists(gamePath))
        {
            EditorUtility.DisplayDialog("HKEdit", "Could not find Steam path. If you've moved your Steam directory this could be why. Contact nes.", "OK");
            return null;
        }

        string gameDataPath = Path.Combine(gamePath, "hollow_knight_Data");

        return gameDataPath;
    }

    //[MenuItem("HKDebug/Unload Bundle", priority = 51)]
    //public static void UnloadBundle()
    //{
    //    AssetBundle.UnloadAllAssetBundles(true);
    //}
}

public class SceneSelector : EditorWindow
{
    AssetsManager am = null;
    string[] strings = null;
    string gameDataPath;
    public static SceneSelector ShowDialog(AssetsManager am, List<string> scenes, string gameDataPath)
    {
        SceneSelector window = GetWindow<SceneSelector>();
        window.am = am;
        window.strings = scenes.ToArray();
        window.gameDataPath = gameDataPath;
        return window;
    }

    Vector2 scrollPos;
    int selected = -1;
    void OnGUI()
    {
        if (am == null || strings == null || gameDataPath == string.Empty)
            return;
        Rect scrollViewRect = new Rect(0, 0, position.width, position.height);
        Rect selectionGridRect = new Rect(0, 0, position.width - 20, strings.Length * 20);
        scrollPos = GUI.BeginScrollView(scrollViewRect, scrollPos, selectionGridRect);
        selected = GUI.SelectionGrid(selectionGridRect, selected, strings, 1);
        GUI.EndScrollView();

        if (selected != -1)
        {
            string path = Path.Combine(gameDataPath, "level" + selected);
            SceneLoad.OpenScene(path);
            selected = -1;
        }
    }

    void OnEnable()
    {
        titleContent.text = "Level Selector";
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
