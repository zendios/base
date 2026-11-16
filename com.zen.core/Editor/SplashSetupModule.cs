using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Base.Setup
{
    /// <summary>
    /// Base > Hub > Setup: the Splash every game needs at Build Settings index 0 (ATT, data, UMP, Firebase, ads, then the
    /// game's scene), and the game's ItemDatas registered on its DataManager (the data loads there, before any other
    /// scene). Create copies Core's template (Templates~, portrait or landscape from the Player Settings) to
    /// Assets/Scenes; from then on the scene is the game's.
    /// </summary>
    internal class SplashSetupModule : ISetupModule
    {
        public string Name => "Splash";
        public int Order => 1;

        private const string ScenesFolder = "Assets/Scenes";

        private static string TemplatesFolder
        {
            get
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(SplashSetupModule).Assembly);
                return Path.Combine(info != null ? info.resolvedPath : "Packages/com.zen.core", "Templates~");
            }
        }

        public IEnumerable<SetupIssue> Check()
        {
            string splash = SplashPath();
            if (splash == null)
            {
                yield return new SetupIssue
                {
                    title = "No Splash scene at Build Settings index 0",
                    message = "The Splash (ADsManager, DataManager, GameStateManager, IAPManager, Firebase, AudioManager) starts every game: " +
                              "ATT, data, UMP, Firebase and ads, then it loads build index 1. Create copies Base's template to " +
                              ScenesFolder + " and puts it first in the Build Settings.",
                    type = MessageType.Error,
                    fixLabel = "Create",
                    fix = CreateSplash,
                };
                yield break;
            }

            var missing = MissingItemDatas(splash);
            if (missing.Count > 0)
                yield return new SetupIssue
                {
                    message = $"ItemDatas not on the DataManager of {Path.GetFileName(splash)}: {string.Join(", ", missing.Select(Path.GetFileNameWithoutExtension))}. " +
                              "The DataManager loads the save in the Splash, so their saved values (owned, counts) would be lost.",
                    fixLabel = "Add",
                    fix = () => AddItemDatas(splash, missing),
                };
        }

        // Build index 0 when it holds an ADsManager (AdSplash), else null.
        private static string SplashPath()
        {
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0 || !File.Exists(scenes[0].path))
                return null;
            string text = File.ReadAllText(scenes[0].path);
            bool hasAds = new[] { "ADsManager", "ADsManager_Portrait" }
                .Select(n => AssetDatabase.AssetPathToGUID($"Packages/com.zen.core/Runtime/Ads/Prefabs/{n}.prefab"))
                .Any(guid => !string.IsNullOrEmpty(guid) && text.Contains(guid));
            return hasAds ? scenes[0].path : null;
        }

        private static void CreateSplash()
        {
            var orientation = PlayerSettings.defaultInterfaceOrientation;
            bool portrait = orientation == UIOrientation.Portrait || orientation == UIOrientation.PortraitUpsideDown;
            string name = portrait ? "00_SplashScreen_Portrait.unity" : "00_SplashScreen.unity";
            string target = $"{ScenesFolder}/{name}";
            if (!File.Exists(target))
            {
                Directory.CreateDirectory(ScenesFolder);
                File.Copy(Path.Combine(TemplatesFolder, name), target);   // no .meta: the scene gets the game's own GUID
                AssetDatabase.ImportAsset(target);
            }
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != target).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(target, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[Base Setup] {target} is the Splash (Build Settings index 0); the game's scene goes at index 1.");
        }

        // ItemDatas assets under Assets that the Splash scene file does not reference.
        private static List<string> MissingItemDatas(string splash)
        {
            string text = File.ReadAllText(splash);
            return AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => AssetDatabase.LoadMainAssetAtPath(path) is IItemDatas)
                .Where(path => !text.Contains(AssetDatabase.AssetPathToGUID(path)))
                .ToList();
        }

        private static void AddItemDatas(string splash, List<string> paths)
        {
            bool wasOpen = EditorSceneManager.GetSceneByPath(splash).isLoaded;
            var scene = EditorSceneManager.OpenScene(splash, OpenSceneMode.Additive);
            var manager = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DataManager>(true)).FirstOrDefault();
            if (manager != null)
            {
                var so = new SerializedObject(manager);
                var list = so.FindProperty("itemDatas");
                foreach (var path in paths)
                {
                    list.arraySize++;
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = AssetDatabase.LoadMainAssetAtPath(path);
                }
                so.ApplyModifiedProperties();
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[Base Setup] {paths.Count} ItemDatas added to the DataManager of {splash}");
            }
            if (!wasOpen)
                EditorSceneManager.CloseScene(scene, true);
        }
    }
}
