#if UNITY_EDITOR && USE_IN_APP_PURCHASE
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Purchasing;
using UnityEngine;
using UnityEngine.Purchasing;

namespace Base
{
    [CustomEditor(typeof(IAPButtonExtent))]
    public class IAPButtonExtentEditor : Editor
    {
        private const string kNoProduct = "<None>";
        private readonly List<string> m_ValidIDs = new List<string>();
        private SerializedProperty m_ProductIDProperty;

        public void OnEnable()
        {
            OnEnableInternal();
        }

        public override void OnInspectorGUI()
        {
            OnInspectorGuiInternal();
        }

        protected void OnEnableInternal()
        {
            m_ProductIDProperty = serializedObject.FindProperty("productId");
        }

        protected void OnInspectorGuiInternal()
        {
            var iapButton = (IAPButtonExtent)target;
            DrawProductIdDropDown(iapButton, iapButton.productId);

            base.OnInspectorGUI();
        }

        void DrawProductIdDropDown(IAPButtonExtent iapButton, string productId)
        {
            serializedObject.Update();

            if (iapButton != null)
            {
                EditorGUILayout.LabelField(new GUIContent("Product ID:", "Select a product from the IAP catalog."));
                LoadProductIdsFromCodelessCatalog();
                m_ProductIDProperty.stringValue = GetCurrentlySelectedProduct(productId);

                if (GUILayout.Button("IAP Catalog..."))
                {
                    ProductCatalogEditor.ShowWindow();
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        void LoadProductIdsFromCodelessCatalog()
        {
            var catalog = ProductCatalog.LoadDefaultCatalog();

            m_ValidIDs.Clear();
            m_ValidIDs.Add(kNoProduct);

            if (catalog != null && catalog.allProducts != null)
            {
                foreach (var product in catalog.allProducts)
                {
                    m_ValidIDs.Add(product.id);
                }
            }
        }

        string GetCurrentlySelectedProduct(string productId)
        {
            var currentIndex = string.IsNullOrEmpty(productId) ? 0 : m_ValidIDs.IndexOf(productId);
            if (currentIndex < 0)
            {
                // Preserve current productId string if not yet found in valid IDs list
                return productId;
            }

            var newIndex = EditorGUILayout.Popup(currentIndex, m_ValidIDs.ToArray());
            return newIndex > 0 && newIndex < m_ValidIDs.Count ? m_ValidIDs[newIndex] : string.Empty;
        }
    }
}
#endif