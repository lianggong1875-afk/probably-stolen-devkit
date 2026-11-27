using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModTemplates
{
    /// <summary>
    /// 给游戏外的东西（测试脚本、外部工具、AI）开一条**不用开端口**的通道：
    /// 用原版 ModData 目录做「文件收件箱」——外面往里写命令文件，游戏内轮询执行，结果另写出去。
    ///
    /// 来源蒸馏：店長可能是智能體（工坊 3845754309，`WhiteDragon.Ledger`）。
    /// 注意：作者简介说用 SQLite，但反编译产物里**没有任何 SQLite 引用**，实际就是这套 ModData 文件轮询。
    ///
    /// ✅ 权限实测：全程仅调用原版白名单沙箱 API（ModHelper.*ModDataFile*），不碰 System.IO。
    /// 经游戏真实 ModScanner.Scan 实测：Refused = False，Capabilities 为空（零能力，可安全用于正式发布候选）。
    ///
    /// 三条必守细节（照抄）：
    ///   1. **消费即删**：文件存在 = 有待处理命令，读完立刻删，天然幂等，不会重放；
    ///   2. **代号命名**：每轮任务用 `<prefix>-<generation>.json`，避免上一轮的残留下发；
    ///   3. **malformed 直接丢弃 + LogWarning**：外部输入写错格式绝不能把游戏带崩。
    /// </summary>
    internal static class ModDataInbox
    {
        /// <summary>你的模组 id（必须是 manifest 里的 ID）。</summary>
        internal static string ModId = "my_mod_id";

        /// <summary>收件箱文件名前缀。一轮任务一个代号：`inbox-<generation>.json`。</summary>
        internal static string InboxPrefix = "inbox";

        /// <summary>发件箱（游戏 → 外部）文件名，覆盖写，外部读最新的即可。</summary>
        internal static string OutboxName = "outbox.json";

        /// <summary>轮询间隔（秒）。用 ModHelper.Delay 驱动，不要每帧扫目录。</summary>
        internal static float PollIntervalSeconds = 1f;

        private static bool running;
        private static int generation;
        private static Action<string, string> handler;      // (generation, jsonBody) -> 执行

        /// <summary>
        /// 开始轮询。<paramref name="onCommand"/> 在**主线程**被调用，参数是（代号, 文件正文）。
        /// </summary>
        internal static void Start(Action<string, string> onCommand)
        {
            if (running) return;
            handler = onCommand;
            running = true;
            ScheduleNext();
        }

        /// <summary>停轮询（OnDisable / 热重载前调）。</summary>
        internal static void Stop()
        {
            running = false;
            handler = null;
        }

        /// <summary>开一轮新任务：返回本轮代号，并返回本轮收件箱文件名。</summary>
        internal static string NewGeneration(out string inboxFileName)
        {
            generation++;
            inboxFileName = InboxPrefix + "-" + generation + ".json";
            return generation.ToString();
        }

        /// <summary>游戏 → 外部：写状态/账本（覆盖写）。</summary>
        internal static void Publish(string json)
        {
            try { ModHelper.WriteModDataFileText(ModId, OutboxName, json ?? string.Empty); }
            catch (Exception e) { Debug.LogError("[ModDataInbox] publish failed: " + e); }
        }

        /// <summary>游戏 → 外部：写一份带代号的结果文件（外部按代号取）。</summary>
        internal static void PublishResult(string generationTag, string json)
        {
            try
            {
                ModHelper.WriteModDataFileText(ModId, "result-" + generationTag + ".json", json ?? string.Empty);
            }
            catch (Exception e) { Debug.LogError("[ModDataInbox] publish result failed: " + e); }
        }

        private static void ScheduleNext()
        {
            if (!running) return;
            try { ModHelper.Delay(ModId, PollIntervalSeconds, Poll); }
            catch (Exception e)
            {
                Debug.LogError("[ModDataInbox] schedule failed: " + e);
                running = false;
            }
        }

        /// <summary>扫一遍收件箱目录，处理所有待办，然后排下一次。</summary>
        private static void Poll()
        {
            if (!running) return;
            try
            {
                List<string> files = ModHelper.ListModDataFiles(ModId);
                if (files != null)
                {
                    foreach (string name in files)
                    {
                        if (string.IsNullOrEmpty(name)) continue;
                        if (!name.StartsWith(InboxPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                        if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
                        ConsumeOne(name);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[ModDataInbox] poll failed: " + e);
            }
            finally
            {
                ScheduleNext();
            }
        }

        private static void ConsumeOne(string fileName)
        {
            string body = null;
            try
            {
                body = ModHelper.ReadModDataFileText(ModId, fileName);
            }
            catch (Exception e)
            {
                Debug.LogError("[ModDataInbox] read failed: " + fileName + " / " + e);
            }
            finally
            {
                // 无论读成功与否都删掉：坏文件留在那儿只会每轮刷错误
                try { ModHelper.DeleteModDataFile(ModId, fileName); }
                catch (Exception e) { Debug.LogError("[ModDataInbox] delete failed: " + fileName + " / " + e); }
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                Debug.LogWarning("[ModDataInbox] empty command dropped: " + fileName);
                return;
            }

            string tag = ExtractGeneration(fileName);
            try
            {
                handler?.Invoke(tag, body);
            }
            catch (Exception e)
            {
                // 外部输入把执行搞炸了：吃掉这一次，但必须留日志，绝不牵连游戏循环
                Debug.LogError("[ModDataInbox] malformed command dropped: " + fileName + " / " + e);
            }
        }

        private static string ExtractGeneration(string fileName)
        {
            int dot = fileName.LastIndexOf('.');
            if (dot <= 0) return string.Empty;
            string stem = fileName.Substring(0, dot);
            int dash = stem.LastIndexOf('-');
            return dash >= 0 && dash + 1 < stem.Length ? stem.Substring(dash + 1) : stem;
        }
    }
}
