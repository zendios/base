using Base;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The pause popup (GameFlow opens it at Pause): Resume, Settings, Home. Closing it in any way (Resume, the back key)
/// resumes with GameStateManager.Play(), never through Resume (Missions would count a new game). Home leaves the game
/// for the main screen without a Win / Lose (the game is not counted).
/// </summary>
public class UIPopupPause : UIPopupBase<UIPopupPause>
{
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button homeButton;

    protected override void Awake()
    {
        base.Awake();
        resumeButton.onClick.AddListener(() => Close());
        settingsButton.onClick.AddListener(() => UIPopupSettings.Show());
        homeButton.onClick.AddListener(GoHome);
    }

    protected override void OnHide()
    {
        if (GameStateManager.CurrentState == GameState.Pause)
            GameStateManager.Play();
    }

    private void GoHome()
    {
        GameStateManager.Main();
        Close();
    }
}
