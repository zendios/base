#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Base.Base
{
    [ExecuteInEditMode]
    [CustomEditor(typeof(DataManager))]
    public class DataManagerEditor : Editor
    {
        DataManager data;

        private void OnEnable()
        {
            data = (DataManager)target;
        }

        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("RESET DATA"))
            {
                data.ResetData();
            }

            DrawDefaultInspector();
        }
    }
}
#endif