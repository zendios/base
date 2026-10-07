using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace Base.Ads
{
    [CustomEditor(typeof(AdsManager))]
    public class AdsManagerEditor : Editor, IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 8;

        public override void OnInspectorGUI()
        {
            GUILayout.Space(12);
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                fixedHeight = 36
            };

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("VALIDATE ALL IDs", buttonStyle))
            {
                Debug.ClearDeveloperConsole();

                var assembly = Assembly.GetAssembly(typeof(Editor));
                var type = assembly.GetType("UnityEditor.LogEntries");
                var method = type.GetMethod("Clear");
                method.Invoke(new object(), null);

                Validate();
            }

            GUILayout.Space(12);
            GUI.backgroundColor = Color.white;
            DrawDefaultInspector();
        }

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            Validate();
        }

        /// <summary>Lists the placements of the game's ZenAdIds that have no ID (they load Google test IDs).</summary>
        public void Validate()
        {
            var ids = ZenAdIds.Instance;
            if (ids == null)
            {
                Debug.LogWarning("ZenAdIds: no Resources/ZenAdIds asset in the game (Base > Hub > Setup creates it) --> every placement uses Google test IDs");
                return;
            }

            foreach (PlacementType p in Enum.GetValues(typeof(PlacementType)))
            {
                if (p == PlacementType.None)
                    continue;
                var list = ids.Get(p);
                if (list.Count == 0)
                    Debug.LogWarning($"ZenAdIds: {p} has no ID --> Google test ID");
                else
                    Debug.Log($"ZenAdIds: {p} --> {string.Join(", ", list)}");
            }
        }
    }
}
