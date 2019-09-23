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
    [MenuItem("HKEdit/Load Scene", priority = 0)]
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

            Loader.GenerateLevelFiles(path);
        }
    }
}