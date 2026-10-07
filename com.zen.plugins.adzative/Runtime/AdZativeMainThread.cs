using System;
using System.Collections.Generic;
using UnityEngine;

namespace Base.Ads
{
    /// <summary>
    /// Marshals native callbacks (Android UI thread) onto Unity's main thread, and routes the
    /// Android back key (Escape) to a fullscreen ad. Because ads never steal window focus,
    /// the back key keeps arriving in Unity — this is where we hand it to the ad.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    internal sealed class AdZativeMainThread : MonoBehaviour
    {
        private static AdZativeMainThread _instance;
        private static readonly Queue<Action> Queue = new Queue<Action>();
        private static readonly object Lock = new object();
        private readonly List<Action> _batch = new List<Action>(16);

        internal static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("[AdZativeSDK]");
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<AdZativeMainThread>();
        }

        internal static void Enqueue(Action a)
        {
            lock (Lock) Queue.Enqueue(a);
        }

        private void Update()
        {
            lock (Lock)
            {
                while (Queue.Count > 0) _batch.Add(Queue.Dequeue());
            }
            for (int i = 0; i < _batch.Count; i++)
            {
                try { _batch[i](); }
                catch (Exception e) { Debug.LogException(e); }
            }
            _batch.Clear();

            if (AdZativeSDK.IsFullscreenShowing && BackPressed()) AdZativeSDK.HandleBackKey();
        }

        private static bool BackPressed()
        {
#if ZEN_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;   // new Input System only: back arrives via the Keyboard device above
#endif
        }
    }
}
