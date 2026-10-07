using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Base.Ads;
using UnityEditor;
using UnityEngine;

namespace Base.Setup
{
    /// <summary>
    /// Ad Config: every field by group, fields that differ from the package defaults highlighted (with a reset),
    /// presets (package defaults, test, the game's own saved presets) and the Firebase Remote Config template export.
    /// </summary>
    internal class HubAdConfig : IZenHubTab
    {
        public string Title => "Ad Config";

        private const string PresetsFolder = "Assets/ZenProfiles/AdConfig";

        private AdConfig config, defaults;
        private SerializedObject so, def;
        private Vector2 scroll;
        private string presetName = "Casual";
        private readonly HubIap iap = new HubIap();

        public void OnEnable()
        {
            iap.OnEnable();
            config = Resources.Load<AdConfig>(AdConfig.ResourcePath);
            so = config != null ? new SerializedObject(config) : null;
            if (defaults == null)
            {
                defaults = ScriptableObject.CreateInstance<AdConfig>();
                defaults.hideFlags = HideFlags.HideAndDontSave;
            }
            def = new SerializedObject(defaults);
        }

        public void OnGUI()
        {
            if (config == null)
            {
                EditorGUILayout.HelpBox("No AdConfig in Assets/Resources: the game runs with the package defaults.", MessageType.Warning);
                if (GUILayout.Button("Create Assets/Resources/AdConfig", GUILayout.Width(260)))
                {
                    ZenSetupUtil.CreateResource<AdConfig>(AdConfig.ResourcePath);
                    OnEnable();
                }
                return;
            }

            DrawPresets();
            EditorGUILayout.LabelField("Highlighted = differs from the package default. Remote Config (same key names) overrides these values at runtime.", ZenHub.NoteWrapped);

            so.Update();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var it = so.GetIterator();
            bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = false;
                if (it.name == "m_Script")
                    continue;
                var d = def.FindProperty(it.propertyPath);
                bool changed = d != null && !SerializedProperty.DataEquals(it, d);
                EditorGUILayout.BeginHorizontal();
                var old = GUI.backgroundColor;
                if (changed) GUI.backgroundColor = ZenHub.Changed;
                EditorGUILayout.PropertyField(it, true);
                GUI.backgroundColor = old;
                using (new EditorGUI.DisabledScope(!changed))
                    if (GUILayout.Button(new GUIContent("Reset", d != null ? "Back to the package default" : null), EditorStyles.miniButton, GUILayout.Width(48)))
                        so.CopyFromSerializedProperty(d);
                EditorGUILayout.EndHorizontal();
            }
            so.ApplyModifiedProperties();
            ZenHub.Header("Remove Ads (IAP)");
            iap.OnGUI();
            EditorGUILayout.EndScrollView();

            GUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Export Remote Config template (JSON)", GUILayout.Width(240)))
                ExportRemoteConfig();
            if (GUILayout.Button("Copy JSON to clipboard", GUILayout.Width(160)))
            {
                EditorGUIUtility.systemCopyBuffer = RemoteConfigJson();
                Debug.Log("[Base Hub] Remote Config template copied to the clipboard");
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPresets()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Presets", ZenHub.Note, GUILayout.Width(50));
            if (GUILayout.Button(new GUIContent("Package defaults", "Every field back to the package default (ad IDs are not here)"), EditorStyles.toolbarButton))
                Apply(() => EditorUtility.CopySerializedManagedFieldsOnly(defaults, config), "Package defaults");
            if (GUILayout.Button(new GUIContent("Test (no waits)", "Test IDs on, no time / play limits before an interstitial"), EditorStyles.toolbarButton))
                Apply(() =>
                {
                    var s = new SerializedObject(config);
                    s.FindProperty("useIdTest").boolValue = true;
                    s.FindProperty("_adTimePlayToShow").floatValue = 0;
                    s.FindProperty("_adTimePlayReduceToShow").floatValue = 0;
                    s.FindProperty("_adTimeBetween").floatValue = 0;
                    s.FindProperty("_adInterOnPlay").intValue = 0;
                    s.ApplyModifiedPropertiesWithoutUndo();
                }, "Test");
            foreach (var path in Presets())
                if (GUILayout.Button(new GUIContent(Path.GetFileNameWithoutExtension(path), "Load this saved preset"), EditorStyles.toolbarButton))
                {
                    string json = File.ReadAllText(path);
                    Apply(() => EditorJsonUtility.FromJsonOverwrite(json, config), Path.GetFileNameWithoutExtension(path));
                }
            GUILayout.FlexibleSpace();
            presetName = EditorGUILayout.TextField(presetName, EditorStyles.toolbarTextField, GUILayout.Width(90));
            if (GUILayout.Button("Save as preset", EditorStyles.toolbarButton) && !string.IsNullOrWhiteSpace(presetName))
            {
                Directory.CreateDirectory(PresetsFolder);
                File.WriteAllText($"{PresetsFolder}/{presetName.Trim()}.json", EditorJsonUtility.ToJson(config, true));
                AssetDatabase.Refresh();
                Debug.Log($"[Base Hub] AdConfig preset saved: {PresetsFolder}/{presetName.Trim()}.json");
            }
            EditorGUILayout.EndHorizontal();
        }

        private static IEnumerable<string> Presets() =>
            Directory.Exists(PresetsFolder) ? Directory.GetFiles(PresetsFolder, "*.json").OrderBy(p => p) : Enumerable.Empty<string>();

        private void Apply(Action change, string name)
        {
            Undo.RecordObject(config, "AdConfig preset " + name);
            change();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            so.Update();
            Debug.Log($"[Base Hub] AdConfig preset applied: {name}");
        }

        private void ExportRemoteConfig()
        {
            string path = EditorUtility.SaveFilePanel("Remote Config template", "", "remote_config_template.json", "json");
            if (string.IsNullOrEmpty(path))
                return;
            File.WriteAllText(path, RemoteConfigJson());
            Debug.Log($"[Base Hub] Remote Config template written: {path} (Firebase console > Remote Config > Publish from a file)");
        }

        /// <summary>
        /// Firebase Remote Config template ({"parameters": ...}) with every Zen key: GameConfig, AdConfig (this
        /// game's values) and one JSON per native full-screen placement (pod size + timing of its AdZative prefab; key =
        /// the prefab's placement name).
        /// </summary>
        public static string RemoteConfigJson()
        {
            var values = new Dictionary<string, object>();
            GameConfigRemote.AddDefaults(values);
            AdConfigRemote.AddDefaults(values);
            foreach (var z in NativePrefabs().Where(z => AdZativeEditor.IsFullscreen(z.type)))
            {
                var s = new SerializedObject(z);
                values[s.FindProperty("placementName").stringValue] = "{\"podSize\":" + z.PodSize + "," + JsonUtility.ToJson(z.Timing).Substring(1);
            }

            var sb = new StringBuilder("{\n  \"parameters\": {\n");
            var keys = values.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            for (int i = 0; i < keys.Count; i++)
            {
                object v = values[keys[i]];
                string type, text;
                switch (v)
                {
                    case bool b: type = "BOOLEAN"; text = b ? "true" : "false"; break;
                    case int n: type = "NUMBER"; text = n.ToString(CultureInfo.InvariantCulture); break;
                    case float f: type = "NUMBER"; text = f.ToString(CultureInfo.InvariantCulture); break;
                    case double d: type = "NUMBER"; text = d.ToString(CultureInfo.InvariantCulture); break;
                    case string s when s.TrimStart().StartsWith("{"): type = "JSON"; text = s; break;
                    default: type = "STRING"; text = v?.ToString() ?? ""; break;
                }
                sb.Append($"    \"{keys[i]}\": {{ \"defaultValue\": {{ \"value\": {Quote(text)} }}, \"valueType\": \"{type}\" }}");
                sb.Append(i < keys.Count - 1 ? ",\n" : "\n");
            }
            sb.Append("  }\n}\n");
            return sb.ToString();
        }

        /// <summary>Every AdZative prefab (project and packages). Prefab files are first filtered by text (AdZative
        /// script GUID, or a Prefab Variant), so big projects stay fast.</summary>
        private static IEnumerable<AdZative> NativePrefabs()
        {
            string guid = AssetDatabase.FindAssets("AdZative t:MonoScript")
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => Path.GetFileName(p) == "AdZative.cs")
                .Select(AssetDatabase.AssetPathToGUID).FirstOrDefault();
            return AssetDatabase.FindAssets("t:Prefab").Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => guid == null || FileHas(p, guid) || FileHas(p, "m_SourcePrefab"))   // variants carry the script only through their base
                .Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p)?.GetComponent<AdZative>())
                .Where(z => z != null);
        }

        private static bool FileHas(string assetPath, string text)
        {
            try { return File.ReadAllText(FileUtil.GetPhysicalPath(assetPath)).Contains(text); }
            catch { return true; }
        }

        private static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
    }
}
