using System;
using UnityEngine;

namespace Base
{
    /// <summary>Short on-screen notices for every package; com.zen.ads shows them with AdToast, else they go to the log.</summary>
    public static class ZenToast
    {
        public static Action<string> Backend { get; set; }

        public static void ShowNotice(string message)
        {
            if (Backend != null)
                Backend(message);
            else
                Debug.Log("[ZenToast] " + message);
        }
    }
}
