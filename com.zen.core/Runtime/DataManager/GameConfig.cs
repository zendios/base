using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Base
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "DataAsset/GameConfig")]
    [Serializable]
    public class GameConfig : GameConfigBase
    {
        [Header("TODO: add more properties in here!")]
        public int autoNextLevel;
    }
}
