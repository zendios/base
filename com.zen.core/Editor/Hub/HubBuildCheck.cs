using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Base.Setup
{
    /// <summary>Build Check: the release-build checks of BuildCheck, run on demand.</summary>
    internal class HubBuildCheck : IHubTab
    {
        public string Title => "Build Check";

        private List<SetupIssue> issues;
        private bool release = true;
        private Vector2 scroll;

        public void OnEnable() => issues = BuildCheck.Run(release);

        public void OnGUI()
        {
            EditorGUILayout.LabelField("A build logs the problems below as warnings; it is not stopped. Errors must be fixed before publishing.", BaseHub.NoteWrapped);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            bool next = GUILayout.Toggle(release, "Check as a release build", EditorStyles.toolbarButton);
            if (next != release || GUILayout.Button("Check again", EditorStyles.toolbarButton))
            {
                release = next;
                OnEnable();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            int errors = issues.Count(i => i.type == MessageType.Error);
            EditorGUILayout.HelpBox(errors == 0 ? "Ready to build." : $"{errors} error(s) block a release build.", errors == 0 ? MessageType.Info : MessageType.Error);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            BaseHub.Issues(issues.OrderBy(i => i.type == MessageType.Error ? 0 : i.type == MessageType.Warning ? 1 : 2));
            EditorGUILayout.EndScrollView();
        }
    }
}
