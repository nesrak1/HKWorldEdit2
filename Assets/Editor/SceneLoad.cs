using Assets.Bundler;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class SceneLoad
{
    [MenuItem("HKEdit/Load Scene By File", priority = 0)]
    public static void CreateScene()
    {
        string path = EditorUtility.OpenFilePanel("Open level file", "", "");
        OpenScene(path);
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

            Loader.GenerateLevelFiles(path);

            AssetDatabase.Refresh();

            EditorSceneManager.OpenScene(localDestPath);
        }
    }

    [MenuItem("HKEdit/Load Scene By Name", priority = 1)]
    public static void OpenSceneByName()
    {
        AssetsManager am = new AssetsManager();
        am.LoadClassDatabase("cldb.dat");

        string gameDataPath = GetGamePath();

        AssetsFileInstance inst = am.LoadAssetsFile(Path.Combine(gameDataPath, "globalgamemanagers"), false);
        AssetFileInfoEx buildSettings = inst.table.GetAssetInfo(11);

        List<string> scenes = new List<string>();
        AssetTypeValueField baseField = am.GetATI(inst.file, buildSettings).GetBaseField();
        AssetTypeValueField sceneArray = baseField.Get("scenes").Get("Array");
        for (int i = 0; i < sceneArray.GetValue().AsArray().size; i++)
        {
            scenes.Add(sceneArray[i].GetValue().AsString() + "[" + i + "]");
        }
        SceneSelector.ShowDialog(am, scenes, gameDataPath);
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
        {
            GUILayout.Label("SceneSelector was unloaded. Run Load Scene By Name again.");
            return;
        }
        Rect scrollViewRect = new Rect(0, 0, position.width, position.height);
        Rect selectionGridRect = new Rect(0, 0, position.width - 20, strings.Length * 20);
        scrollPos = GUI.BeginScrollView(scrollViewRect, scrollPos, selectionGridRect);
        selected = GUI.SelectionGrid(selectionGridRect, selected, strings, 1);
        GUI.EndScrollView();

        if (selected != -1)
        {
            int oldSelected = selected;
            selected = -1; //prevent error loop
            string path = Path.Combine(gameDataPath, "level" + oldSelected);
            SceneLoad.OpenScene(path);
        }
    }

    void OnEnable()
    {
        titleContent.text = "Level Selector";
    }
}