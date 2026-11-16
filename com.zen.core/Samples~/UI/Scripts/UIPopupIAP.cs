using UnityEngine;

/// <summary>
/// The shop popup of the sample (<c>UIPopupIAP.Show()</c>). Each pack slot is an IAPButtonExtent with its productId; a
/// free-reward slot is a ButtonWatchAdToGetCurrency. It is the game's shop: CurrencyManager.OpenShop (the currency
/// buttons, UIManagerDebug) opens it. A game with its own shop sets CurrencyManager.OpenShop to it instead.
/// </summary>
public class UIPopupIAP : UIPopupBase<UIPopupIAP>
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterShop() => CurrencyManager.OpenShop = () => Show();
}
