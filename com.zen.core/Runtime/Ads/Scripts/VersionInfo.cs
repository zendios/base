using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Base.Base
{
    public class VersionInfo : MonoBehaviour
    {
        [SerializeField] protected TextMeshProUGUI version = null;
        [SerializeField] protected Button button = null;

        private void Awake()
        {
            button.onClick.AddListener(() =>
            {
                DebugMode.Count++;
            });

            DataManager.OnLoaded += DataManagerOnLoaded;
        }

        private void OnEnable()
        {
            UpdateVersionInfo();
        }

        private void OnValidate()
        {
            if (version == null)
                TryGetComponent(out version);
            if (button == null)
                TryGetComponent(out button);
        }

        private void DataManagerOnLoaded()
        {
            UpdateVersionInfo();
        }

        protected void UpdateVersionInfo()
        {
            if (version != null)
                version.text = "VERSION " + Application.version + " [" + DataManager.BundleVersion + "]";
        }
    }
}
