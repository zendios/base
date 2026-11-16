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
        /// Rewards Base does not grant itself (Skin, Level, Other..., or a Booster / Item without an id): the game grants
        /// them here. Soft / Hard / Ad (remove ads) / VIP and Booster / Item with an id (Rewards.Grant) are granted by Base.
        /// </summary>
        public static event Action<IAPButtonExtent, IAPButtonExtent.IAPReward> OnGrantReward;

        /// <summary>Remove Ads is owned again when the store holds any purchase of this player (iOS "Restore" button).</summary>
        public static void RestorePurchases()
        {
            var store = Instance != null ? Instance.store : null;
            if (store == null)
                return;
            foreach (var order in store.GetPurchases())
            {
                if (order?.Info != null && (order.Info.Receipt != null || order.Info.TransactionID != null))
                {
                    Debug.Log($"Purchase: TransactionID: {order.Info.TransactionID}, Receipt: {order.Info.Receipt}");
                    DataManager.UserData.isRemovedAds = true;
                }
            }
        }

        /// <summary>The pack with this id in the registered IAPPackDatas (DataManager.itemDatas), or null.</summary>
        public static IAPPackData GetPack(string packId)
        {
            if (string.IsNullOrEmpty(packId))
                return null;
            var packs = DataManager.Get<IAPPackDatas>();
            return packs != null ? packs.Find(packId) : null;
        }

        public static void OnPurchaseSuccess(IAPButtonExtent product, Transform startParticlePosition = null)
        {
            TryGrant(product, startParticlePosition);
        }

        /// <summary>
        /// Grants every reward of the product (its pack's rewards, or its own list) into UserData. False = nothing could
        /// be granted (no product, data not loaded): the caller must NOT confirm the order, so the store delivers it again
        /// on the next launch.
        /// </summary>
        public static bool TryGrant(IAPButtonExtent product, Transform startParticlePosition = null)
        {
            if (product == null || !DataManager.IsLoaded || DataManager.UserData == null)
            {
                Debug.LogError($"[IAP] TryGrant {(product != null ? product.productId : "null")}: data not ready --> NOT granted");
                return false;
            }
            var rewards = RewardsOf(product);
            if (rewards.Count == 0)
            {
                Debug.LogError($"[IAP] TryGrant {product.productId}: no rewards (no pack with this productId in IAPPackDatas) --> NOT granted");
                return false;
            }

            Transform end = startParticlePosition ? startParticlePosition : product.transform;
            foreach (var reward in rewards)
            {
                switch (reward.type)
                {
                    case CurrencyType.Ad:
                        DataManager.UserData.isRemovedAds = true;
                        break;
                    case CurrencyType.VIP:
                        DataManager.UserData.isVIP = true;
                        break;
                    case CurrencyType.Soft:
                    case CurrencyType.Hard:
                        if (!CurrencyManager.GrantFromPurchase(reward.amount, reward.type, product.productId, product.transform, end))
                            Debug.LogError($"[IAP] {product.productId}: {reward.type} {reward.amount} NOT granted");
                        break;
                    case CurrencyType.Booster:
                    case CurrencyType.Item:
                        if (string.IsNullOrEmpty(reward.id))
                            RaiseGrantReward(product, reward);   // a button's own list has no ids: the game knows
                        else
                            Rewards.Grant(reward, product.productId, end);
                        break;
                    default:
                        RaiseGrantReward(product, reward);
                        break;
                }
            }
            return true;
        }

        /// <summary>The pack's rewards, else the button's own list (ids empty; empty by default: nothing is granted).</summary>
        internal static List<Reward> RewardsOf(IAPButtonExtent product)
        {
            var pack = product.Pack;
            if (pack != null)
                return pack.rewards;
            var rewards = new List<Reward>();
            if (product.rewards != null)
                foreach (var i in product.rewards)
                    if (i != null)
                        rewards.Add(new Reward { type = i.type, amount = i.value });
            return rewards;
        }

        private static void RaiseGrantReward(IAPButtonExtent product, Reward reward)
        {
            var legacy = new IAPButtonExtent.IAPReward { type = reward.type, value = reward.amount };
            if (OnGrantReward != null)
                OnGrantReward(product, legacy);
            else
                Debug.LogWarning($"[IAP] {product.productId}: reward {reward.type} {reward.id} {reward.amount} has no handler (IAPManager.OnGrantReward)");
        }
    }
}
#endif