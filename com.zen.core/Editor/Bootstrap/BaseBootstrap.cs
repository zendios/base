using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Networking;

namespace Base.Bootstrap
{
    /// <summary>
    /// Base needs DOTween. Base's assemblies (Zen.Core, Zen.Core.Editor, Base.dll, Base.Editor.dll) only compile when
    /// the DOTWEEN symbol is set, so a project without DOTween has no compile errors; this editor-only assembly has no
    /// dependency and always runs:
    ///  - DOTween (free or Pro) in the project but no DOTWEEN symbol (old project, or DOTween not set up yet): adds it;
    ///  - no DOTween: offers to download the latest free DOTween from its official site (dotween.demigiant.com) and
    ///    import it, then adds the symbol. DOTween Pro is imported by hand from the Asset Store instead.
    /// </summary>
    [InitializeOnLoad]
    internal static class BaseBootstrap
    {
        private const string Symbol = "DOTWEEN";
        private const string DownloadPage = "https://dotween.demigiant.com/download.php";
        private const string ProStoreUrl = "https://assetstore.unity.com/packages/tools/visual-scripting/dotween-pro-32416";
        private const string SkipKey = "Base.Bootstrap.SkipDotween";
        private const string ConfigureKey = "Base.Bootstrap.ConfigureDotween";
        private const string DotweenSettingsPath = "Assets/Resources/DOTweenSettings.asset";

        static BaseBootstrap()
        {
            EdmDefaults();   // now, before EDM4U (Android Resolver) loads and asks
            EditorApplication.delayCall += Check;
        }

        /// <summary>EDM4U (Android Resolver): auto-resolution off, no "Enable Android Auto-resolution?" prompt
        /// (resolve with Assets > External Dependency Manager > Android Resolver > Resolve; Base > Hub > Setup does it).
        /// Only keys the project has not set yet: a choice made in the Android Resolver settings stays.</summary>
        private static void EdmDefaults()
        {
            try
            {
                const string path = "ProjectSettings/GvhProjectSettings.xml";
                string xml = File.Exists(path) ? File.ReadAllText(path)
                    : "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<projectSettings>\n</projectSettings>";
                bool changed = false;
                foreach (var key in new[] { "GooglePlayServices.AutoResolverEnabled", "GooglePlayServices.PromptBeforeAutoResolution" })
                {
                    if (xml.Contains($"name=\"{key}\"") || !xml.Contains("</projectSettings>")) continue;
                    xml = xml.Replace("</projectSettings>", $"  <projectSetting name=\"{key}\" value=\"False\" />\n</projectSettings>");
                    changed = true;
                }
                if (changed) File.WriteAllText(path, xml);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Base] Android Resolver settings: " + ex.Message);
            }
        }

        private static bool HasDotween =>
            AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "DOTween")
            || AssetDatabase.FindAssets("DOTween t:DefaultAsset").Select(AssetDatabase.GUIDToAssetPath).Any(p => p.EndsWith("/DOTween.dll"));

        private static void Check()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isBatchMode)
                return;
            if (HasDotween)
            {
                AddSymbol();
                // DOTween just imported (by Base, or by hand: no DOTweenSettings yet): Base's module defaults
                if (SessionState.GetBool(ConfigureKey, false) || AssetDatabase.FindAssets("DOTweenSettings").Length == 0)
                    ConfigureDotween();
                return;
            }
            if (SessionState.GetBool(SkipKey, false))
                return;
            SessionState.SetBool(SkipKey, true);   // ask once per editor session
            int choice = EditorUtility.DisplayDialogComplex("Base needs DOTween",
                "Base uses DOTween and stays off until the project has it.\n\n" +
                "Install downloads the latest free DOTween from its official site (dotween.demigiant.com) and imports it into Assets/Plugins/Demigiant.\n\n" +
                "Using DOTween Pro? Import it from the Asset Store instead (Package Manager > My Assets).",
                "Install DOTween", "Later", "I use DOTween Pro");
            if (choice == 0)
                InstallDotween();
            else if (choice == 2)
                Application.OpenURL(ProStoreUrl);
        }

        /// <summary>Downloads the newest DOTween_x_y_zzz.zip listed on the official download page and imports its .unitypackage.</summary>
        private static void InstallDotween()
        {
            try
            {
                string page = Get(DownloadPage, "Reading the DOTween download page");
                var m = Regex.Match(page ?? "", @"downloads/(DOTween_[0-9_]+\.zip)");
                if (!m.Success)
                    throw new Exception("no DOTween download link on " + DownloadPage);
                string url = "https://dotween.demigiant.com/" + m.Value;
                string temp = Path.Combine(Path.GetTempPath(), "base-dotween-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(temp);
                string zip = Path.Combine(temp, m.Groups[1].Value);
                Download(url, zip);
                ZipFile.ExtractToDirectory(zip, temp);
                string package = Directory.GetFiles(temp, "*.unitypackage", SearchOption.AllDirectories).FirstOrDefault()
                                 ?? throw new Exception("no .unitypackage in " + m.Groups[1].Value);
                Debug.Log($"[Base] importing {Path.GetFileName(package)} from {url}");
                AssetDatabase.importPackageCompleted -= OnImported;
                AssetDatabase.importPackageCompleted += OnImported;
                AssetDatabase.ImportPackage(package, false);
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Base", "DOTween could not be installed: " + ex.Message +
                                                    "\n\nImport DOTween from the Asset Store (Package Manager > My Assets) instead.", "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void OnImported(string name)
        {
            if (!name.StartsWith("DOTween")) return;
            AssetDatabase.importPackageCompleted -= OnImported;
            AddSymbol();
            SessionState.SetBool(ConfigureKey, true);   // DOTween's editor dll loads after the next reload: Check() then
            Debug.Log("[Base] DOTween installed; Base compiles now (DOTWEEN symbol set).");
        }

        /// <summary>DOTween modules as Base uses them: Audio, Sprites, UI on; Physics, Physics2D, UI Toolkit, TextMesh Pro
        /// and the other external modules off. The same as ticking them in the DOTween Utility Panel and pressing Apply.
        /// Waits (next reload) until DOTween's editor dll is loaded.</summary>
        private static void ConfigureDotween()
        {
            var settingsType = Type.GetType("DG.Tweening.Core.DOTweenSettings, DOTween");
            var modulesType = Type.GetType("DG.DOTweenEditor.UI.DOTweenUtilityWindowModules, DOTweenEditor");
            if (settingsType == null || modulesType == null)
            {
                SessionState.SetBool(ConfigureKey, true);
                return;
            }
            SessionState.EraseBool(ConfigureKey);
            try
            {
                string path = AssetDatabase.FindAssets("DOTweenSettings").Select(AssetDatabase.GUIDToAssetPath)
                    .FirstOrDefault(p => p.EndsWith(".asset"));
                var settings = path != null ? AssetDatabase.LoadAssetAtPath(path, settingsType) : null;
                if (settings == null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(DotweenSettingsPath));
                    settings = ScriptableObject.CreateInstance(settingsType);
                    AssetDatabase.CreateAsset(settings, DotweenSettingsPath);
                }
                var so = new SerializedObject(settings);
                foreach (var (module, on) in new[]
                         {
                             ("audioEnabled", true), ("physicsEnabled", false), ("physics2DEnabled", false), ("spriteEnabled", true),
                             ("uiEnabled", true), ("uiToolkitEnabled", false), ("textMeshProEnabled", false), ("tk2DEnabled", false),
                             ("deAudioEnabled", false), ("deUnityExtendedEnabled", false), ("epoOutlineEnabled", false),
                         })
                {
                    var p = so.FindProperty("modules." + module);
                    if (p != null) p.boolValue = on;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();

                const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                var refresh = modulesType.GetMethod("RefreshDOTweenSettings", flags);
                if (refresh != null && refresh.GetParameters().Length == 1)
                    refresh.Invoke(null, new object[] { settings });
                modulesType.GetMethod("ApplyModulesSettings", flags)?.Invoke(null, null);
                Debug.Log("[Base] DOTween modules set: Audio, Sprites, UI on; Physics, Physics2D, UI Toolkit off (Tools > Demigiant > DOTween Utility Panel to change).");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Base] DOTween modules not set (open Tools > Demigiant > DOTween Utility Panel): " + ex.Message);
            }
        }

        /// <summary>DOTWEEN for the active platform and the usual mobile / desktop ones.</summary>
        private static void AddSymbol()
        {
            var groups = new[] { EditorUserBuildSettings.selectedBuildTargetGroup, BuildTargetGroup.Android, BuildTargetGroup.iOS, BuildTargetGroup.Standalone }.Distinct();
            foreach (var group in groups)
            {
                if (group == BuildTargetGroup.Unknown) continue;
                var target = NamedBuildTarget.FromBuildTargetGroup(group);
                string defines = PlayerSettings.GetScriptingDefineSymbols(target);
                var list = defines.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                if (list.Contains(Symbol)) continue;
                list.Add(Symbol);
                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", list));
                Debug.Log($"[Base] {Symbol} added to the {group} scripting symbols (Base turns on with DOTween).");
            }
        }

        private static string Get(string url, string what)
        {
            using (var req = UnityWebRequest.Get(url))
            {
                Send(req, what);
                return req.downloadHandler.text;
            }
        }

        private static void Download(string url, string file)
        {
            using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET, new DownloadHandlerFile(file), null))
                Send(req, "Downloading " + Path.GetFileName(file));
        }

        private static void Send(UnityWebRequest req, string what)
        {
            req.timeout = 60;
            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Base", what, req.downloadProgress))
                {
                    req.Abort();
                    throw new Exception("cancelled");
                }
                System.Threading.Thread.Sleep(30);
            }
            if (req.result != UnityWebRequest.Result.Success)
                throw new Exception($"{req.url}: {req.error}");
        }
    }
}
