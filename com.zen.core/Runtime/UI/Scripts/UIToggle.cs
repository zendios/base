using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public class UIToggle : MonoBehaviour
{
    public Toggle toggle = null;
    [SerializeField]
    private TextMeshProUGUI status = null;
    [SerializeField]
    private string keyOn = "On";
    [SerializeField]
    private Color keyOnColor = Color.white;
    [SerializeField]
    private string keyOff = "Off";
    [SerializeField]
    private Color keyOffColor = Color.gray;
    [SerializeField]
    private bool autoSaveLoad = false;

    [Header("Events On Changed")]
    [SerializeField]
    private UnityEvent isOnEvent = new UnityEvent();
    [SerializeField]
    private UnityEvent isOffEvent = new UnityEvent();

    protected static UIToggle instance = null;

    public virtual void Awake()
    {
        instance = this;

        if (toggle == null)
            toggle = GetComponent<Toggle>();

        if (toggle != null)
        {
            if (autoSaveLoad)
                toggle.isOn = PlayerPrefs.GetInt(name, toggle.isOn ? 1 : 0) == 1 ? true : false;
            OnValueChanged(toggle.isOn);
        }
    }

    public virtual void Start()
    {
        toggle.onValueChanged.AddListener(OnValueChanged);
        UpdateStatus();
    }

    public virtual void OnChangedAction(UnityAction<bool> action)
    {
        if (toggle && action != null)
            toggle.onValueChanged.AddListener(action);
    }

    public virtual void OnValueChanged(bool isOn)
    {
        if (autoSaveLoad)
        {
            PlayerPrefs.SetInt(name, isOn ? 1 : 0);
            PlayerPrefs.Save();
        }

        if (isOn)
            isOnEvent?.Invoke();
        else
            isOffEvent?.Invoke();

        UpdateStatus();
    }

    public bool isOn
    {
        get
        {
            return toggle.isOn;
        }
        set
        {
            if (toggle)
                toggle.isOn = value;
        }
    }

    private void UpdateStatus()
    {
        if (status)
        {
            status.text = isOn ? keyOn : keyOff;
            status.color = isOn ? keyOnColor : keyOffColor;
        }
    }
}
