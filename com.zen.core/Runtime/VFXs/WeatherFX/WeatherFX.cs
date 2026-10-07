using System.Collections.Generic;
using UnityEngine;

namespace Base.Core
{
    public class WeatherFX : MonoBehaviour
    {
        [SerializeField] List<GameObject> prefabsFxList = new List<GameObject>();

        private void Start()
        {
            GameStateManager.OnGameStateChanged += OnGameStateChanged;
        }
        private void OnDestroy()
        {
            GameStateManager.OnGameStateChanged -= OnGameStateChanged;
        }

        private void OnGameStateChanged(GameState current, GameState last, object data = null)
        {
            if (current == GameState.Play || current == GameState.Resume)
                RandomFX();
        }

        public void RandomFX()
        {
            if (prefabsFxList.Count > 0)
            {
                var randomIndex = Random.Range(0, prefabsFxList.Count);
                SetFX(randomIndex);
            }
        }

        public void SetFX(int index)
        {
            if (prefabsFxList.Count > 0)
            {
                index = index % prefabsFxList.Count;
                for (int i = 0; i < prefabsFxList.Count; i++)
                {
                    if (prefabsFxList[i] != null)
                        prefabsFxList[i].SetActive(index == i);
                }

            }
        }
    }
}
