using Assets.Editor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSave
{
    [MenuItem("HKEdit/Save Scene", priority = 2)]
    public static void CreateScene()
    {
        string dataPath = "ExportedScenesData";
        if (!Directory.Exists(dataPath))
        {
            EditorUtility.DisplayDialog("No scenes exist", "No preloaded scenes were found. Please load a scene first.", "Close");
            return;
        }
        string scenePath = Path.GetFileNameWithoutExtension(SceneManager.GetActiveScene().path); //todo does name work
        string origPathPath = Path.Combine(dataPath, scenePath + ".metadata");
        if (!File.Exists(origPathPath))
        {
            if (!scenePath.StartsWith("level"))
                EditorUtility.DisplayDialog("Metadata not found", "Could not find the .metadata file. Did you change the file name?", "Close");
            else
                EditorUtility.DisplayDialog("Metadata not found", "Could not find the .metadata file. Did you clear the ExportedScenesData folder?", "Close");
            return;
        }
    }
}
//todo editorscenemanager
public class SceneSaveProcessor : UnityEditor.AssetModificationProcessor
{
    public static string[] OnWillSaveAssets(string[] paths)
    {
        string sceneName = string.Empty;

        foreach (string path in paths)
        {
            if (path.Contains(".unity"))
            {
                sceneName = Path.GetFileNameWithoutExtension(path);
            }
        }

        if (sceneName.Length == 0)
        {
            return paths;
        }

        GameObject[] allGameObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
        foreach (GameObject go in allGameObjects)
        {
            EditDiffer editDiffer = go.GetComponent<EditDiffer>();
            if (editDiffer == null)
                continue;
            Dictionary<Component, long> componentMaps = editDiffer.componentMaps;
            Component[] components = go.GetComponents<Component>();
            List<long> newIds = new List<long>();
            foreach (Component c in components)
            {
                if (componentMaps.ContainsKey(c))
                    newIds.Add(componentMaps[c]);
                else
                    newIds.Add(-1);
            }
            editDiffer.componentIds = newIds;
        }

        SceneView sv = EditorWindow.GetWindow<SceneView>();
        if (sv == null)
        {
            return paths;
        }
        sv.ShowNotification(new GUIContent("Scene saved & components updated. Use HKEdit->Save Scene to save diff."));

        return paths;
    }
}