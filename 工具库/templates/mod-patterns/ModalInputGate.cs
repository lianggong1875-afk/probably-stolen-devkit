using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ModTemplates
{
    /// <summary>
    /// 模态窗口输入门控：窗口关闭后多保持 1 帧并等待全部鼠标键抬起再交还输入，
    /// 彻底杜绝「点击关闭按钮时穿透误触背后柜台物品」。
    /// 来源蒸馏：Developer Tools (InputCaptureGate + WorkshopInput)。
    /// </summary>
    internal sealed class ModalInputGate
    {
        private bool captured;
        private int releaseThrough = -1;

        public void OnOpening()
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            CustomEventHandler.ClearDoubleClickTarget();
            if (InputActionManager.current != null)
            {
                InputActionManager.current.Reset();
                InputActionManager.current.ResetExcept(null);
            }
        }

        public void OnClosing()
        {
            captured = true;
            releaseThrough = Math.Max(releaseThrough, Time.frameCount + 1);
            CustomEventHandler.ClearDoubleClickTarget();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        public bool IsBlocked(bool windowVisible)
        {
            bool mouseHeld = false;
            for (int i = 0; i < 7; i++)
                mouseHeld |= Input.GetMouseButton(i);

            int frame = Time.frameCount;
            if (windowVisible || (captured && mouseHeld))
            {
                captured = true;
                releaseThrough = frame + 1;
            }
            else if (captured && frame > releaseThrough)
            {
                captured = false;
            }
            return captured;
        }

        public void Reset()
        {
            captured = false;
            releaseThrough = -1;
        }
    }
}
