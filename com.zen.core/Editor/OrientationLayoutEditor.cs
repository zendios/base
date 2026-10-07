using UnityEditor;
using UnityEngine;

namespace Base.Setup
{
    [CustomEditor(typeof(OrientationLayout)), CanEditMultipleObjects]
    public class OrientationLayoutEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Lay this element out, then save the pose of the current orientation. " +
                                    "Active in a pose shows / hides the object in that orientation (keep it active in the scene so it can wake up).", MessageType.None);
            Row("Portrait", l => l.portrait);
            Row("Landscape", l => l.landscape);
            GUILayout.Space(6);
            DrawDefaultInspector();
        }

        private void Row(string label, System.Func<OrientationLayout, OrientationLayout.Pose> pose)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button($"Save as {label}"))
                foreach (OrientationLayout l in targets)
                {
                    Undo.RecordObject(l, $"Save {label} pose");
                    l.Save(pose(l));
                    EditorUtility.SetDirty(l);
                }
            if (GUILayout.Button($"Preview {label}"))
                foreach (OrientationLayout l in targets)
                {
                    Undo.RecordObject(l.transform, $"Preview {label} pose");
                    l.Load(pose(l));
                }
            EditorGUILayout.EndHorizontal();
        }
    }
}
