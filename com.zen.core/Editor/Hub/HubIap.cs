using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
#if USE_IN_APP_PURCHASE
using UnityEngine.Purchasing;
#endif

namespace Base.Setup
{
    /// <summary>Remove Ads (section of Ad Config): the product the ad frames sell, its settings, and whether the IAP catalog has it.</summary>
    internal class HubIap
    {
        private IAPSettings settings;
        private SerializedObject so;
        private List<string> catalog;

        public void OnEnable()
        {
            settings = Resources.Load<IAPSettings>(IAPSettings.ResourcePath);
            so = settings != null ? new SerializedObject(settings) : null;
            catalog = CatalogIds();
        }

        private static List<string> CatalogIds()
        {
#if USE_IN_APP_PURCHASE
            var c = ProductCatalog.LoadDefaultCatalog();
            return c?.allProducts?.Select(p => p.id).ToList() ?? new List<string>();
#else
            return null;
#endif
        }

        public void OnGUI()
        {
            string productId = IAPSettings.GetRemoveAdsProductId(settings);
            EditorGUILayout.LabelField("Remove ADs product", productId, BaseHub.Body);
            EditorGUILayout.LabelField($"Empty setting = <package name>.{IAPSettings.RemoveAdsSuffix} ({Application.identifier}.{IAPSettings.RemoveAdsSuffix}). A custom id must end with \"{IAPSettings.RemoveAdsSuffix}\".", BaseHub.NoteWrapped);

            if (settings == null)
            {
                if (GUILayout.Button("Create Assets/Resources/IAPSettings (to set another id)", GUILayout.Width(360)))
                {
                    SetupUtil.CreateResource<IAPSettings>(IAPSettings.ResourcePath);
                    OnEnable();
                }
            }
            else
            {
                so.Update();
                EditorGUILayout.PropertyField(so.FindProperty("removeAdsProductId"));
                so.ApplyModifiedProperties();
            }

            GUILayout.Space(8);
            if (catalog == null)
                EditorGUILayout.HelpBox("Unity IAP is not installed: the Remove ADs buttons hide themselves (Base > Hub > Setup installs it).", MessageType.Info);
            else
            {
                if (!productId.EndsWith(IAPSettings.RemoveAdsSuffix))
                    EditorGUILayout.HelpBox($"\"{productId}\" does not end with \"{IAPSettings.RemoveAdsSuffix}\".", MessageType.Error);
                if (catalog.Contains(productId))
                    EditorGUILayout.HelpBox($"The IAP catalog has {productId}.", MessageType.Info);
                else
                    EditorGUILayout.HelpBox($"The IAP catalog (Services > In-App Purchasing > IAP Catalog) has no product {productId}: the Remove ADs purchase will fail. " +
                                            "Add it there and in Google Play / App Store with the same id.", MessageType.Error);
                EditorGUILayout.LabelField($"Catalog products ({catalog.Count})", BaseHub.Note);
                foreach (var id in catalog)
                    EditorGUILayout.LabelField("  " + id, BaseHub.Body);
            }
            if (GUILayout.Button("Refresh", GUILayout.Width(90)))
                OnEnable();
        }
    }
}
