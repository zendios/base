#if USE_IN_APP_PURCHASE
using Base.Ads;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.UI;

namespace Base
{
    public class IAPButtonExtent : MonoBehaviour
    {
        private StoreController storeController => IAPManager.Instance?.store;

        [Tooltip("Product id of the IAP catalog. The pack of IAPPackDatas (DataManager.itemDatas) with this productId gives the rewards.")]
        public string productId;
        private string localizedPrice;
        private string isoCurrencyCode;

        public Button button = null;
        public TextMeshProUGUI titleText = null;
        public TextMeshProUGUI priceText = null;
        [Tooltip("Optional: the product description of IAPProductCatalog.json (else the store's).")]
        public TextMeshProUGUI descriptionText = null;
        [Tooltip("Optional: the pack's icon (IAPPackDatas).")]
        public Image iconImage = null;
        [Tooltip("Optional: the pack's badge (\"BEST VALUE\"...); its object is hidden when the pack has none.")]
        public TextMeshProUGUI badgeText = null;
        [Tooltip("Optional: the whole badge (the ribbon with its text), shown only when the pack has a badge. Empty = the badge text.")]
        public GameObject badgeRoot = null;

        public Transform startParticlePosition;
        [Tooltip("Only for a button without a pack: what the purchase grants. Empty = nothing is granted (an error is logged).")]
        public List<IAPReward> rewards = new List<IAPReward>();

        private Product product;
        private ProductCatalog catalog;
        private bool isCatalogLoaded;

        private void OnValidate()
        {
            if (button == null)
                TryGetComponent(out button);

#if UNITY_EDITOR
            // Show the pack in the prefab: icon, badge, name, price and description, so the layout can be judged.
            var editorPack = FindPackInEditor();
            if (editorPack != null)
            {
                UnityEditor.EditorApplication.delayCall += () => PreviewInEditor(editorPack);
                return;
            }
#endif
            if (titleText && rewards != null && rewards.Count > 0)
            {
                var softReward = rewards.FirstOrDefault(x => x.type == CurrencyType.Soft);
                if (softReward != null)
                    titleText.text = softReward.value.ToString();
            }
        }

#if UNITY_EDITOR
        private IAPPackData FindPackInEditor()
        {
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:IAPPackDatas"))
            {
                var pack = FindPack(UnityEditor.AssetDatabase.LoadAssetAtPath<IAPPackDatas>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid)));
                if (pack != null)
                    return pack;
            }
            return null;
        }

        private void PreviewInEditor(IAPPackData pack)
        {
            if (this == null)
                return;
            ApplyPack(pack);
            ApplyCatalog();
            WarnIfDescriptionDiffers(pack);
            NameAfterPack(pack);
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
                UnityEditor.EditorUtility.SetDirty(graphic);
        }

        // The slot (the outermost prefab instance of this button) is named after its pack, to find it in the Hierarchy.
        // Only a slot that holds this one button: never a popup that holds many.
        private void NameAfterPack(IAPPackData pack)
        {
            if (Application.isPlaying || string.IsNullOrEmpty(pack.name))
                return;
            var slot = UnityEditor.PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject);
            var target = slot != null ? slot : gameObject;
            if (target.name == pack.name || target.GetComponentsInChildren<IAPButtonExtent>(true).Length != 1)
                return;
            target.name = pack.name;
            UnityEditor.EditorUtility.SetDirty(target);
            if (slot != null)
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        // The catalog description is generated from the pack (Base > IAP > Sync Catalog from Packs): warn when it is stale.
        private void WarnIfDescriptionDiffers(IAPPackData pack)
        {
            var description = CatalogItem?.defaultDescription.Description;
            string expected = pack.Describe();
            if (!string.IsNullOrEmpty(description) && description != expected)
                Debug.LogWarning($"[IAP] {name}: the catalog description of {productId} (\"{description}\") is not the pack's " +
                                 $"(\"{expected}\"): run Base > IAP > Sync Catalog from Packs.", this);
        }
#endif

        /// <summary>The pack of IAPPackDatas whose productId this button has, or null.</summary>
        internal IAPPackData Pack => FindPack(DataManager.Get<IAPPackDatas>());

        private IAPPackData FindPack(IAPPackDatas packs)
        {
            return packs == null || string.IsNullOrEmpty(productId) ? null : packs.list.FirstOrDefault(x => x.productId == productId);
        }

        private void Start()
        {
            var pack = Pack;

            // No product: not a purchase button (e.g. the free-reward slot of the shop).
            if (string.IsNullOrEmpty(productId))
                return;

            // Name, price and description of the catalog show even before the store answers (or without a store).
            ApplyPack(pack);
            ApplyCatalog();

            if (storeController == null)
            {
                Debug.LogError("[IAP] StoreController is null. Make sure IAPManager is initialized.");
                return;
            }

            storeController.OnPurchasePending += OnPurchasePending;
            storeController.OnPurchaseConfirmed += OnPurchaseConfirmed;
            storeController.OnPurchaseFailed += OnPurchaseFailed;

            UpdateUI();

            if (button != null)
            {
                button.onClick.RemoveListener(OnClick);
                button.onClick.AddListener(OnClick);
            }
        }

        private void OnEnable()
        {
            if (storeController == null)
            {
                Debug.LogWarning("[IAP] StoreController is null. Make sure IAPManager is initialized.");
                return;
            }

            if (product != null)
                AnalyticsManager.LogIAP(name, productId, IAPStatus.Show, localizedPrice, isoCurrencyCode);
        }

        private void OnDestroy()
        {
            if (storeController == null)
                return;

            storeController.OnPurchasePending -= OnPurchasePending;
            storeController.OnPurchaseConfirmed -= OnPurchaseConfirmed;
            storeController.OnPurchaseFailed -= OnPurchaseFailed;
        }

        private void OnClick()
        {
            AdsManager.DelayAdsForPurchase();

            AnalyticsManager.LogIAP(name, productId, IAPStatus.Click, localizedPrice, isoCurrencyCode);

            if (storeController != null && !string.IsNullOrEmpty(productId))
            {
                storeController.PurchaseProduct(productId);
            }
            else
            {
                Debug.LogError($"[IAP] Unable to purchase: StoreController is null or product ID '{productId}' is invalid.");
            }
        }

        /// <summary>The product of this button in IAPProductCatalog.json (Resources); null when none declares it.</summary>
        private ProductCatalogItem CatalogItem
        {
            get
            {
                if (!isCatalogLoaded)
                {
                    isCatalogLoaded = true;
                    catalog = ProductCatalog.LoadDefaultCatalog();
                }
                return string.IsNullOrEmpty(productId) ? null : catalog?.allValidProducts.FirstOrDefault(x => x.id == productId);
            }
        }

        // The catalog price is a USD fallback; the store's localized price replaces it in UpdateUI.
        private void ApplyCatalog()
        {
            var item = CatalogItem;
            if (item == null)
                return;
            var description = item.defaultDescription;
            if (titleText != null && !string.IsNullOrEmpty(description.Title))
                titleText.text = description.Title;
            if (descriptionText != null && !string.IsNullOrEmpty(description.Description))
                descriptionText.text = description.Description;
            if (priceText != null && localizedPrice == null && item.googlePrice.value > 0)
                priceText.text = "$" + item.googlePrice.value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private void ApplyPack(IAPPackData pack)
        {
            if (pack == null)
                return;
            if (iconImage != null && pack.icon != null)
                iconImage.sprite = pack.icon;
            if (badgeText != null)
                badgeText.text = pack.badge;
            var badge = badgeRoot != null ? badgeRoot : badgeText != null ? badgeText.gameObject : null;
            if (badge != null)
                badge.SetActive(!string.IsNullOrEmpty(pack.badge));
            ApplyRewards(pack);
        }

        // The reward cells of the slot (IAPRewardItem): the rows take the Soft / Hard / Remove Ads / VIP rewards, the grid
        // cells the boosters and items, each in order.
        private void ApplyRewards(IAPPackData pack)
        {
            var cells = GetComponentsInChildren<IAPRewardItem>(true);
            if (cells.Length == 0)
                return;
            var items = pack.rewards.Where(x => x.type == CurrencyType.Booster || x.type == CurrencyType.Item).ToList();
            var lines = pack.rewards.Except(items).ToList();
            Fill(cells.Where(x => x.IsRow).ToList(), lines, pack, "rows");
            Fill(cells.Where(x => !x.IsRow).ToList(), items, pack, "grid cells");
        }

        private void Fill(List<IAPRewardItem> cells, List<Reward> rewards, IAPPackData pack, string kind)
        {
            for (int i = 0; i < cells.Count; i++)
                cells[i].Show(i < rewards.Count ? rewards[i] : null);
            if (rewards.Count > cells.Count)
                Debug.LogWarning($"[IAP] {name}: pack {pack.id} has {rewards.Count} rewards for {cells.Count} {kind}: add cells.", this);
        }

        public void UpdateUI()
        {
            if (storeController == null) return;

            if (product == null && !string.IsNullOrEmpty(productId))
                product = storeController.GetProductById(productId);

            if (product == null)
                return;

            if (product.metadata != null)
            {
                localizedPrice = product.metadata.localizedPriceString;
                isoCurrencyCode = product.metadata.isoCurrencyCode;
            }

            var catalogItem = CatalogItem;
            bool hasCatalogTitle = catalogItem != null && !string.IsNullOrEmpty(catalogItem.defaultDescription.Title);
            if (titleText != null && product.metadata != null && !hasCatalogTitle)
            {
                string title = product.metadata.localizedTitle;
                try
                {
                    if (!string.IsNullOrEmpty(title) && title.Contains("("))
                        title = title.Split('(')?.FirstOrDefault()?.Trim();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
                finally
                {
                    if (titleText != null && !string.IsNullOrEmpty(title))
                        titleText.text = title.ToUpper();
                }
            }

            if (priceText != null && product.metadata != null)
            {
                priceText.text = product.metadata.localizedPriceString;
            }

            bool hasCatalogDescription = catalogItem != null && !string.IsNullOrEmpty(catalogItem.defaultDescription.Description);
            if (descriptionText != null && !hasCatalogDescription && product.metadata != null)
                descriptionText.text = product.metadata.localizedDescription;
        }


        void OnPurchasePending(PendingOrder order)
        {
            var targetProduct = GetFirstProductInOrder(order);
            if (targetProduct is null || targetProduct.definition.id != productId)
            {
                return;
            }

            Debug.Log($"OnPurchasePending: {targetProduct.definition.id}");

            // Add purchased product to player's inventory. Not granted (data not loaded): leave the order pending, the
            // store delivers it again (a confirmed order is never delivered again).
            if (!IAPManager.TryGrant(this, startParticlePosition))
                return;

            // Persist BEFORE confirming: a crash / kill after the confirm would otherwise lose a paid purchase
            // (the game only saves on pause).
            DataManager.Save(false);

            AdToast.ShowNotice("Thank for loving...");
            AnalyticsManager.LogIAP(name, productId, IAPStatus.Success, localizedPrice, isoCurrencyCode);

            storeController.ConfirmPurchase(order);
        }

        void OnPurchaseConfirmed(Order order)
        {
            switch (order)
            {
                case ConfirmedOrder confirmedOrder:
                    OnPurchaseConfirmed(confirmedOrder);
                    break;
                case FailedOrder failedOrder:
                    OnPurchaseConfirmationFailed(failedOrder);
                    break;
                default:
                    Debug.Log("OnPurchaseConfirmed: result Unknown");
                    break;
            }
        }

        public void OnPurchaseConfirmed(ConfirmedOrder order)
        {
            var targetProduct = GetFirstProductInOrder(order);
            if (targetProduct == null || targetProduct.definition.id != productId)
            {
                return;
            }

            Debug.Log($"OnPurchaseConfirmed: {targetProduct.definition.id}");
        }

        void OnPurchaseFailed(FailedOrder order)
        {
            var targetProduct = GetFirstProductInOrder(order);
            if (targetProduct == null || targetProduct.definition.id != productId)
            {
                return;
            }

            Debug.Log($"Purchase failed - Product: '{targetProduct.definition.id}', " +
                      $"PurchaseFailureReason: {order.FailureReason}," +
                      $" Failure Details: {order.Details}");

            AnalyticsManager.LogIAP(name, productId, IAPStatus.Failed, localizedPrice, isoCurrencyCode);

            AdToast.ShowNotice($"Purchase FAILED: {order.Details}");
        }

        void OnPurchaseConfirmationFailed(FailedOrder order)
        {
            var targetProduct = GetFirstProductInOrder(order);
            if (targetProduct == null || targetProduct.definition.id != productId)
            {
                return;
            }

            Debug.Log($"Confirmation failed - Product: '{targetProduct.definition.id}', " +
                      $"PurchaseFailureReason: {order.FailureReason}," +
                      $" Details: {order.Details}");

            AdToast.ShowNotice($"Purchase FAILED: {order.Details}");
        }

        public void Restore() => IAPManager.RestorePurchases();

        Product GetFirstProductInOrder(Order order)
        {
            return order?.CartOrdered?.Items()?.FirstOrDefault()?.Product;
        }

        [Serializable]
        public class IAPReward
        {
            public CurrencyType type = CurrencyType.Soft;
            public int value = 10000;
        }
    }
}
#else
using UnityEngine;
namespace Base
{
    /// <summary>Without com.unity.purchasing: keeps the fields game code sets, so it compiles either way.</summary>
    public class IAPButtonExtent : MonoBehaviour
    {
        public string productId;
    }
}
#endif