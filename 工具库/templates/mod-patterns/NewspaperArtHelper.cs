using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModTemplates
{
    /// <summary>
    /// 零能力报纸事件配图渲染器（蒸馏自 Better Newspapers 并在深空电玩验证通过）。
    /// 用法：
    ///   1. Harmony Patch NewsUIManager.PopulateUI：
    ///        Prefix  调 HideAll()；
    ///        Postfix 加 [HarmonyAfter("mod.local.newspaper.illustrations")] [HarmonyPriority(Priority.Last)] 调 Apply(__instance)；
    ///   2. 在 Mod OnDisable / HotReloadPrepare 调 Clear() 释放对象与边框 Sprite；
    ///   3. 传入 Func<string, string> resolveTextureKey（事件 id → 根目录下 Textures/<key>.png 键名）。
    /// </summary>
    internal sealed class NewspaperArtHelper
    {
        private const int InsetPixels = 6;
        private const float MinPhotoSize = 60f;
        private const float MaxPhotoHeight = 320f;

        private sealed class PhotoSlot
        {
            public RectTransform Frame;
            public Image Border;
            public RawImage Photo;
        }

        private readonly string _modId;
        private readonly string _frameKey;
        private readonly Func<string, string> _resolveTextureKey;
        private readonly Dictionary<NewsUIElement, PhotoSlot> _slots = new Dictionary<NewsUIElement, PhotoSlot>();
        private Sprite _frameSprite;

        public NewspaperArtHelper(string modId, string frameKey, Func<string, string> resolveTextureKey)
        {
            _modId = modId;
            _frameKey = frameKey;
            _resolveTextureKey = resolveTextureKey;
        }

        public void HideAll()
        {
            foreach (KeyValuePair<NewsUIElement, PhotoSlot> kv in _slots)
                if (kv.Value?.Frame != null && !ReadyChain.IsDestroyedUnityObject(kv.Value.Frame))
                    kv.Value.Frame.gameObject.SetActive(false);
        }

        public void Clear()
        {
            foreach (KeyValuePair<NewsUIElement, PhotoSlot> kv in _slots)
                if (kv.Value?.Frame != null && !ReadyChain.IsDestroyedUnityObject(kv.Value.Frame))
                    UnityEngine.Object.Destroy(kv.Value.Frame.gameObject);
            _slots.Clear();
            if (_frameSprite != null && !ReadyChain.IsDestroyedUnityObject(_frameSprite))
                UnityEngine.Object.Destroy(_frameSprite);
            _frameSprite = null;
        }

        public void Apply(NewsUIManager mgr)
        {
            if (mgr == null) return;
            LoadedMod mod = ModLoader.Mods.Find((LoadedMod m) => m.Manifest != null && m.Manifest.ID == _modId);
            if (mod == null || mod.Textures == null) return;

            List<NewsUIElement> active = CollectActive(mgr);
            if (active.Count == 0) return;

            Canvas.ForceUpdateCanvases();
            RectTransform panel = mgr.layout1 != null ? mgr.layout1.transform.parent as RectTransform : null;

            for (int i = 0; i < active.Count; i++)
            {
                NewsUIElement el = active[i];
                string key = _resolveTextureKey(el.savedStoreEvent.identifier);
                if (string.IsNullOrEmpty(key) || !mod.Textures.TryGetValue(key, out Texture2D tex) || tex == null) continue;
                if (tex.filterMode != FilterMode.Point) tex.filterMode = FilterMode.Point;
                if (tex.mipmapCount > 1 && tex.mipMapBias > -2.5f) tex.mipMapBias = -3f;

                Transform existing = el.textContent.rectTransform.Find("Printed photograph");
                if (existing != null && existing.gameObject.activeSelf) continue;

                if (!TryComputeRect(el, panel, active, out Rect frameRect)) continue;
                ShowPhoto(mod, el, tex, frameRect);
            }
        }

        private static List<NewsUIElement> CollectActive(NewsUIManager mgr)
        {
            List<NewsUIElement> dst = new List<NewsUIElement>();
            HashSet<NewsUIElement> seen = new HashSet<NewsUIElement>();
            foreach (List<NewsUIElement> lst in new[] { mgr.layout1Elements, mgr.layout2Elements, mgr.layout3Elements, mgr.layout4Elements, mgr.allElements })
            {
                if (lst == null) continue;
                for (int i = 0; i < lst.Count; i++)
                {
                    NewsUIElement el = lst[i];
                    if (el != null && seen.Add(el) && el.gameObject.activeInHierarchy && el.savedStoreEvent != null && el.textContent != null)
                        dst.Add(el);
                }
            }
            return dst;
        }

        private static bool TryComputeRect(NewsUIElement el, RectTransform panel, List<NewsUIElement> active, out Rect frameRect)
        {
            frameRect = default(Rect);
            TextMeshProUGUI tmp = el.textContent;
            tmp.ForceMeshUpdate(true, true);

            RectTransform rt = tmp.rectTransform;
            Rect rect = rt.rect;
            Vector4 margin = tmp.margin;

            float textBottom = rect.yMax - margin.y;
            for (int i = 0; i < tmp.textInfo.lineCount; i++)
                if (tmp.textInfo.lineInfo[i].characterCount > 0)
                    textBottom = Mathf.Min(textBottom, tmp.textInfo.lineInfo[i].descender);

            float top = textBottom - 10f;
            float left = rect.xMin + margin.x + 3f;
            float right = rect.xMax - margin.z - 3f;

            bool hasRelayout = panel != null && (panel.Find("Newspaper footer") != null
                || panel.Find("Newspaper page navigation") != null
                || panel.Find("柜台笑谈") != null || panel.Find("街头剪贴") != null);

            float bottom = hasRelayout ? float.NegativeInfinity : (rect.yMin + margin.w + 4f);

            if (panel != null)
            {
                Transform footer = panel.Find("Newspaper footer");
                Transform nav = panel.Find("Newspaper page navigation");
                bool hasFooter = (footer != null && footer.gameObject.activeSelf) || (nav != null && nav.gameObject.activeSelf);
                float panelBottomLocal = rt.InverseTransformPoint(
                    panel.TransformPoint(new Vector3(0f, panel.rect.yMin + (hasFooter ? 96f : 24f), 0f))).y;
                bottom = Mathf.Max(bottom, panelBottomLocal);

                for (int c = 0; c < panel.childCount; c++)
                {
                    Transform ch = panel.GetChild(c);
                    if (ch == null || !ch.gameObject.activeSelf) continue;
                    if (ch.name != "柜台笑谈" && ch.name != "街头剪贴" && ch.name != "Column rule") continue;
                    if (ch is RectTransform crt)
                    {
                        Rect cr = crt.rect;
                        Vector3 tl = rt.InverseTransformPoint(crt.TransformPoint(new Vector3(cr.xMin, cr.yMax, 0f)));
                        Vector3 tr = rt.InverseTransformPoint(crt.TransformPoint(new Vector3(cr.xMax, cr.yMax, 0f)));
                        if (tl.y < top && Mathf.Min(right, tr.x) - Mathf.Max(left, tl.x) > 15f)
                            bottom = Mathf.Max(bottom, tl.y + 8f);
                    }
                }
            }

            for (int j = 0; j < active.Count; j++)
            {
                NewsUIElement other = active[j];
                if (other == el || other.title == null) continue;
                RectTransform ort = other.title.rectTransform;
                Rect or = ort.rect;
                Vector3 tl = rt.InverseTransformPoint(ort.TransformPoint(new Vector3(or.xMin, or.yMax, 0f)));
                Vector3 tr = rt.InverseTransformPoint(ort.TransformPoint(new Vector3(or.xMax, or.yMax, 0f)));
                if (tl.y < top && Mathf.Min(right, tr.x) - Mathf.Max(left, tl.x) > 15f)
                    bottom = Mathf.Max(bottom, tl.y + 8f);
            }

            float availW = right - left;
            float availH = top - bottom;
            if (availW < MinPhotoSize || availH < MinPhotoSize) return false;

            float height = Mathf.Floor(Mathf.Min(availH, MaxPhotoHeight));
            float width = Mathf.Floor(Mathf.Min(availW, (height - InsetPixels * 2f) * 1.5f + InsetPixels * 2f, MaxPhotoHeight * 1.5f));
            float cx = (left + right) * 0.5f;
            frameRect = Rect.MinMaxRect(cx - width * 0.5f, top - height, cx + width * 0.5f, top);
            return true;
        }

        private void ShowPhoto(LoadedMod mod, NewsUIElement el, Texture2D tex, Rect frameRect)
        {
            RectTransform parent = el.textContent.rectTransform;
            if (!_slots.TryGetValue(el, out PhotoSlot slot) || slot?.Frame == null || ReadyChain.IsDestroyedUnityObject(slot.Frame))
            {
                GameObject frameGo = new GameObject("ModNewsPhoto", typeof(RectTransform), typeof(Image));
                RectTransform frameRt = frameGo.GetComponent<RectTransform>();
                frameRt.SetParent(parent, false);
                Image border = frameGo.GetComponent<Image>();
                border.raycastTarget = false;

                GameObject imgGo = new GameObject("Photo", typeof(RectTransform), typeof(RawImage));
                RectTransform imgRt = imgGo.GetComponent<RectTransform>();
                imgRt.SetParent(frameRt, false);
                imgRt.anchorMin = Vector2.zero;
                imgRt.anchorMax = Vector2.one;
                imgRt.offsetMin = new Vector2(InsetPixels, InsetPixels);
                imgRt.offsetMax = new Vector2(-InsetPixels, -InsetPixels);
                RawImage photo = imgGo.GetComponent<RawImage>();
                photo.raycastTarget = false;

                slot = new PhotoSlot { Frame = frameRt, Border = border, Photo = photo };
                _slots[el] = slot;
            }
            if (slot.Frame.parent != parent) slot.Frame.SetParent(parent, false);
            slot.Frame.anchorMin = slot.Frame.anchorMax = parent.pivot;
            slot.Frame.pivot = new Vector2(0.5f, 1f);
            slot.Frame.anchoredPosition = new Vector2(Mathf.Round(frameRect.center.x), Mathf.Floor(frameRect.yMax));
            slot.Frame.sizeDelta = new Vector2(Mathf.Floor(frameRect.width), Mathf.Floor(frameRect.height));

            if (_frameSprite == null && !string.IsNullOrEmpty(_frameKey) && mod.Textures.TryGetValue(_frameKey, out Texture2D ftex) && ftex != null)
            {
                ftex.filterMode = FilterMode.Point;
                _frameSprite = Sprite.Create(ftex, new Rect(0f, 0f, ftex.width, ftex.height), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(14f, 14f, 14f, 14f));
            }
            if (_frameSprite != null)
            {
                slot.Border.sprite = _frameSprite;
                slot.Border.type = Image.Type.Sliced;
                slot.Border.color = Color.white;
            }
            else
            {
                slot.Border.sprite = null;
                slot.Border.color = new Color32(56, 54, 49, 255);
            }

            slot.Photo.texture = tex;
            float w = Mathf.Max(1f, slot.Photo.rectTransform.rect.width);
            float h = Mathf.Max(1f, slot.Photo.rectTransform.rect.height);
            float texAspect = (float)tex.width / tex.height;
            float targetAspect = w / h;
            slot.Photo.uvRect = Math.Abs(texAspect - targetAspect) < 0.01f
                ? new Rect(0f, 0f, 1f, 1f)
                : (texAspect > targetAspect
                    ? new Rect((1f - targetAspect / texAspect) * 0.5f, 0f, targetAspect / texAspect, 1f)
                    : new Rect(0f, (1f - texAspect / targetAspect) * 0.5f, 1f, texAspect / targetAspect));
            slot.Frame.gameObject.SetActive(true);
        }
    }
}
