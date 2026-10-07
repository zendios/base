using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Base.Setup
{
    /// <summary>Build Check: the release-build checks of ZenBuildCheck, run on demand.</summary>
    internal class HubBuildCheck : IZenHubTab
    {
        public string Title => "Build Check";

        private List<ZenSetupIssue> issues;
        private bool release = true;
        private Vector2 scroll;

        public void OnEnable() => issues = ZenBuildCheck.Run(release);

        public void OnGUI()
        {
            EditorGUILayout.LabelField("A release (non-Development) build stops on the errors below; a Development build only logs them.", ZenHub.NoteWrapped);
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
            ZenHub.Issues(issues.OrderBy(i => i.type == MessageType.Error ? 0 : i.type == MessageType.Warning ? 1 : 2));
            EditorGUILayout.EndScrollView();
        }
    }
}
