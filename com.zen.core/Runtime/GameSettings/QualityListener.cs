using UnityEngine;

namespace Base
{
    /// <summary>
    /// Turns objects on from a graphics quality level up (heavy effects, extra props) and others on below it (cheap
    /// fallbacks). Follows GameSettings.Quality; levels are QualitySettings.names indexes (0 = lowest).
    /// </summary>
    public class QualityListener : MonoBehaviour
    {
        [Tooltip("Lowest QualitySettings level (0 = lowest) at which the 'from level' objects are on.")]
        [Min(0)] [SerializeField] private int minLevel = 2;
        [Tooltip("On at minLevel and above.")]
        [SerializeField] private GameObject[] onFromLevel;
        [Tooltip("On below minLevel.")]
        [SerializeField] private GameObject[] onBelowLevel;

        private void OnEnable()
        {
            GameSettings.OnQualityChanged += Apply;
            Apply(QualitySettings.GetQualityLevel());
        }

        private void OnDisable() => GameSettings.OnQualityChanged -= Apply;

        private void Apply(int level)
        {
            bool high = level >= minLevel;
            foreach (GameObject target in onFromLevel)
                if (target != null)
                    target.SetActive(high);
            foreach (GameObject target in onBelowLevel)
                if (target != null)
                    target.SetActive(!high);
        }
    }
}
