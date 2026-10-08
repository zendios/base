using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Message popup: title, message, Confirm button and optional Cancel button (the close button).
/// UIPopupMessage.Show("Error", "No connection. Try again?", onConfirm: Retry, confirmLabel: "Retry", cancelLabel: "Close");
/// Confirm runs onConfirm then hides; Cancel runs onCancel then hides. Showing it again while open replaces the content.
/// </summary>
public class UIPopupMessage : UIPopupBase<UIPopupMessage>
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TextMeshProUGUI confirmButtonText;

    private Action onConfirm, onCancel;
    private bool wired;

    /// <param name="title">Title (required, always shown).</param>
    /// <param name="message">Body text.</param>
    /// <param name="onConfirm">Runs when Confirm is pressed (the popup starts hiding first).</param>
    /// <param name="confirmLabel">Confirm button text.</param>
    /// <param name="onCancel">Runs when Cancel is pressed, before the popup hides.</param>
    /// <param name="cancelLabel">Cancel button text; with neither cancelLabel nor onCancel the Cancel button is hidden.</param>
    public static UIPopupMessage Show(string title, string message, Action onConfirm = null, string confirmLabel = "OK",
        Action onCancel = null, string cancelLabel = null)
    {
        var popup = Show();
        if (popup != null)
            popup.Set(title, message, onConfirm, confirmLabel, onCancel, cancelLabel);
        return popup;
    }

    private void Set(string title, string message, Action confirm, string confirmLabel, Action cancel, string cancelLabel)
    {
        Wire();
        onConfirm = confirm;
        onCancel = cancel;

        if (string.IsNullOrEmpty(title))
            Debug.LogWarning("[UIPopupMessage] Show needs a title.");
        if (titleText != null)
            titleText.text = title ?? "";
        if (messageText != null)
            messageText.text = message ?? "";
        if (confirmButtonText != null)
            confirmButtonText.text = string.IsNullOrEmpty(confirmLabel) ? "OK" : confirmLabel;

        if (closeButton != null)
        {
            bool showCancel = cancel != null || !string.IsNullOrEmpty(cancelLabel);
            closeButton.gameObject.SetActive(showCancel);
            var label = closeButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (showCancel && label != null && !string.IsNullOrEmpty(cancelLabel))
                label.text = cancelLabel;
        }
    }

    /// <summary>Button listeners, once: Confirm runs onConfirm and hides; Cancel (UIPopupBase already hides on it) runs onCancel.</summary>
    private void Wire()
    {
        if (wired) return;
        wired = true;
        if (confirmButton != null)
            confirmButton.onClick.AddListener(() =>
            {
                var action = onConfirm;
                onConfirm = onCancel = null;
                Hide();
                action?.Invoke();
            });
        if (closeButton != null)
            closeButton.onClick.AddListener(() =>
            {
                var action = onCancel;
                onConfirm = onCancel = null;
                action?.Invoke();
            });
    }
}
