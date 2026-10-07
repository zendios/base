using System;
using UnityEngine;
using UnityEngine.UI;

public class HapticFeedback : MonoBehaviour
{
    [SerializeField]
    protected Toggle hapticToggle = null;

    protected static int isOn = -1;
    public static int IsOn
    {
        get
        {
            if (isOn == -1)
                isOn = PlayerPrefs.GetInt("HapticFeedback", 1);
            return isOn;
        }
        set
        {
            if (value != isOn)
            {
                isOn = value;
                PlayerPrefs.SetInt("HapticFeedback", isOn);
                PlayerPrefs.Save();
            }
        }
    }

    protected static float vibrationDelay = 0.01f;

    protected static float lastTime;

    public static HapticFeedback instance = null;

    protected void Awake()
    {
        instance = this;
    }

    public static void CheckIsOn()
    {
        if (instance != null && instance.hapticToggle)
        {
            instance.hapticToggle.onValueChanged.AddListener(instance.OnToggleChanged);
            instance.hapticToggle.isOn = IsOn > 0;
        }
    }

    private void OnToggleChanged(bool isOn)
    {
        if (isOn)
            PlayImpact(ImpactStyle.Light);
    }

    private void OnEnable()
    {
        if (hapticToggle)
            hapticToggle.isOn = IsOn > 0;
    }

    private void Start()
    {
        lastTime = Time.time;
    }

    public enum ImpactStyle { Light = 0, Medium = 1, Heavy = 2, Rigid = 3, Soft = 4 }

    public static void PlayImpact(ImpactStyle style)
    {
#if UNITY_IOS
        iOSHapticController.PlayImpact(style);
#elif UNITY_ANDROID
        AndroidHapticController.PlayImpact(style);
#endif
    }
}
