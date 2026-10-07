using System.Linq;
using UnityEditor;

namespace Base.Ads
{
    /// <summary>
    /// AdZative inspector: only the fields of its ad type. Banner Strip (reserved area, offsets, sizes, root rects):
    /// banner only. Full-screen timing: interstitial, rewarded, app open. Pod size: interstitial / rewarded.
    /// </summary>
    [CustomEditor(typeof(AdZative)), CanEditMultipleObjects]
    public class AdZativeEditor : Editor
    {
        private static readonly string[] BannerFields =
            { "background", "backgroundRect", "offsetTop", "offsetTopDp", "size", "sizeDp", "sizeRequest", "sizeRequestDp", "rootRectTransform" };

        internal static bool IsFullscreen(AdType t) => t == AdType.Inter || t == AdType.RewardInter || t == AdType.AppOpen || t == AdType.Reward;
        private static bool HasPod(AdType t) => t == AdType.Inter || t == AdType.RewardInter || t == AdType.Reward;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var typeProp = serializedObject.FindProperty("type");
            bool mixed = typeProp.hasMultipleDifferentValues;   // several types selected: show everything
            var type = ((AdZative)target).type;

            var it = serializedObject.GetIterator();
            for (bool enter = true; it.NextVisible(enter); enter = false)
            {
                if (!mixed && !Shown(it.name, type))
                    continue;
                using (new EditorGUI.DisabledScope(it.name == "m_Script"))
                    EditorGUILayout.PropertyField(it, true);
            }
            serializedObject.ApplyModifiedProperties();
        }

        private static bool Shown(string field, AdType type)
        {
            if (BannerFields.Contains(field)) return type == AdType.Banner;
            if (field == "timing") return IsFullscreen(type);
            if (field == "podSize") return HasPod(type);
            return true;
        }
    }
}
