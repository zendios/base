using Base;
using UnityEngine;
using UnityEngine.UI;

namespace Base.Ads
{
    /// <summary>
    /// "Remove ADs" button of the banner / MREC frames. com.zen.iap turns it into a purchase of the game's Remove Ads
    /// product (ZenIapSettings); without com.zen.iap the button hides itself.
    /// </summary>
    public class RemoveAdsButton : MonoBehaviour
    {
        public Button button;
        public Transform startParticlePosition;

        private void Awake()
        {
            if (ZenStore.SetupRemoveAdsButton == null)
            {
                gameObject.SetActive(false);
                return;
            }
            ZenStore.SetupRemoveAdsButton(gameObject, startParticlePosition);
        }
    }
}
