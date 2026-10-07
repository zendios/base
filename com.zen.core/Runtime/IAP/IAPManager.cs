#if USE_IN_APP_PURCHASE
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;

namespace Base
{
    public class IAPManager : MonoBehaviour
    {
        public StoreController store { get; private set; }
        public static IAPManager Instance { get; private set; }

        private async void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            DontDestroyOnLoad(Instance);


            store = UnityIAPServices.StoreController();
            store.OnStoreConnected += OnStoreConnected;
            store.OnStoreDisconnected += OnStoreDisconnected;
            store.OnProductsFetchFailed += OnProductsFetchedFailed;
            store.OnProductsFetched += OnProductsFetched;
            await store.Connect();

            // Load local product catalog and fetch products from App Store / Google Play
            var catalog = ProductCatalog.LoadDefaultCatalog();
            if (catalog != null)
            {
                var catalogProvider = CodelessCatalogProvider.PopulateCatalogProvider(catalog);
                if (catalogProvider != null)
                    store.FetchProducts(catalogProvider.GetProducts());
            }
        }

        private void OnDestroy()
        {
            if (store != null)
            {
                store.OnStoreConnected -= OnStoreConnected;
                store.OnStoreDisconnected -= OnStoreDisconnected;
                store.OnProductsFetchFailed -= OnProductsFetchedFailed;
                store.OnProductsFetched -= OnProductsFetched;
            }
        }

        void OnStoreConnected()
        {
            Debug.Log($"OnStoreConnected.");
        }

        void OnStoreDisconnected(StoreConnectionFailureDescription description)
        {
            Debug.Log($"OnStoreDisconnected: {description.message}");
        }

        void OnProductsFetched(List<Product> products)
        {
            Debug.Log($"OnProductsFetched: {products.Count} products.");
        }

        void OnProductsFetchedFailed(ProductFetchFailed failure)
        {
            Debug.LogError($"OnProductsFetchedFailed: {failure.FailedFetchProducts?.Count ?? 0} products: {failure.FailureReason}");
        }

        /// <summary>
        /// Rewards Base does not store itself (Booster, Skin, Level, Other...): the game grants them here. Soft / Hard /
        /// Ad (remove ads) / VIP are granted by Base.
        /// </summary>
        public static event Action<IAPButtonExtent, IAPButtonExtent.IAPReward> OnGrantReward;

        public static void OnPurchaseSuccess(IAPButtonExtent product, Transform startParticlePosition = null)
        {
            TryGrant(product, startParticlePosition);
        }

        /// <summary>
        /// Grants every reward of the product into UserData. False = nothing could be granted (no product, data not
        /// loaded): the caller must NOT confirm the order, so the store delivers it again on the next launch.
        /// </summary>
        public static bool TryGrant(IAPButtonExtent product, Transform startParticlePosition = null)
        {
            if (product == null || product.rewards == null || !DataManager.IsLoaded || DataManager.UserData == null)
            {
                Debug.LogError($"[IAP] TryGrant {(product != null ? product.productId : "null")}: data not ready --> NOT granted");
                return false;
            }

            Transform end = startParticlePosition ? startParticlePosition : product.transform;
            foreach (var i in product.rewards)
            {
                if (i == null)
                    continue;
                switch (i.type)
                {
                    case CurrencyType.Ad:
                        DataManager.UserData.isRemovedAds = true;
                        break;
                    case CurrencyType.VIP:
                        DataManager.UserData.isVIP = true;
                        break;
                    case CurrencyType.Soft:
                    case CurrencyType.Hard:
                        if (!CurrencyManager.GrantFromPurchase(i.value, i.type, product.productId, product.transform, end))
                            Debug.LogError($"[IAP] {product.productId}: {i.type} {i.value} NOT granted");
                        break;
                    default:
                        if (OnGrantReward != null)
                            OnGrantReward(product, i);
                        else
                            Debug.LogWarning($"[IAP] {product.productId}: reward {i.type} {i.value} has no handler (IAPManager.OnGrantReward)");
                        break;
                }
            }
            return true;
        }
    }
}
#endif