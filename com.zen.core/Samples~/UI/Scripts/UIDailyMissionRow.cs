using System;
using Base;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One mission or achievement (prefab DailyMission_Row): title, progress bar, x/y, reward, Claim or Done. The popup
/// passes the claim call (DailyMissions.Claim or Achievements.Claim).
/// </summary>
public class UIDailyMissionRow : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [Tooltip("Image of type Filled (horizontal).")]
    [SerializeField] private Image progressFill;
    [SerializeField] private TMP_Text progressText;
    [Tooltip("The coin icon: shown for coin rewards or when the mission has its own icon.")]
    [SerializeField] private Image rewardIcon;
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private Button claimButton;
    [SerializeField] private GameObject doneMark;

    private string id;
    private Func<string, Transform, bool> claim;

    private void Awake() => claimButton.onClick.AddListener(() => claim(id, claimButton.transform));

    public void Set(MissionData mission, int value, bool claimed, Func<string, Transform, bool> claim)
    {
        id = mission.id;
        this.claim = claim;
        titleText.text = mission.Title;
        progressText.text = value + "/" + mission.target;
        progressFill.fillAmount = (float)value / mission.target;
        rewardText.text = SampleRewards.Label(mission.reward);
        if (mission.icon != null)
            rewardIcon.sprite = mission.icon;
        rewardIcon.enabled = mission.icon != null || mission.reward.type == CurrencyType.Soft;
        claimButton.gameObject.SetActive(!claimed && value >= mission.target);
        doneMark.SetActive(claimed);
    }
}
