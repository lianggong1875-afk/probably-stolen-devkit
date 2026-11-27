using System.Collections.Generic;
using UnityEngine;

namespace ModTemplates
{
    /// <summary>
    /// 贴图与精灵实用工具：像素风锐化、高清道具 PPU 精确换算、不可读纹理 GPU 导出 PNG。
    /// 来源蒸馏：Appraiser+ / PortraitStudio.AuthorKit / MCPBridge。
    /// </summary>
    internal static class SafeSpriteHelper
    {
        /// <summary>
        /// 在 ModHook.OnModAssetsLoaded 里调用，将像素风模组的 Sprites/PaperSprites 统一设为 Point 采样防发糊。
        /// </summary>
        public static void SharpenModPixelSprites(LoadedMod mod)
        {
            if (mod == null) return;
            ApplyFilter(mod.Sprites, FilterMode.Point);
            ApplyFilter(mod.PaperSprites, FilterMode.Point);
        }

        private static void ApplyFilter(Dictionary<string, Sprite> dict, FilterMode mode)
        {
            if (dict == null) return;
            foreach (var sp in dict.Values)
            {
                if (sp != null && sp.texture != null)
                    sp.texture.filterMode = mode;
            }
        }

        /// <summary>
        /// 计算高清道具贴图适配到 (gridCols x gridRows) 物品格框所需的精确 PPU。
        /// 游戏参考 PPU=100、单格 16px。
        /// </summary>
        public static float CalcItemFitPpu(int texWidth, int texHeight, int gridCols, int gridRows, int gridSize = 16)
        {
            float boxW = Mathf.Max(1, gridCols) * gridSize;
            float boxH = Mathf.Max(1, gridRows) * gridSize;
            return 100f * Mathf.Max(texWidth / boxW, texHeight / boxH);
        }

        /// <summary>
        /// 通过 GPU RenderTexture 无损读取不可读（isReadable=false）图集中的任意 Sprite 并编码为 PNG。
        /// 仅限开发工具/导出场景调用（如写盘会触发 files 能力）。
        /// </summary>
        public static byte[] ExtractSpriteToPng(Sprite sprite)
        {
            Texture2D src = sprite.texture;
            Rect r = sprite.textureRect;
            int w = Mathf.RoundToInt(r.width);
            int h = Mathf.RoundToInt(r.height);

            RenderTexture rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            Graphics.Blit(src, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;

            var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(r.x, r.y, w, h), 0, 0);
            copy.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            byte[] png = copy.EncodeToPNG();
            UnityEngine.Object.Destroy(copy);
            return png;
        }
    }
}
