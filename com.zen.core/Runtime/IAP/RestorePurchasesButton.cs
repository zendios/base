using UnityEngine;
using UnityEngine.UI;

namespace Base
{
    /// <summary>
    /// "Restore purchases" button (App Store rule): restores the player's purchases, Remove Ads first. Shown on iOS with
    /// Unity IAP only; it hides itself elsewhere.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class RestorePurchasesButton : MonoBehaviour
    {
        private void Awake()
        {
#if USE_IN_APP_PURCHASE
            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                GetComponent<Button>().onClick.AddListener(IAPManager.RestorePurchases);
                return;
            }
#endif
            gameObject.SetActive(false);
        }
    }
}
