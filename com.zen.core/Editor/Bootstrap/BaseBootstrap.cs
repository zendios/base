using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
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

        static BaseBootstrap()
        {
            EditorApplication.delayCall += Check;
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
            Debug.Log("[Base] DOTween installed; Base compiles now (DOTWEEN symbol set).");
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
