using Base;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteInEditMode]
public class CurrencyManager : MonoBehaviour
{
    protected static CurrencyManager instance = null;

    [SerializeField]
    protected Button softButton = null;
    [SerializeField]
    protected TextMeshProUGUI softLabel = null;
    [SerializeField]
    protected string softName = "Cash";
    [SerializeField]
    public static string SoftName
    {
        get
        {
            if (instance == null)
                return "Cash";
            return instance.softName;
        }
    }
    [SerializeField]
    protected ParticleLookAt softParticle = null;
    [SerializeField]
    protected Transform softFXPos = null;
    protected static int SoftChanged = 0;

    protected static int totalSoft = 0;
    public static int TotalSoft
    {
        get
        {
            if (DataManager.UserData != null)
            {
                return DataManager.UserData.totalSoftCurrency;
            }
            return totalSoft;
        }
        set
        {
            if (DataManager.UserData != null && DataManager.UserData.totalSoftCurrency != value)
            {
                DataManager.UserData.totalSoftCurrency = value;
            }
            else
            {
                totalSoft = value;
            }
        }
    }

    public static int SoftEarned = 0;
    public static int HardEarned = 0;


    [Header("OPTION Hard Currency")]
    [SerializeField]
    protected Button hardButton = null;
    [SerializeField]
    protected TextMeshProUGUI hardLabel = null;
    [SerializeField]
    protected string hardName = "Token";
    [SerializeField]
    public static string HardName
    {
        get
        {
            if (instance == null)
                return "Token";
            return instance.hardName;
        }
    }
    [SerializeField]
    protected ParticleLookAt hardParticle = null;
    [SerializeField]
    protected Transform hardFXPos = null;



    protected static int HardChanged = 0;

    protected static int totalHard = 0;
    public static int TotalHard
    {
        get
        {
            if (DataManager.UserData != null)
            {
                return DataManager.UserData.totalHardCurrency;
            }
            return totalHard;
        }
        set
        {
            if (DataManager.UserData != null && DataManager.UserData.totalHardCurrency != value)
            {
                DataManager.UserData.totalHardCurrency = value;
            }
            else
            {
                totalHard = value;
            }
        }
    }



    private void Awake()
    {
        instance = this;
        DataManagerOnLoaded();
        DataManager.OnLoaded += DataManagerOnLoaded;
        GameStateManager.OnGameStateChanged += OnGameStateChanged;
    }

    private void OnGameStateChanged(GameState current, GameState last, object data = null)
    {
        if (current == GameState.Main || current == GameState.Restart || current == GameState.Next || current == GameState.Init)
        {
            SoftEarned = 0;
            HardEarned = 0;

            if (current == GameState.Init || current == GameState.Main)
                DataManagerOnLoaded();
        }
    }

    private void DataManagerOnLoaded()
    {
        if (softLabel != null)
        {
            softLabel.text = "0";
            softLabel.DONumber(0, TotalSoft);
        }
        if (hardLabel != null)
        {
            hardLabel.text = "0";
            hardLabel.DONumber(0, TotalHard);
        }
    }

    private void Start()
    {
        if (softParticle != null)
        {
            //totalSoftParticle.transform.SetParent(null, false);
            softParticle.OnTarget += TotalSoftParticleOnTarget;
            softParticle.OnEmitDone += TotalSoftParticleOnEmitDone;
        }
        if (hardParticle != null)
        {
            //totalHardParticle.transform.SetParent(null, false);
            hardParticle.OnTarget += TotalHardParticleOnTarget;
            hardParticle.OnEmitDone += TotalHardParticleOnEmitDone;
        }

        if (softButton)
        {
            softButton.onClick.AddListener(() => ZenStore.OpenShop?.Invoke());
        }

        if (hardButton)
        {
            hardButton.onClick.AddListener(() => ZenStore.OpenShop?.Invoke());
        }
    }

    private void TotalSoftParticleOnTarget(int obj)
    {
        //Debug.Log("TotalSoftParticleOnTarget: " + obj);
        if (SoftChanged > 0)
            instance.softLabel.DONumber(TotalSoft - SoftChanged, TotalSoft);
        SoftChanged = 0;
    }

    private void TotalSoftParticleOnEmitDone(int obj)
    {
        //Debug.Log("TotalSoftParticleOnEmitDone: " + obj);
    }

    private void TotalHardParticleOnTarget(int obj)
    {
        //Debug.Log("TotalSoftParticleOnTarget: " + obj);
        if (HardChanged > 0)
            instance.hardLabel.DONumber(TotalHard - HardChanged, TotalHard);
        HardChanged = 0;

    }

    private void TotalHardParticleOnEmitDone(int obj)
    {
        //Debug.Log("TotalHardParticleOnEmitDone: " + obj);
    }

    public void AddSoftOnClick(int value)
    {
        AddSoft(value, "SoftOnClick", instance.softButton.transform);
    }

    public static void AddSoft(int value, string itemName, Transform from, Transform end = null, bool showFx = true)
    {
        if (value > 1000000)
        {
            Debug.LogWarning($"CurrencyManager.AddSoft {value} ({itemName}) blocked by the 1000000 cap. Purchases: use GrantFromPurchase.");
            return;
        }

        Add(value, itemName, CurrencyType.Soft, from, end, showFx);
    }

    public void AddHardOnClick(int value)
    {
        AddHard(value, "HardOnClick", instance.hardButton.transform);
    }

    public static void AddHard(int value, string itemName, Transform from, Transform end = null, bool showFx = true)
    {
        if (value > 1000)
        {
            Debug.LogWarning($"CurrencyManager.AddHard {value} ({itemName}) blocked by the 1000 cap. Purchases: use GrantFromPurchase.");
            return;
        }

        Add(value, itemName, CurrencyType.Hard, from, end, showFx);
    }

    /// <summary>
    /// Paid currency: no anti-cheat cap (a pack can be larger than AddSoft / AddHard allow) and written to UserData even
    /// when no CurrencyManager is in the scene (no FX then). Returns false when nothing could be granted.
    /// </summary>
    public static bool GrantFromPurchase(int value, CurrencyType currency, string itemName, Transform from, Transform end = null)
    {
        if (value <= 0 || (currency != CurrencyType.Soft && currency != CurrencyType.Hard) || DataManager.UserData == null)
            return false;

        if (instance != null)
        {
            Add(value, itemName, currency, from, end, true);
        }
        else if (currency == CurrencyType.Soft)
        {
            DataManager.UserData.totalSoftCurrency += value;
            if (ZenAnalytics.IsReady)
                ZenAnalytics.LogResourceEarn(value, "Soft", CurrencyType.Soft, itemName);
        }
        else
        {
            DataManager.UserData.totalHardCurrency += value;
            if (ZenAnalytics.IsReady)
                ZenAnalytics.LogResourceEarn(value, HardName, CurrencyType.Hard, itemName);
        }
        return true;
    }

    public static void Add(int value, string itemName, CurrencyType currency, Transform start, Transform end = null, bool showFx = true)
    {
        if (instance != null)
        {
            if (currency == CurrencyType.Soft)
            {
                if (ZenAnalytics.IsReady)
                {
                    if (value > 100)
                        ZenAnalytics.LogResourceEarn(value, instance.softName, CurrencyType.Soft, itemName);
                    else if (value < -100)
                        ZenAnalytics.LogResourceSpend(value, instance.softName, CurrencyType.Soft, itemName);
                }

                SoftChanged = value;

                var current = TotalSoft;
                TotalSoft += value;

                if (TotalSoft < 0)
                    TotalSoft = 0;

                if (value > 0 || end != null)
                {
                    if (instance.softParticle != null && showFx)
                        instance.softParticle.Emit(start, end != null ? end : instance.softFXPos, SoftChanged);
                    else
                        instance.softLabel.DONumber(current, TotalSoft);
                }
                else
                {
                    instance.softLabel.DONumber(current, TotalSoft);
                }
            }
            else
            {
                if (ZenAnalytics.IsReady)
                {
                    if (value > 10)
                        ZenAnalytics.LogResourceEarn(value, instance.hardName, CurrencyType.Hard, itemName);
                    else if (value < -10)
                        ZenAnalytics.LogResourceSpend(value, instance.hardName, CurrencyType.Hard, itemName);
                }

                HardChanged = value;

                var current = TotalHard;
                TotalHard += value;

                if (TotalHard < 0)
                    TotalHard = 0;

                if (value > 0 || end != null)
                {
                    if (instance.hardParticle != null && showFx)
                        instance.hardParticle.Emit(start, end != null ? end : instance.hardFXPos, HardChanged);
                    else
                        instance.hardLabel.DONumber(current, TotalHard);
                }
                else
                {
                    instance.hardLabel.DONumber(current, TotalHard);
                }
            }
        }
    }

    public static int ConvertSortToHard(int price)
    {
        return Mathf.FloorToInt(price / 10);
    }

    public static int ConvertVIPToHard(int price)
    {
        return Mathf.FloorToInt(price * 100);
    }
}