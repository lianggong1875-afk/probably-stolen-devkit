using System;
using TMPro;
using UnityEngine;

namespace ModTemplates
{
    /// <summary>
    /// 让自定义物品接入原版放大镜检视界面（双树镜像 + LensFollow 镜头同步）。
    /// 来源蒸馏：Appraiser+ (InspectView + LensFollow)。零能力。
    /// 用法：在 InspectionUIManager.OnInspectItem Postfix 调用 Show()，在 CloseUI / OnDisable 调用 Clear()。
    /// </summary>
    internal static class CustomInspectLens
    {
        private sealed class LensFollow : MonoBehaviour
        {
            public RectTransform Target;
            public Transform Lens;
            public float Zoom = 2f;

            private void LateUpdate()
            {
                if (Target == null || Lens == null) return;
                Vector3 lensPos = Lens.position;
                Vector3 zoomed = lensPos + Zoom * (Target.position - lensPos);
                zoomed.z = Target.position.z;
                transform.position = zoomed;

                float parentScale = transform.parent != null ? transform.parent.lossyScale.x : 1f;
                float s = parentScale == 0f ? Zoom : Zoom * Target.lossyScale.x / parentScale;
                transform.localScale = new Vector3(s, s, 1f);
            }
        }

        private static GameObject smallRoot;
        private static GameObject largeRoot;

        public static bool Show(string rootPrefix, Action<RectTransform, TMP_FontAsset> buildView)
        {
            Clear();
            var ui = InspectionUIManager.Instance;
            if (ui == null || ui.ribwichUISmall == null || ui.ribwichUILarge == null || buildView == null)
                return false;

            Transform smallParent = ui.ribwichUISmall.transform.parent;
            Transform largeParent = ui.ribwichUILarge.transform.parent;
            TMP_FontAsset font = ui.ribwichUISmall.barcodeTMP != null ? ui.ribwichUISmall.barcodeTMP.font : null;

            smallRoot = CreateCanvasRoot("_" + rootPrefix + "_Small", smallParent);
            Transform glass = ui.magnifyingGlass != null ? ui.magnifyingGlass.transform : null;
            if (glass != null && glass.parent == smallParent)
                smallRoot.transform.SetSiblingIndex(glass.GetSiblingIndex());
            buildView((RectTransform)smallRoot.transform, font);

            largeRoot = CreateCanvasRoot("_" + rootPrefix + "_Zoom", largeParent);
            buildView((RectTransform)largeRoot.transform, font);

            var follow = largeRoot.AddComponent<LensFollow>();
            follow.Target = (RectTransform)smallRoot.transform;
            follow.Lens = largeParent;
            follow.Zoom = ui.ribwichUILarge.transform.localScale.x > 1.01f
                ? ui.ribwichUILarge.transform.localScale.x
                : 2f;

            return true;
        }

        public static void Clear()
        {
            if (smallRoot != null) UnityEngine.Object.Destroy(smallRoot);
            if (largeRoot != null) UnityEngine.Object.Destroy(largeRoot);
            smallRoot = null;
            largeRoot = null;
        }

        private static GameObject CreateCanvasRoot(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(900f, 800f);
            rt.localScale = Vector3.one;
            return go;
        }
    }
}
