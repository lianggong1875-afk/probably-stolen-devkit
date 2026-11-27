using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModTemplates
{
    /// <summary>
    /// 像素立绘零泄漏目录模板（零能力）：
    ///   · 纯内存从 mip 0 重建无 Mipmap + Point + Clamp 贴图，并显式 Destroy 旧贴图防显存泄漏；
    ///   · 维护 _spriteCache 实现跨天/读档 SpriteDict 重建后的 O(1) 零分配自愈；
    ///   · 支持 spriteKey != textureKey 映射，避免自愈落空。
    /// 实证来源：深空电玩 (ArcPortrait) / 深空当铺：可能是群友 (FreyaPortrait) 2026-11-28 全面重构。
    /// </summary>
    internal sealed class CrispPortraitCatalog
    {
        private const float TargetWorldHeight = 1.54f;
        private const float PivotX = 0.5f;
        private const float PivotY = 0.657f;

        private readonly string _modId;
        private readonly Dictionary<string, string> _spriteToTexture = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>(StringComparer.Ordinal);

        internal CrispPortraitCatalog(string modId)
        {
            _modId = modId;
        }

        internal void Define(string spriteKey, string textureKey = null)
        {
            if (string.IsNullOrEmpty(spriteKey)) return;
            _spriteToTexture[spriteKey] = string.IsNullOrEmpty(textureKey) ? spriteKey : textureKey;
        }

        internal bool IsOurs(string spriteKey) =>
            !string.IsNullOrEmpty(spriteKey) && _spriteToTexture.ContainsKey(spriteKey);

        internal void Invalidate()
        {
            foreach (KeyValuePair<string, Sprite> kv in _spriteCache)
                if (kv.Value != null) UnityEngine.Object.Destroy(kv.Value);
            _spriteCache.Clear();
        }

        internal bool EnsureInSpriteDict(string spriteKey, bool force = false)
        {
            string textureKey;
            if (string.IsNullOrEmpty(spriteKey) || !_spriteToTexture.TryGetValue(spriteKey, out textureKey))
                return false;

            SpriteDict dict = SpriteDict.Instance;
            if (dict == null || dict.spriteDictionary == null) return false;

            Sprite existing;
            if (!force && dict.spriteDictionary.TryGetValue(spriteKey, out existing) && existing != null)
                return true;

            Sprite cached;
            if (!force && _spriteCache.TryGetValue(spriteKey, out cached) && cached != null && cached.texture != null)
            {
                dict.spriteDictionary[spriteKey] = cached;
                return true;
            }

            LoadedMod mod = ModLoader.Mods.Find(m => m != null && m.Manifest != null && m.Manifest.ID == _modId);
            Texture2D tex;
            if (mod == null || mod.Textures == null || !mod.Textures.TryGetValue(textureKey, out tex) || tex == null)
                return false;

            tex = EnsureCrisp(mod, textureKey, tex);
            if (tex == null) return false;

            float ppu = tex.height / TargetWorldHeight;
            Sprite sp = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(PivotX, PivotY), ppu);
            if (sp == null) return false;
            sp.name = spriteKey;

            Sprite old;
            if (_spriteCache.TryGetValue(spriteKey, out old) && old != null && !ReferenceEquals(old, sp))
                UnityEngine.Object.Destroy(old);
            _spriteCache[spriteKey] = sp;
            dict.spriteDictionary[spriteKey] = sp;
            return true;
        }

        private static Texture2D EnsureCrisp(LoadedMod mod, string textureKey, Texture2D source)
        {
            if (source.mipmapCount <= 1 && source.filterMode == FilterMode.Point && source.wrapMode == TextureWrapMode.Clamp)
                return source;

            int w = source.width, h = source.height;
            if (w > 0 && h > 0)
            {
                Color32[] pixels = source.GetPixels32(0);
                if (pixels != null && pixels.Length == w * h)
                {
                    Texture2D crisp = new Texture2D(w, h, TextureFormat.RGBA32, false);
                    crisp.name = source.name;
                    crisp.SetPixels32(pixels);
                    crisp.filterMode = FilterMode.Point;
                    crisp.wrapMode = TextureWrapMode.Clamp;
                    crisp.Apply(false, false);
                    mod.Textures[textureKey] = crisp;
                    UnityEngine.Object.Destroy(source);   // 立即释放原 mipmap 纹理显存
                    return crisp;
                }
            }
            source.filterMode = FilterMode.Point;
            source.wrapMode = TextureWrapMode.Clamp;
            source.Apply(false, false);
            return source;
        }
    }
}
