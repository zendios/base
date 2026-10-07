using System;
using UnityEngine;

namespace Base
{
    [Serializable]
    public class ItemBase : ItemData
    {
        public Sprite thumbnail
        {
            get
            {
                var _thumbnail = Resources.Load<Sprite>(resoucePath + "/" + id);
                if (_thumbnail == null)
                    Debug.LogError(id + " NOT FOUND in Resources");
                return _thumbnail;
            }
        }

        public GameObject prefab
        {
            get
            {
                var _prefab = Resources.Load<GameObject>(resoucePath + "/" + id);
                if (_prefab == null)
                    Debug.LogError(id + " NOT FOUND in Resources");
                return _prefab;
            }
        }

        [SerializeField]
        internal string resoucePath = null;

#if UNITY_EDITOR
        [SerializeField]
        internal GameObject prefabInResource = null;
#endif
    }
}