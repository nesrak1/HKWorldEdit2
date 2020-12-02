using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SplashStarter
{
    static SplashStarter()
    {
        EditorApplication.update += Startup;
    }
    static void Startup()
    {
        EditorApplication.update -= Startup;
        //Splash.Open();
    }
}

public class Splash : EditorWindow
{
    private static string updateFeatures = "TK2D\nCamera lock zones & transitions";

    private bool dontShow;

    public static void Open()
    { 
        Splash window = CreateInstance(typeof(Splash)) as Splash;
        window.titleContent = new GUIContent("Welcome to HKEdit2");
        window.minSize = new Vector2(470, 250);
        window.maxSize = new Vector2(470, 250);
        window.ShowUtility();
    }

    private void OnGUI()
    {
        Texture2D tex = new Texture2D(256, 256);
        ImageConversion.LoadImage(tex, File.ReadAllBytes("icon.png"));
        GUILayout.BeginHorizontal();
        GUILayout.Label(tex);
        GUILayout.BeginVertical(GUILayout.Height(240));
        GUILayout.FlexibleSpace();
        GUILayout.Label("Welcome to HKEdit2!\n\nNew features:\n" + updateFeatures);
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
        if (GUILayout.Button("Settings"))
        {
            Close();
        }
        if (GUILayout.Button("Tutorial Playlist"))
        {
            Close();
        }
        if (GUILayout.Button("Close"))
        {
            Close();
        }
        dontShow = GUILayout.Toggle(dontShow, "Don't show this window again");
        GUILayout.EndHorizontal();
    }
}
