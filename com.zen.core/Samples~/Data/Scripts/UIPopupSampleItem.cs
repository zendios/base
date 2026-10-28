using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A custom popup: a Popup_Base variant in Resources named like the class, with its own texts and buttons.
/// It only shows and forwards clicks; the caller owns the logic (pass null to hide an action).
/// UIPopupSampleItem.Show("Truck", "Owned", buy: null, select: Select, upgrade: Upgrade);
/// </summary>
public class UIPopupSampleItem : UIPopupBase<UIPopupSampleItem>
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text infoText;
    [SerializeField] private Button buyButton;
    [SerializeField] private Button selectButton;
    [SerializeField] private Button upgradeButton;

    private Action _buy, _select, _upgrade;

    protected override void Awake()
    {
        base.Awake();
        buyButton.onClick.AddListener(() => Run(_buy));
        selectButton.onClick.AddListener(() => Run(_select));
        upgradeButton.onClick.AddListener(() => Run(_upgrade));
    }

    /// <summary>Shows the popup with the given content; a null action hides its button.</summary>
    public static UIPopupSampleItem Show(string title, string info, Action buy, Action select, Action upgrade)
    {
        var popup = Show();
        if (popup != null)
            popup.Set(title, info, buy, select, upgrade);
        return popup;
    }

    private void Set(string title, string info, Action buy, Action select, Action upgrade)
    {
        titleText.text = title;
        infoText.text = info;
        _buy = buy;
        _select = select;
        _upgrade = upgrade;
        buyButton.gameObject.SetActive(buy != null);
        selectButton.gameObject.SetActive(select != null);
        upgradeButton.gameObject.SetActive(upgrade != null);
    }

    private void Run(Action action)
    {
        Close();
        action?.Invoke();
    }
}
