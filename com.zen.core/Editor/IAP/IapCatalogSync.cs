#if USE_IN_APP_PURCHASE
using System.IO;
using System.Text;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Purchasing;

namespace Base
{
    /// <summary>
    /// One source of truth for the store listing: CurrencyManager (currency names) → IAPPackDatas (rewards, name) →
    /// IAPProductCatalog.json (title, description) → Documents/Store/google_play_iap.csv (Google Play import).
    /// The price stays in the catalog.
    /// </summary>
    public static class IapCatalogSync
    {
        private const string CsvPath = "Documents/Store/google_play_iap.csv";

        [MenuItem("Base/IAP/Sync Catalog from Packs")]
        private static void Sync()
        {
            var packs = AssetDatabase.FindAssets("t:IAPPackDatas")
                .Select(guid => AssetDatabase.LoadAssetAtPath<IAPPackDatas>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(x => x != null).SelectMany(x => x.list).ToList();
            var catalog = ProductCatalog.LoadDefaultCatalog();
            if (catalog == null || packs.Count == 0)
            {
                Debug.LogError($"[IAP] Sync: need {ProductCatalog.kCatalogPath} and an IAPPackDatas with packs.");
                return;
            }

            var csv = new StringBuilder();
            csv.AppendLine("Product ID,Published State,Purchase Type,Auto Translate,Locale; Title; Description,Auto Fill Prices,Price");
            int synced = 0;
            foreach (var pack in packs)
            {
                var item = catalog.allProducts.FirstOrDefault(x => x.id == pack.productId);
                if (item == null)
                {
                    Debug.LogWarning($"[IAP] Sync: no catalog product \"{pack.productId}\" for pack {pack.id}: add it in the IAP Catalog window.");
                    continue;
                }
                item.defaultDescription.Title = pack.name;
                item.defaultDescription.Description = pack.Describe();
                long micros = (long)(item.googlePrice.value * 1000000m);
                csv.AppendLine($"{item.id},published,managed_by_android,false,\"en-US; {pack.name}; {item.defaultDescription.Description}\",true,{micros}");
                synced++;
            }

            File.WriteAllText(ProductCatalog.kCatalogPath, ProductCatalog.Serialize(catalog));
            Directory.CreateDirectory(Path.GetDirectoryName(CsvPath));
            File.WriteAllText(CsvPath, csv.ToString());
            AssetDatabase.Refresh();
            Debug.Log($"[IAP] Sync: {synced} products of {packs.Count} packs (currency names: {CurrencyManager.SoftName} / {CurrencyManager.HardName}). Wrote {ProductCatalog.kCatalogPath} and {CsvPath}.");
        }
    }
}
#endif
