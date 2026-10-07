using Base;
using Base.Ads;
using System;
using UnityEngine;
using UnityEngine.UI;

public class ButtonWatchAdToGetCurrency : MonoBehaviour
{
    [SerializeField] protected Button button = null;
    [SerializeField] protected CurrencyType currency = CurrencyType.Soft;
    [HideInInspector]
    [SerializeField]
    public int reward = 200;
    [SerializeField] protected int maxInSession = 5;
    protected int totalInSession = 0;
    [SerializeField] protected string placement = "default";
    [SerializeField] protected Text rewardLabel = null;
    [SerializeField] protected Transform currencyFrom = null;

    public Action OnGetSuccess;
    private void Awake()
    {
        if (button == null)
            TryGetComponent(out button);
        button.onClick.AddListener(GetByAd);

        if (currencyFrom == null)
            currencyFrom = transform.GetChild(0);
    }

    public void CheckTotalInSession()
    {
        if (totalInSession < maxInSession)
            gameObject.SetActive(true);
        else
            gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        UpdateRewardLabel();
    }

    public void UpdateRewardLabel()
    {
        reward = AdsManager.SoftCurrencyByReward;

        if (maxInSession <= 1)
            reward *= 2;

        if (rewardLabel != null)
            rewardLabel.text = reward.ToString();
    }

    public void GetByAd()
    {
        AdsManager.ShowReward((type, status) =>
        {
            if (status == AdState.ShowSuccess)
            {
                totalInSession++;
                if (currency == CurrencyType.Hard)
                    CurrencyManager.AddHard(reward, name, currencyFrom);
                else
                    CurrencyManager.AddSoft(reward, name, currencyFrom);
                CheckTotalInSession();
                OnGetSuccess?.Invoke();
            }
        }, name);
    }
}
