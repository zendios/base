using System;
using UnityEngine;

namespace Base
{
    /// <summary>
    /// A RectTransform with its own place, size and visibility in portrait and in landscape. Lay the element out for one
    /// orientation in the editor (Game view resolution), press "Save as Portrait" / "Save as Landscape" in the
    /// inspector, then the other. At runtime it switches when the screen rotates. For a different arrangement of
    /// many children (row vs column), keep two containers and switch them with "active". The object must be active
    /// when the scene loads (once awake it keeps switching, hidden or not).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class OrientationLayout : MonoBehaviour
    {
        [Serializable]
        public class Pose
        {
            public bool saved;
            public bool active = true;
            public Vector2 anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta;
            public Vector3 localScale = Vector3.one;
            public float rotationZ;
        }

        public Pose portrait = new Pose();
        public Pose landscape = new Pose();

        private RectTransform rect;
        private bool? applied;

        private RectTransform Rect => rect != null ? rect : rect = (RectTransform)transform;

        // Canvas.preWillRenderCanvases runs every frame whether this object is active or not, so a pose can hide the
        // object in one orientation and show it again in the other.
        private void Awake()
        {
            if (!Application.isPlaying)
                return;
            Canvas.preWillRenderCanvases += Check;
            Apply(true);
        }

        private void OnDestroy() => Canvas.preWillRenderCanvases -= Check;

        private void Check() => Apply(false);

        private void Apply(bool force)
        {
            if (!Application.isPlaying)
                return;          // edit mode: the inspector buttons save / preview
            bool isPortrait = ScreenMetrics.IsPortrait;
            if (!force && applied == isPortrait)
                return;
            applied = isPortrait;
            Load(isPortrait ? portrait : landscape);
        }

        public void Load(Pose pose)
        {
            if (pose == null || !pose.saved)
                return;
            var r = Rect;
            r.anchorMin = pose.anchorMin;
            r.anchorMax = pose.anchorMax;
            r.pivot = pose.pivot;
            r.anchoredPosition = pose.anchoredPosition;
            r.sizeDelta = pose.sizeDelta;
            r.localScale = pose.localScale;
            r.localEulerAngles = new Vector3(0, 0, pose.rotationZ);
            if (gameObject.activeSelf != pose.active && Application.isPlaying)
                gameObject.SetActive(pose.active);
        }

        public void Save(Pose pose)
        {
            var r = Rect;
            pose.saved = true;
            pose.active = gameObject.activeSelf;
            pose.anchorMin = r.anchorMin;
            pose.anchorMax = r.anchorMax;
            pose.pivot = r.pivot;
            pose.anchoredPosition = r.anchoredPosition;
            pose.sizeDelta = r.sizeDelta;
            pose.localScale = r.localScale;
            pose.rotationZ = r.localEulerAngles.z;
        }
    }
}
