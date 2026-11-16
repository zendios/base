using System.Collections.Generic;
using UnityEngine;

namespace Base.Ads
{
    /// <summary>
    /// Keeps the bottom of this RectTransform clear of the banner (UIManager > Contents, where screens and popups live).
    /// The banner (AdZative, AdmobBanner) calls Set() with the height of its strip; 0 = no banner (Remove Ads, VIP).
    /// Until a banner calls Set(), the RectTransform keeps the offset set in its prefab.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class AdBannerSpace : MonoBehaviour
    {
        private static readonly List<AdBannerSpace> spaces = new List<AdBannerSpace>();

        /// <summary>Height kept clear at the bottom (canvas units of the banner); &lt; 0 = not set by a banner yet.</summary>
        public static float Height { get; private set; } = -1f;

        private RectTransform rectTransform;

        private void Awake() => rectTransform = (RectTransform)transform;

        private void OnEnable()
        {
            spaces.Add(this);
            Apply();
        }

        private void OnDisable() => spaces.Remove(this);

        /// <summary>Sets the height kept clear at the bottom of every AdBannerSpace, now and for those enabled later.</summary>
        public static void Set(float height)
        {
            Height = Mathf.Max(0f, height);
            foreach (AdBannerSpace space in spaces)
                space.Apply();
        }

        private void Apply()
        {
            if (Height >= 0f)
                rectTransform.offsetMin = new Vector2(0, Height);
        }
    }
}
