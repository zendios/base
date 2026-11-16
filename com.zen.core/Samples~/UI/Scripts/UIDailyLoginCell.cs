using Base;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One day of the daily login calendar (prefab DailyLogin_Cell): icon, reward, "Day n", claimed mark, today frame.</summary>
public class UIDailyLoginCell : MonoBehaviour
{
    [Tooltip("The coin icon: shown for coin rewards or when the day has its own icon.")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private GameObject claimedMark;
    [SerializeField] private GameObject todayFrame;

    public void Set(int day, DailyLoginData data, bool claimed, bool today)
    {
        dayText.text = "Day " + day;
        amountText.text = SampleRewards.Label(data.reward);
        if (data.icon != null)
            icon.sprite = data.icon;
        icon.enabled = data.icon != null || data.reward.type == CurrencyType.Soft;
        icon.transform.localScale = data.isSpecial ? Vector3.one * 1.3f : Vector3.one;
        claimedMark.SetActive(claimed);
        todayFrame.SetActive(today);
    }
}
