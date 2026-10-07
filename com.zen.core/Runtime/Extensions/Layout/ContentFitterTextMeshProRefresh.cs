using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(ContentSizeFitter))]
public class ContentFitterTextMeshProRefresh : MonoBehaviour
{
    [SerializeField]
    protected TMPro.TextMeshProUGUI contentText = null;

    RectTransform rectTransform = null;
    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void Start()
    {
        try
        {
            if (contentText != null)
            {
                contentText.RegisterDirtyLayoutCallback(RefreshContentFitters);
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    protected void OnDestroy()
    {
        try
        {
            if (contentText != null)
            {
                contentText.UnregisterDirtyLayoutCallback(RefreshContentFitters);
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    private void OnEnable()
    {
        RefreshContentFitters();
    }

    public void RefreshContentFitters()
    {
        if (rectTransform)
            RefreshContentFitter(rectTransform);
    }

    private void RefreshContentFitter(RectTransform rect)
    {
        if (rect == null || !rect.gameObject.activeSelf)
        {
            return;
        }

        foreach (RectTransform child in rect)
        {
            RefreshContentFitter(child);
        }

        var layoutGroup = rect.GetComponent<LayoutGroup>();
        var contentSizeFitter = rect.GetComponent<ContentSizeFitter>();
        if (layoutGroup != null)
        {
            layoutGroup.SetLayoutHorizontal();
            layoutGroup.SetLayoutVertical();
        }

        if (contentSizeFitter != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }
    }
}