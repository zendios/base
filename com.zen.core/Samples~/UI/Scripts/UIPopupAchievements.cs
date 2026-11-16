using System.Collections.Generic;
using Base;
using UnityEngine;

/// <summary>
/// Achievements: one UIDailyMissionRow per group (its lowest unclaimed tier). Prefab: Resources/UIPopupAchievements, a
/// Popup_Base variant: the game copies and restyles it. The logic is Achievements (package).
/// </summary>
public class UIPopupAchievements : UIPopupBase<UIPopupAchievements>
{
    [SerializeField] private RectTransform rowRoot;
    [Tooltip("Hidden template, cloned per achievement.")]
    [SerializeField] private UIDailyMissionRow rowTemplate;

    private readonly List<UIDailyMissionRow> rows = new List<UIDailyMissionRow>();

    protected override void Awake()
    {
        base.Awake();
        rowTemplate.gameObject.SetActive(false);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Achievements.OnChanged += Refresh;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Achievements.OnChanged -= Refresh;
    }

    protected override void OnShow() => Refresh();

    private void Refresh()
    {
        if (!Achievements.IsAvailable)
        {
            Debug.LogWarning("[UIPopupAchievements] no AchievementDatas in DataManager.itemDatas, or the feature is off.");
            Close();
            return;
        }

        var visible = Achievements.Visible;
        while (rows.Count < visible.Count)
        {
            var row = Instantiate(rowTemplate, rowRoot);
            row.gameObject.SetActive(true);
            rows.Add(row);
        }
        for (int i = 0; i < rows.Count; i++)
        {
            bool shown = i < visible.Count;
            rows[i].gameObject.SetActive(shown);
            if (shown)
            {
                var achievement = visible[i];
                rows[i].Set(achievement, Achievements.Progress(achievement), Achievements.IsClaimed(achievement),
                    Achievements.Claim);
            }
        }
    }
}
