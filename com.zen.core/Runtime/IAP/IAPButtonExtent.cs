#if USE_IN_APP_PURCHASE
using Base.Ads;
using System;
using System.Collections.Generic;
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

        [HideInInspector]
        public string productId;
        private string localizedPrice;
        private string isoCurrencyCode;

        public Button button = null;
        public TextMeshProUGUI titleText = null;
        public TextMeshProUGUI priceText = null;

        public Transform startParticlePosition;
        public List<IAPReward> rewards = new List<IAPReward>
        {
            new IAPReward { type = CurrencyType.Soft, value = 5000 },
            new IAPReward { type = CurrencyType.Ad, value = 1 }
        };

        private Product product;

        private void OnValidate()
        {
            if (button == null)
                TryGetComponent(out button);

            if (titleText && rewards != null && rewards.Count > 0)
            {
                var softReward = rewards.FirstOrDefault(x => x.type == CurrencyType.Soft);
                if (softReward != null)
                    titleText.text = softReward.value.ToString();
            }
        }

        private void Start()
        {
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
                ZenAnalytics.LogIAP(name, productId, IAPStatus.Show, localizedPrice, isoCurrencyCode);
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
            ZenStore.RaisePurchaseStarted();

            ZenAnalytics.LogIAP(name, productId, IAPStatus.Click, localizedPrice, isoCurrencyCode);

            if (storeController != null && !string.IsNullOrEmpty(productId))
            {
                storeController.PurchaseProduct(productId);
            }
            else
            {
                Debug.LogError($"[IAP] Unable to purchase: StoreController is null or product ID '{productId}' is invalid.");
            }
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

            if (titleText != null && product.metadata != null)
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

            ZenToast.ShowNotice("Thank for loving...");
            ZenAnalytics.LogIAP(name, productId, IAPStatus.Success, localizedPrice, isoCurrencyCode);

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

            ZenAnalytics.LogIAP(name, productId, IAPStatus.Failed, localizedPrice, isoCurrencyCode);

            ZenToast.ShowNotice($"Purchase FAILED: {order.Details}");
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

            ZenToast.ShowNotice($"Purchase FAILED: {order.Details}");
        }

        public void Restore()
        {
            if (storeController != null)
            {
                var purchases = storeController.GetPurchases();
                foreach (var order in purchases)
                {
                    if (order?.Info != null && (order.Info.Receipt != null || order.Info.TransactionID != null))
                    {
                        Debug.Log($"Purchase: TransactionID: {order.Info.TransactionID}, Receipt: {order.Info.Receipt}");
                        DataManager.UserData.isRemovedAds = true;
                    }
                }
            }
        }

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
    public class IAPButtonExtent : MonoBehaviour
    {
    }
}
#endif