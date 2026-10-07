using UnityEngine;
using UnityEngine.Events;

public class DebugModeListenner : MonoBehaviour
{
    public UnityEvent debugIsOn = null;
    public UnityEvent debugIsOff = null;

    private void Awake()
    {
        DebugMode.OnChanged += DebugModeOnChanged;
    }

    private void OnDestroy()
    {
        DebugMode.OnChanged -= DebugModeOnChanged;
    }

    protected void DebugModeOnChanged(bool isOn)
    {
        if (isOn)
        {
            debugIsOn?.Invoke();
        }
        else
        {
            debugIsOff?.Invoke();
        }
    }
}
