using System.Linq;
using UnityEngine;
using DG.Tweening;
using TMPro;
using UnityEngine.UI;

namespace Base.Ads
{
    public class AdToast : MonoBehaviour
    {
        [Header("Custom")]
        [SerializeField] protected GameObject content = null;
        [SerializeField] protected GameObject loadingObject = null;
        [SerializeField] protected Slider loadingSlider = null;

        [Header("Images - Use this to change your loading game")]
        [SerializeField] protected Image loadingImage = null;
        [SerializeField] protected AspectRatioFitter aspectRatioFitter;
#if UNITY_EDITOR
        [SerializeField] protected Sprite[] spriteInReoucesFolder;
#endif
        [HideInInspector] public string[] loadingSpriteInReoucesFolder;
        protected int loadingSpriteIndex = 0;

        [Header("Messages")]
        [SerializeField] protected TextMeshProUGUI message = null;
        [SerializeField] protected bool isToUpperCase = false;
        [SerializeField] protected int maxLenght = 42;
        [SerializeField] protected string[] messages;
        protected int messageIndex = 0;
        public static string messageDefault = "ADs break!!! We apologize for any inconvenience the advertisement may cause!";

        public static AdToast instance = null;

        private void Awake()
        {
            instance = this;
            if (content)
                content.SetActive(true);
            if (loadingSlider)
                loadingSlider.value = 0;
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            loadingSpriteInReoucesFolder = null;
            if (spriteInReoucesFolder != null && spriteInReoucesFolder.Length > 0)
                loadingSpriteInReoucesFolder = spriteInReoucesFolder.Where(x => x != null).Select(x => x.name).ToArray();
#endif

            if (loadingSpriteInReoucesFolder != null && loadingSpriteInReoucesFolder.Length > 0)
            {
                var sprite = Resources.Load<Sprite>(loadingSpriteInReoucesFolder[0]);
                if (sprite != null)
                    loadingImage.sprite = sprite;
            }

            if (loadingImage && loadingImage.sprite)
            {
                if (!aspectRatioFitter)
                    aspectRatioFitter = loadingImage.gameObject.GetComponent<AspectRatioFitter>();
                if (aspectRatioFitter && aspectRatioFitter.aspectMode != AspectRatioFitter.AspectMode.None)
                    aspectRatioFitter.aspectRatio = loadingImage.sprite.rect.width / loadingImage.sprite.rect.height;
            }

            if (messages != null && messages.Length > 0)
                message.text = isToUpperCase ? messages[0].ToUpper() : messages[0];
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterToast() => ZenToast.Backend = message => ShowNotice(message);

        public static void ShowNotice(string message, float durationAutoHide = 1.5f)
        {
            instance.DOShow(message, durationAutoHide, false);
        }

        public static void ShowLoading(string message, float durationAutoHide = 1.5f, bool showLoadingImage = true)
        {
            instance.DOShow(message, durationAutoHide, showLoadingImage);
        }

        public static void SetUpdate(string message, float process, float durationTime, bool hideOnDone = false)
        {
            instance.DOSetUpdate(message, process, durationTime, hideOnDone);
        }

        public void DOSetUpdate(string message, float process, float durationTime, bool hideOnDone = false)
        {
            if (!string.IsNullOrEmpty(message))
                message = message.Length > maxLenght ? message.Substring(0, maxLenght) + "..." : message;
            content.SetActive(true);
            this.message.text = instance.isToUpperCase ? message.ToUpper() : message;
            loadingSlider.DOKill();
            TweenValue(loadingSlider, process, durationTime).OnComplete(() =>
            {
                if (hideOnDone || process >= 1)
                    HideLoading();
            });
        }

        public static void ChangeImage()
        {
            if (instance)
                instance.DOChangeImage();
        }

        public void DOChangeImage()
        {
            if (loadingSpriteInReoucesFolder != null && loadingSpriteInReoucesFolder.Length > 0)
            {
                loadingSpriteIndex %= loadingSpriteInReoucesFolder.Length;
                var sprite = Resources.Load<Sprite>(loadingSpriteInReoucesFolder[loadingSpriteIndex]);
                if (sprite != null)
                    loadingImage.sprite = sprite;
                loadingSpriteIndex++;
            }
        }

        public void DOShow(string message, float durationAutoHide, bool showLoadingImage)
        {
            if (showLoadingImage)
                DOChangeImage();

            loadingObject.SetActive(showLoadingImage);
            if (loadingImage.sprite && aspectRatioFitter && aspectRatioFitter.aspectMode != AspectRatioFitter.AspectMode.None)
                aspectRatioFitter.aspectRatio = loadingImage.sprite.rect.width / loadingImage.sprite.rect.height;

            if (string.IsNullOrEmpty(message) && messages != null && messages.Length > 0)
            {
                messageIndex %= messages.Length;
                message = messages[messageIndex];
                messageIndex++;
            }

            if (string.IsNullOrEmpty(message))
                message = isToUpperCase ? messageDefault.ToUpper() : messageDefault;

            if (!string.IsNullOrEmpty(message))
                message = message.Length > instance.maxLenght ? message.Substring(0, instance.maxLenght) + "..." : message;

            this.message.text = isToUpperCase ? message.ToUpper() : message;

            content.SetActive(true);

            loadingSlider.DOKill();
            TweenValue(loadingSlider, 1, durationAutoHide)
                .OnComplete(() =>
                {
                    Hide();
                });
        }

        protected void Hide(float durationTime = 0.25f)
        {
            loadingSlider.DOKill();
            if (loadingSlider.value != 1)
            {
                TweenValue(loadingSlider, 1, durationTime)
                    .SetUpdate(UpdateType.Normal, true)
                    .OnComplete(() =>
                    {
                        loadingSlider.value = 0;
                        loadingObject.SetActive(false);
                        content.SetActive(false);
                    });
            }
            else
            {
                loadingSlider.value = 0;
                loadingObject.SetActive(false);
                content.SetActive(false);
            }
        }

        public static void HideLoading()
        {
            instance.Hide();
        }

        /// <summary>Slider.DOValue with DOTween's core only (Base needs DOTween.dll, not the DOTween.Modules asmdef).</summary>
        private static Tweener TweenValue(Slider slider, float to, float duration) =>
            DOTween.To(() => slider.value, v => slider.value = v, to, duration).SetTarget(slider);
    }
}
