using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SplashStarter
{
    static SplashStarter()
    {
        Splash.Open();
    }
}

public class Splash : EditorWindow
{
    public static void Open()
    { 
        Splash window = CreateInstance(typeof(Splash)) as Splash;
        window.titleContent = new GUIContent("Welcome to HKEdit2");
        window.minSize = new Vector2(460, 250);
        window.maxSize = new Vector2(460, 250);
        window.ShowUtility();
    }

    void OnGUI()
    {
        Texture2D tex = new Texture2D(256, 256);
        ImageConversion.LoadImage(tex, File.ReadAllBytes("icon.png"));
        GUILayout.BeginHorizontal();
        GUILayout.Label(tex);
        GUILayout.BeginVertical(GUILayout.Height(240));
        GUILayout.FlexibleSpace();
        GUILayout.Label("HKEdit2 by nes\nAssetsTools by DerPopo\nHollow Knight by Team Cherry");
        if (GUILayout.Button("Open a new scene"))
        {
            Close();
            SceneLoad.CreateScene();
        }
        if (GUILayout.Button("Open an edited scene"))
        {
            Close();
            SceneLoad.CreateScene();
        }
        if (GUILayout.Button("Close"))
        {
            Close();
        }
        GUILayout.EndHorizontal();
    }
}
