using System;
using HarmonyLib;
using UnityEngine;

namespace YourModNamespace
{
    /// <summary>
    /// 《深空当铺》标准零能力模组入口骨架（支持 ModDevBridgeNext 热重载）。
    /// </summary>
    public sealed class ModEntry : IMod
    {
        public const string ModId = "your.mod_id";

        private Harmony harmony;
        private bool hooksBound;

        public void Init(ModManifest manifest)
        {
            Debug.Log("[" + ModId + "] Init v" + (manifest != null ? manifest.ModVersion : "?"));
        }

        public void OnEnable()
        {
            try
            {
                BindHooks();
                harmony = new Harmony("mod." + ModId);
                harmony.PatchAll(typeof(ModEntry).Assembly);
            }
            catch (Exception ex)
            {
                Debug.LogError("[" + ModId + "] OnEnable failed: " + ex);
                OnDisable();
                throw;
            }
        }

        public void OnDisable()
        {
            UnbindHooks();
            if (harmony != null)
            {
                harmony.UnpatchAll(harmony.Id);
                harmony = null;
            }
            ResetRuntimeState();
        }

        /// <summary>供 ModDevBridgeNext reload_mod 在换出旧版本前调用。</summary>
        public void HotReloadPrepare()
        {
            UnbindHooks();
            ResetRuntimeState();
        }

        /// <summary>供 ModDevBridgeNext reload_mod 在换入新版本后调用。</summary>
        public void HotReloadActivate()
        {
            BindHooks();
        }

        private void BindHooks()
        {
            if (hooksBound) return;
            ModHook.OnModItemDirectoryInit += OnItemDirectoryInit;
            ModHook.OnGameLoadedInit += OnGameLoadedInit;
            ModHook.OnGameLoadedNormal += OnGameLoadedNormal;
            hooksBound = true;
        }

        private void UnbindHooks()
        {
            if (!hooksBound) return;
            ModHook.OnModItemDirectoryInit -= OnItemDirectoryInit;
            ModHook.OnGameLoadedInit -= OnGameLoadedInit;
            ModHook.OnGameLoadedNormal -= OnGameLoadedNormal;
            hooksBound = false;
        }

        private void OnItemDirectoryInit(ModItemDirectory dir)
        {
            // 在此注册自定义物品工厂：if (!dir.Has(id)) dir.Add(id, factory); else dir.Set(id, factory);
        }

        private void OnGameLoadedInit()
        {
            ResetRuntimeState();
        }

        private void OnGameLoadedNormal()
        {
            // 在此恢复依赖存档与场景的运行时状态
        }

        private void ResetRuntimeState()
        {
            // 清理静态缓存、自建 UI 与对象引用
        }
    }
}
