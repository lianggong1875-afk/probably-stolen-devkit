using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ModTemplates
{
    /// <summary>
    /// 零存档污染的机器运行动画播放器（蒸馏自 Vanilla Machine Animations）。
    /// 特性：
    ///   1. 8 列网格雪碧图（<c>*_running__frames.png</c>）按需懒切片，支持任意帧延迟表（ms）；
    ///   2. 仅操作实体/拖拽 <c>Image.sprite</c>，绝不修改 <c>GameItem.spritePath</c>，不污染存档；
    ///   3. 播放期间暂设 <c>alphaHitTestMinimumThreshold = 0f</c> 防透明区悬停闪烁，停止后精确还原；
    ///   4. 150ms 状态轮询节流 + <c>HasForeignSprite</c> 防跨模组覆盖冲突。
    /// 零能力，直接复制修改命名空间与规则回调即可使用。
    /// </summary>
    internal sealed class SpriteSheetMachineAnim
    {
        private const int Columns = 8;
        private const int StateCheckIntervalMs = 150;

        internal sealed class ClipSpec
        {
            public readonly string ModId;
            public readonly string SheetKey;
            public readonly int FrameWidth;
            public readonly int FrameHeight;
            public readonly int[] DelaysMs;
            public readonly int[] FrameEnds;
            public readonly int TotalDurationMs;

            public ClipSpec(string modId, string sheetKey, int frameWidth, int frameHeight, int[] delaysMs)
            {
                ModId = modId;
                SheetKey = sheetKey;
                FrameWidth = frameWidth;
                FrameHeight = frameHeight;
                DelaysMs = delaysMs;
                FrameEnds = new int[delaysMs.Length];
                int acc = 0;
                for (int i = 0; i < delaysMs.Length; i++)
                {
                    acc += delaysMs[i];
                    FrameEnds[i] = acc;
                }
                TotalDurationMs = acc;
            }
        }

        private sealed class DisplaySlot
        {
            private Image _image;
            private Sprite _originalSprite;
            private Sprite _lastFrame;
            private float _originalHitThreshold;
            private bool _showing;

            public void Show(Image target, Sprite frame)
            {
                if (target == null || frame == null) { Restore(null); return; }
                if (_image != target)
                {
                    Release();
                    _image = target;
                }
                if (!_showing || _image.sprite != _lastFrame)
                    _originalSprite = _image.sprite;
                if (!_showing)
                {
                    _originalHitThreshold = _image.alphaHitTestMinimumThreshold;
                    _showing = true;
                }
                _image.alphaHitTestMinimumThreshold = 0f;
                if (_image.sprite != frame) _image.sprite = frame;
                _lastFrame = frame;
            }

            public void Restore(Sprite currentNative)
            {
                if (!_showing) return;
                if (_image != null)
                {
                    if (_image.sprite == _lastFrame)
                    {
                        Sprite back = currentNative != null ? currentNative : _originalSprite;
                        if (_image.sprite != back) _image.sprite = back;
                    }
                    if (_image.alphaHitTestMinimumThreshold == 0f)
                        _image.alphaHitTestMinimumThreshold = _originalHitThreshold;
                }
                _showing = false;
                _lastFrame = null;
            }

            public void Release()
            {
                Restore(null);
                _image = null;
                _originalSprite = null;
                _lastFrame = null;
            }
        }

        private sealed class ViewState
        {
            public string SpriteName;
            public Sprite Vanilla;
            public Sprite PreviousVanilla;
            public Sprite[] Frames;
            public readonly DisplaySlot RealDisplay = new DisplaySlot();
            public readonly DisplaySlot DragDisplay = new DisplaySlot();
            public long AnimationStartedMs = -1L;
            public long NextStateCheckMs;
            public bool Active;
        }

        private readonly Func<GameItem, bool> _isTarget;
        private readonly Func<GameItem, bool> _isRunning;
        private readonly ClipSpec _clip;

        private readonly Dictionary<GameItemElement, ViewState> _views = new Dictionary<GameItemElement, ViewState>();
        private readonly List<GameItemElement> _removed = new List<GameItemElement>();
        private readonly Dictionary<(Vector2 Pivot, float Ppu), Sprite[]> _clipCache = new Dictionary<(Vector2, float), Sprite[]>();
        private readonly HashSet<Sprite> _ownedFrames = new HashSet<Sprite>();

        public SpriteSheetMachineAnim(ClipSpec clip, Func<GameItem, bool> isTarget, Func<GameItem, bool> isRunning)
        {
            _clip = clip;
            _isTarget = isTarget;
            _isRunning = isRunning;
        }

        public void Track(GameItemElement item)
        {
            if (item == null || item.state == null || item.IsDestroyed()) return;
            if (!_isTarget(item) || GeneralHelper.IsDisplayOnly(item) || SpriteAnimationHelper.IsSpriteAnimated(item))
                return;
            if (!_views.ContainsKey(item))
                _views.Add(item, new ViewState());
        }

        public void Invalidate(GameItem machine)
        {
            GameItemElement elem = machine as GameItemElement;
            if (elem != null && _views.TryGetValue(elem, out ViewState view))
                view.NextStateCheckMs = 0L;
        }

        public void Clear()
        {
            foreach (KeyValuePair<GameItemElement, ViewState> kv in _views)
            {
                kv.Value.RealDisplay.Release();
                kv.Value.DragDisplay.Release();
            }
            _views.Clear();
            _removed.Clear();
            foreach (Sprite s in _ownedFrames)
                if (s != null) UnityEngine.Object.Destroy(s);
            _ownedFrames.Clear();
            _clipCache.Clear();
        }

        public void Tick()
        {
            if (_views.Count == 0) return;
            long now = (long)(Time.unscaledTimeAsDouble * 1000.0);
            foreach (KeyValuePair<GameItemElement, ViewState> kv in _views)
            {
                GameItemElement item = kv.Key;
                ViewState view = kv.Value;
                if (item == null || item.IsDestroyed() || ReadyChain.IsDestroyedUnityObject(item))
                {
                    view.RealDisplay.Release();
                    view.DragDisplay.Release();
                    _removed.Add(item);
                    continue;
                }
                UpdateItem(item, view, now);
            }
            if (_removed.Count > 0)
            {
                for (int i = 0; i < _removed.Count; i++) _views.Remove(_removed[i]);
                _removed.Clear();
            }
        }

        private void UpdateItem(GameItemElement item, ViewState view, long now)
        {
            if (!_isTarget(item) || item.state == null || GeneralHelper.IsDisplayOnly(item) || SpriteAnimationHelper.IsSpriteAnimated(item))
            {
                view.RealDisplay.Restore(view.Vanilla);
                view.DragDisplay.Restore(view.Vanilla);
                view.AnimationStartedMs = -1L;
                view.Active = false;
                return;
            }

            Image real = item.background != null ? item.background.image : null;
            Image drag = item.fakeBackground != null ? item.fakeBackground.image : null;

            if (view.SpriteName != item.spritePath || view.Vanilla == null)
            {
                view.PreviousVanilla = view.Vanilla;
                view.Vanilla = item.ResolveSpriteByName(item.spritePath);
                view.SpriteName = item.spritePath;
                view.Frames = GetOrCreateFrames(view.Vanilla);
                view.NextStateCheckMs = 0L;
            }

            bool active = view.Frames != null && ReadActive(item, view, now);
            bool foreign = HasForeignSprite(real, view) || HasForeignSprite(drag, view);

            if (!active || foreign)
            {
                view.RealDisplay.Restore(view.Vanilla);
                view.DragDisplay.Restore(view.Vanilla);
                view.AnimationStartedMs = -1L;
                return;
            }

            if (view.AnimationStartedMs < 0L) view.AnimationStartedMs = now;
            Sprite frame = FrameAt(view.Frames, now - view.AnimationStartedMs);
            view.RealDisplay.Show(real, frame);
            view.DragDisplay.Show(drag, frame);
        }

        private bool ReadActive(GameItemElement item, ViewState view, long now)
        {
            if (now < view.NextStateCheckMs) return view.Active;
            view.NextStateCheckMs = now + StateCheckIntervalMs;
            view.Active = _isRunning(item);
            return view.Active;
        }

        private Sprite[] GetOrCreateFrames(Sprite vanilla)
        {
            if (vanilla == null) return null;
            Rect vr = vanilla.rect;
            if ((int)vr.width != _clip.FrameWidth || (int)vr.height != _clip.FrameHeight) return null;

            Vector2 pivot = new Vector2(vanilla.pivot.x / _clip.FrameWidth, vanilla.pivot.y / _clip.FrameHeight);
            (Vector2, float) key = (pivot, vanilla.pixelsPerUnit);
            if (_clipCache.TryGetValue(key, out Sprite[] cached)) return cached;

            LoadedMod mod = ModLoader.Mods.Find((LoadedMod m) => m.Manifest != null && m.Manifest.ID == _clip.ModId);
            if (mod == null || mod.Sprites == null || !mod.Sprites.TryGetValue(_clip.SheetKey, out Sprite sheetSprite) || sheetSprite == null)
                return null;

            Texture2D sheet = sheetSprite.texture;
            if (sheet == null) return null;
            if (sheet.filterMode != FilterMode.Point) sheet.filterMode = FilterMode.Point;

            int rows = (_clip.DelaysMs.Length + Columns - 1) / Columns;
            if (sheet.width != _clip.FrameWidth * Columns || sheet.height < _clip.FrameHeight * rows) return null;

            Sprite[] frames = new Sprite[_clip.DelaysMs.Length];
            for (int i = 0; i < _clip.DelaysMs.Length; i++)
            {
                int col = i % Columns;
                int row = i / Columns;
                int x = col * _clip.FrameWidth;
                int y = sheet.height - (row + 1) * _clip.FrameHeight;
                Sprite s = Sprite.Create(sheet, new Rect(x, y, _clip.FrameWidth, _clip.FrameHeight), pivot, vanilla.pixelsPerUnit, 0u, SpriteMeshType.FullRect);
                frames[i] = s;
                _ownedFrames.Add(s);
            }
            _clipCache[key] = frames;
            return frames;
        }

        private Sprite FrameAt(Sprite[] frames, long elapsedMs)
        {
            int t = (int)(Math.Max(0L, elapsedMs) % _clip.TotalDurationMs);
            int idx = Array.BinarySearch(_clip.FrameEnds, t + 1);
            if (idx < 0) idx = ~idx;
            if (idx >= frames.Length) idx = frames.Length - 1;
            return frames[idx];
        }

        private bool HasForeignSprite(Image image, ViewState view)
        {
            if (image == null || image.sprite == null) return false;
            if (image.sprite == view.Vanilla || image.sprite == view.PreviousVanilla) return false;
            return !_ownedFrames.Contains(image.sprite);
        }
    }
}
