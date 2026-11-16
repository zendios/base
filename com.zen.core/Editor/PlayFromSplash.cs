using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Base
{
    /// <summary>
    /// Play always starts from the Splash (build index 0), as on a device: ATT, data, UMP, Firebase and ads are set up
    /// by AdSplash, which then loads the scene that was open in the Editor instead of build index 1. Opening a scene
    /// directly never runs without the managers of the Splash. Base > Play From Splash turns it off.
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayFromSplash
    {
        private const string MenuPath = "Base/Play From Splash";
        private const string EnabledKey = "Base.PlayFromSplash.Enabled";
        private const string SceneKey = "Base.PlayFromSplash.Scene";   // read by AdSplash (same key)

        static PlayFromSplash()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, true);
            set => EditorPrefs.SetBool(EnabledKey, value);
        }

        [MenuItem(MenuPath, priority = 20)]
        private static void Toggle() => Enabled = !Enabled;

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode)
                return;
            SessionState.EraseString(SceneKey);
            EditorSceneManager.playModeStartScene = null;
            if (!Enabled || EditorBuildSettings.scenes.Length == 0)
                return;

            string splash = EditorBuildSettings.scenes[0].path;
            string open = SceneManager.GetActiveScene().path;
            var splashAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(splash);
            if (string.IsNullOrEmpty(open) || open == splash || splashAsset == null)
                return;

            // Play mode loads the scene from disk: unsaved edits would be missing.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorApplication.isPlaying = false;
                return;
            }
            EditorSceneManager.playModeStartScene = splashAsset;
            SessionState.SetString(SceneKey, open);
        }
    }
}
