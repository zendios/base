using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Base
{
    /// <summary>The game's config: a plain serializable class saved inside GameData (DataManager.GameConfig), filled by
    /// Remote Config (GameConfigRemote). Not an asset. Add the game's own fields here.</summary>
    [Serializable]
    public class GameConfig : GameConfigBase
    {
        public int autoNextLevel;
    }
}
