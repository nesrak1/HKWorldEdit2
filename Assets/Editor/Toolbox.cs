using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public class Toolbox : EditorWindow
{
    [MenuItem("HKEdit/Toolbox", priority = 3)]
    public static void OpenSceneByName()
    {
        Toolbox window = GetWindow<Toolbox>();
    }

    void OnGUI()
    {
        if (GUILayout.Button("Transition Trigger"))
        {
            foreach (GameObject obj in Selection.gameObjects)
            {
                if (obj != null)
                {
                    string name = obj.name;
                    if (!name.StartsWith("top") && !name.StartsWith("bot") &&
                        !name.StartsWith("left") && !name.StartsWith("right"))
                    {
                        EditorUtility.DisplayDialog("HKEdit",
                            "One of selected objects does not have a valid name.\n" +
                            "(starts with \"top\", \"bot\", \"left\", \"right\")", "OK");
                        return;
                    }
                }
            }

            foreach (GameObject obj in Selection.gameObjects)
            {
                if (obj != null)
                {
                    BoxCollider2D collid = obj.AddComponent<BoxCollider2D>();
                    collid.size = new Vector2(1, 4);
                    LevelTransition trans = obj.AddComponent<LevelTransition>();
                    trans.targetScene = "Town";
                    trans.entryPoint = "left1";
                }
            }
        }
        if (GUILayout.Button("Camera Lock Area"))
        {
            foreach (GameObject obj in Selection.gameObjects)
            {
                if (obj != null)
                {
                    BoxCollider2D collid = obj.AddComponent<BoxCollider2D>();
                    collid.size = new Vector2(20, 15);
                    CameraLockArea area = obj.AddComponent<CameraLockArea>();
                    area.cameraXMin = -1;
                    area.cameraYMin = -1;
                    area.cameraXMax = -1;
                    area.cameraYMax = -1;
                }
            }
        }
    }

    void OnEnable()
    {
        titleContent.text = "Toolbox";
    }
}