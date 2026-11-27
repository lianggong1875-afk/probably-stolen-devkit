using System;
using UnityEngine;

namespace ModTemplates
{
    /// <summary>
    /// 零反射、零能力的跨模组高清去像素化租约。
    /// 来源蒸馏：PortraitStudio.AuthorKit (StandaloneHighResolution)。
    /// 多模组通过 pixelPerfectCamera 下的共享场景节点协调开关，最后一个释放者自动恢复玩家原生设置。
    /// </summary>
    internal sealed class HdCameraLease : IDisposable
    {
        private const string LeaseRootName = "PortraitStudio.AuthorHD.Leases";
        private readonly string tokenName;
        private GameObject token;
        private Transform root;
        private Behaviour camera;
        private bool disposed;

        public HdCameraLease(string ownerTag)
        {
            tokenName = string.IsNullOrEmpty(ownerTag) ? "mod-hd-lease" : ownerTag + "-hd-lease";
        }

        /// <summary>每帧或状态切换时调用：requested=true 持有高清，false 释放。</summary>
        public void Tick(bool requested)
        {
            if (disposed) return;

            Behaviour next = EmporiumEntry.Instance == null ? null : EmporiumEntry.Instance.pixelPerfectCamera;
            if (camera != next || !requested)
                Release();
            if (!requested || next == null)
                return;

            if (token == null)
            {
                Transform shared = next.transform.Find(LeaseRootName);
                // 原版或玩家本来就关着像素化时，不介入租约
                if (shared == null && !next.enabled)
                    return;

                if (shared == null)
                {
                    var host = new GameObject(LeaseRootName);
                    shared = host.transform;
                    shared.SetParent(next.transform, false);
                }

                root = shared;
                camera = next;
                token = new GameObject(tokenName);
                token.transform.SetParent(root, false);
            }

            // 游戏在窗口切焦点/改分辨率时会重开相机，需持续护持
            if (camera.enabled)
                camera.enabled = false;
        }

        public void Release()
        {
            if (token != null)
            {
                token.transform.SetParent(null, false);
                UnityEngine.Object.Destroy(token);
                token = null;
            }

            if (root != null && root.childCount == 0)
            {
                // Destroy 到帧末才生效，先改名避免同帧其它模组 Find 到将死节点
                root.gameObject.name = LeaseRootName + ".released";
                UnityEngine.Object.Destroy(root.gameObject);

                if (camera != null && !camera.enabled)
                {
                    if (EmporiumEntry.Instance != null
                        && EmporiumEntry.Instance.pixelPerfectCamera == camera
                        && EmporiumOption.Instance != null)
                    {
                        EmporiumOption.Instance.ApplyPixelPerfectCamera();
                    }
                    else
                    {
                        camera.enabled = true;
                    }
                }
            }

            root = null;
            camera = null;
        }

        public void Dispose()
        {
            if (disposed) return;
            Release();
            disposed = true;
        }
    }
}
