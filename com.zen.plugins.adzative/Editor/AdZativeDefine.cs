#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace Base.Ads
{
    /// <summary>
    /// Adds the USE_ADZATIVE scripting define while this plugin is in the project, so shared host code
    /// (e.g. zen-unity Base AdZative) can compile its AdZativeSDK engine only in games that have the plugin.
    /// </summary>
    [InitializeOnLoad]
    internal static class AdZativeDefine
    {
        public const string Symbol = "USE_ADZATIVE";

        static AdZativeDefine()
        {
            Ensure(NamedBuildTarget.Android);
            Ensure(NamedBuildTarget.iOS);
            Ensure(NamedBuildTarget.Standalone);
        }

        internal static void Ensure(NamedBuildTarget target)
        {
            string defines = PlayerSettings.GetScriptingDefineSymbols(target);
            var list = defines.Split(';').Select(d => d.Trim()).Where(d => d.Length > 0).ToList();
            bool legacy = list.Remove("ZEN_NATIVE_ADS");   // symbol of the plugin's previous name
            if (list.Contains(Symbol) && !legacy) return;
            if (!list.Contains(Symbol))
                list.Add(Symbol);
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", list));
        }
    }
}
#endif
