using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Assets.Editor
{
    public class About : EditorWindow
    {
        [MenuItem("HKEdit/About", priority = 3)]
        public static void Open()
        {
            About window = CreateInstance(typeof(About)) as About;
            window.minSize = new Vector2(440, 200);
            window.maxSize = new Vector2(440, 200);
            window.ShowUtility();
        }

        void OnGUI()
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("HKEdit2 is a level editor for Hollow Knight.");
            GUILayout.Label("It generates diff files to make it easier");
            GUILayout.Label("to redistribute modified scenes.");
            GUILayout.Label("- nes");
            GUILayout.EndVertical();
            if (GUILayout.Button("Close"))
            {
                Close();
            }
            GUILayout.EndHorizontal();
        }

        void OnEnable()
        {
            titleContent.text = "About";
        }
    }
}
