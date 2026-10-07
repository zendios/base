using Base.Ads;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class DebugMode : MonoBehaviour
{
    public bool debugMode = true;
    protected static int count = 0;

    [SerializeField]
    private Button showHideButton;

    [SerializeField]
    private GameObject group;

    public static int Count
    {
        get => count;
        set
        {
            count = value;
            if (count == 10)
            {
                IsOn = true;
                //AdToast.ShowNotice("Debug mode is ON");
            }
            else if (count > 10)
            {
                count = 0;
                IsOn = false;
            }
        }
    }

    protected static int isOn = -1;
    public static bool IsOn
    {
        get
        {
            if (isOn == -1)
                isOn = PlayerPrefs.GetInt("DebugModeIsOn", 0);
            return isOn == 1;
        }
        set
        {
            if (!value)
                count = 0;

            isOn = value ? 1 : 0;
            PlayerPrefs.SetInt("DebugModeIsOn", isOn);
            PlayerPrefs.Save();

            OnChanged?.Invoke(isOn == 1);
            if (isOn == 1)
                instance.debugIsOn?.Invoke();
            else
                instance.debugIsOff?.Invoke();

            instance.group.SetActive(true);

            Debug.Log("Debug mode is " + (isOn == 1 ? "ON" : "OFF"));
        }
    }

    public delegate void OnDelegateChanged(bool value);
    public static OnDelegateChanged OnChanged;

    public UnityEvent debugIsOn = null;
    public UnityEvent debugIsOff = null;

    protected static DebugMode instance = null;

    protected void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        if (debugMode)
            IsOn = debugMode;
        if (showHideButton)
            showHideButton.onClick.AddListener(ShowHide);
        OnChanged?.Invoke(IsOn);
        if (IsOn)
            instance.debugIsOn?.Invoke();
        else
            instance.debugIsOff?.Invoke();
    }

    public void ShowHide()
    {
        group.SetActive(false);
    }
}
