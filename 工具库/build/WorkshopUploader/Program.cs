using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Steamworks;

internal static class Program
{
    private const uint AppId = 4348910u;

    private sealed class Options
    {
        public string Content = "";
        public string Preview = "";
        public string Title = "";
        public string Description = "";
        public string DescFile = "";
        public string Note = "";
        public string Language = "";
        public int Visibility = 2;
        public ulong PublishedFileId = 0;
        public bool DryRun = false;
        public bool Verify = false;
        // —— 以下为"研读工坊模组"用的订阅/下载能力（与上传共用同一套 Steam 会话）——
        public List<ulong> Subs = new List<ulong>();
        public List<ulong> Unsubs = new List<ulong>();
        public List<ulong> Downloads = new List<ulong>();
        public List<ulong> Fetches = new List<ulong>();
        public bool ListSubs = false;
        // —— 自查 / 清理自己发布的工坊物品（用于误创建后的收尾）——
        public bool ListMine = false;
        public List<ulong> DeleteIds = new List<ulong>();
    }

    private static int Main(string[] args)
    {
        Options opt;
        try { opt = Parse(args); }
        catch (Exception e) { Console.Error.WriteLine("参数错误: " + e.Message); PrintUsage(); return 2; }

        Environment.SetEnvironmentVariable("SteamAppId", AppId.ToString());
        Environment.SetEnvironmentVariable("SteamGameId", AppId.ToString());

        string initError;
        ESteamAPIInitResult initResult = SteamAPI.InitEx(out initError);
        if (initResult != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
        {
            Console.Error.WriteLine("SteamAPI.InitEx 失败: " + initResult + " / " + initError);
            if (!SteamAPI.Init())
            {
                Console.Error.WriteLine("SteamAPI.Init() 同样失败：Steam 客户端未运行或未登录。");
                return 3;
            }
        }

        try
        {
            Console.WriteLine("[init] persona=" + SteamFriends.GetPersonaName()
                + " | steamId=" + SteamUser.GetSteamID().m_SteamID
                + " | appId=" + SteamUtils.GetAppID().m_AppId
                + " | ownsApp=" + SteamApps.BIsSubscribedApp(new AppId_t(AppId)));

            // —— 订阅 / 下载（研读第三方模组）——
            // ⚠️ 每个分支都必须 return！否则会继续走到下面的 CreateItem/Submit，
            //    在玩家工坊里创建并提交一个空物品（实测踩过，已清理）。
            if (opt.ListSubs) { ListSubscribed(); return 0; }
            if (opt.Unsubs.Count > 0) return SetSubscribed(opt.Unsubs, false) ? 0 : 7;
            if (opt.Subs.Count > 0) return SetSubscribed(opt.Subs, true) ? 0 : 7;
            if (opt.Fetches.Count > 0)
            {
                if (!SetSubscribed(opt.Fetches, true)) return 7;
                return DownloadIds(opt.Fetches) ? 0 : 8;
            }
            if (opt.Downloads.Count > 0) return DownloadIds(opt.Downloads) ? 0 : 8;
            if (opt.ListMine) { return ListMyItems() ? 0 : 9; }
            if (opt.DeleteIds.Count > 0) return DeleteItems(opt.DeleteIds) ? 0 : 10;

            // ⚠️ 根本性守卫：走到这里说明上面没有任何子命令命中。
            // 只有「确实要上传」才会继续（必须有 content 目录或已有物品 id，或 --verify）。
            // 没有这道守卫时，--sub/--unsub 忘了 return 会一路走到 CreateItem，在工坊里建空物品（实测踩过 4 次）。
            if (!opt.Verify && opt.Content.Length == 0 && opt.PublishedFileId == 0)
            {
                Console.Error.WriteLine("没有指定任何操作。");
                Console.Error.WriteLine("  上传：--content <目录> [--preview/--title/--desc-file/--id]");
                Console.Error.WriteLine("  研读：--list-subs / --list-mine / --fetch/--sub/--unsub/--download");
                Console.Error.WriteLine("  其他：--dry-run / --verify --id <ID> / --delete <ids>");
                return 2;
            }

            if (opt.DryRun)
            {
                Console.WriteLine("[dry-run] 初始化成功，未提交任何更改。");
                return 0;
            }

            if (opt.Verify)
            {
                if (opt.PublishedFileId == 0) { Console.Error.WriteLine("--verify 需要配合 --id"); return 2; }
                return VerifyItem(opt.PublishedFileId) ? 0 : 6;
            }

            if (opt.Content.Length > 0 && !Directory.Exists(opt.Content))
            {
                Console.Error.WriteLine("内容目录不存在: " + opt.Content);
                return 2;
            }
            if (opt.Preview.Length > 0 && !File.Exists(opt.Preview))
            {
                Console.Error.WriteLine("封面文件不存在: " + opt.Preview);
                return 2;
            }
            if (opt.DescFile.Length > 0)
            {
                if (!File.Exists(opt.DescFile)) { Console.Error.WriteLine("描述文件不存在: " + opt.DescFile); return 2; }
                opt.Description = File.ReadAllText(opt.DescFile).Trim();
            }

            ulong fileId = opt.PublishedFileId;
            if (fileId == 0)
            {
                fileId = CreateItem();
                if (fileId == 0) return 4;
                Console.WriteLine("RESULT_CREATE publishedFileId=" + fileId);
                Console.WriteLine("URL https://steamcommunity.com/sharedfiles/filedetails/?id=" + fileId);
            }

            if (!Submit(new AppId_t(AppId), fileId, opt)) return 5;
            Console.WriteLine("RESULT_OK publishedFileId=" + fileId);
            Console.WriteLine("URL https://steamcommunity.com/sharedfiles/filedetails/?id=" + fileId);
            return 0;
        }
        finally
        {
            SteamAPI.Shutdown();
        }
    }

    // ================= 工坊订阅 / 下载（研读第三方模组用） =================

    /// <summary>列出本账号已订阅的工坊物品及其本地安装状态。</summary>
    private static void ListSubscribed()
    {
        uint n = SteamUGC.GetNumSubscribedItems();
        var ids = new PublishedFileId_t[n == 0 ? 1u : n];
        uint got = SteamUGC.GetSubscribedItems(ids, n);
        Console.WriteLine("[subs] 已订阅 " + got + " 项");
        for (uint i = 0; i < got; i++)
        {
            Console.WriteLine("  " + ids[i].m_PublishedFileId + "  " + DescribeState(ids[i]));
        }
    }

    /// <summary>把 GetItemState 翻成人话 + 本地安装路径。</summary>
    private static string DescribeState(PublishedFileId_t id)
    {
        uint state = SteamUGC.GetItemState(id);
        var parts = new List<string>();
        if (HasFlag(state, EItemState.k_EItemStateSubscribed)) parts.Add("已订阅");
        if (HasFlag(state, EItemState.k_EItemStateInstalled)) parts.Add("已安装");
        if (HasFlag(state, EItemState.k_EItemStateNeedsUpdate)) parts.Add("需更新");
        if (HasFlag(state, EItemState.k_EItemStateDownloading)) parts.Add("下载中");
        if (HasFlag(state, EItemState.k_EItemStateDownloadPending)) parts.Add("待下载");
        ulong sizeOnDisk;
        string folder;
        uint timeStamp;
        if (SteamUGC.GetItemInstallInfo(id, out sizeOnDisk, out folder, 1024u, out timeStamp))
        {
            string upd = timeStamp > 0
                ? DateTimeOffset.FromUnixTimeSeconds(timeStamp).ToLocalTime().ToString("yyyy-MM-dd")
                : "-";
            return string.Join("/", parts) + " | " + (sizeOnDisk / 1024) + " KB | " + upd + " | " + folder;
        }
        return parts.Count > 0 ? string.Join("/", parts) : "未订阅";
    }

    private static bool HasFlag(uint state, EItemState flag)
    {
        return (state & (uint)flag) != 0;
    }

    /// <summary>订阅或取消订阅；每一项都等到 Steam 的正式回调，不猜。</summary>
    private static bool SetSubscribed(List<ulong> ids, bool subscribe)
    {
        bool allOk = true;
        foreach (ulong id in ids)
        {
            bool done = false;
            bool ioFail = false;
            var result = EResult.k_EResultNone;
            ulong echoed = 0;

            if (subscribe)
            {
                SteamAPICall_t call = SteamUGC.SubscribeItem(new PublishedFileId_t(id));
                if (call == SteamAPICall_t.Invalid)
                {
                    Console.Error.WriteLine("[sub] " + id + " SubscribeItem 未返回有效句柄");
                    allOk = false;
                    continue;
                }
                CallResult<RemoteStorageSubscribePublishedFileResult_t> cr =
                    CallResult<RemoteStorageSubscribePublishedFileResult_t>.Create((r, io) =>
                    {
                        ioFail = io;
                        result = r.m_eResult;
                        echoed = r.m_nPublishedFileId.m_PublishedFileId;
                        done = true;
                    });
                cr.Set(call);
                WaitCallbacks(() => done, 60000);
                GC.KeepAlive(cr);
            }
            else
            {
                SteamAPICall_t call = SteamUGC.UnsubscribeItem(new PublishedFileId_t(id));
                if (call == SteamAPICall_t.Invalid)
                {
                    Console.Error.WriteLine("[unsub] " + id + " UnsubscribeItem 未返回有效句柄");
                    allOk = false;
                    continue;
                }
                CallResult<RemoteStorageUnsubscribePublishedFileResult_t> cr =
                    CallResult<RemoteStorageUnsubscribePublishedFileResult_t>.Create((r, io) =>
                    {
                        ioFail = io;
                        result = r.m_eResult;
                        echoed = r.m_nPublishedFileId.m_PublishedFileId;
                        done = true;
                    });
                cr.Set(call);
                WaitCallbacks(() => done, 60000);
                GC.KeepAlive(cr);
            }

            if (!done || ioFail || result != EResult.k_EResultOK)
            {
                Console.Error.WriteLine((subscribe ? "[sub] " : "[unsub] ") + id + " 失败: done=" + done
                    + " io=" + ioFail + " result=" + result + " echoed=" + echoed);
                allOk = false;
                continue;
            }
            Console.WriteLine((subscribe ? "[sub] " : "[unsub] ") + id + " ok");
        }
        return allOk;
    }

    /// <summary>触发下载并等到本地状态真的变成"已安装"；最后打印安装目录。</summary>
    private static bool DownloadIds(List<ulong> ids)
    {
        bool allOk = true;
        foreach (ulong id in ids)
        {
            var pid = new PublishedFileId_t(id);

            // 订阅刚生效时 DownloadItem 可能被拒，先等 state 里出现 subscribed
            uint state = SteamUGC.GetItemState(pid);
            if (!HasFlag(state, EItemState.k_EItemStateSubscribed))
            {
                var sw0 = Stopwatch.StartNew();
                while (!HasFlag(state, EItemState.k_EItemStateSubscribed) && sw0.ElapsedMilliseconds < 30000)
                {
                    SteamAPI.RunCallbacks();
                    Thread.Sleep(200);
                    state = SteamUGC.GetItemState(pid);
                }
            }

            bool started = SteamUGC.DownloadItem(pid, true);
            Console.WriteLine("[download] " + id + " DownloadItem=" + started);

            var sw = Stopwatch.StartNew();
            ulong lastLogged = ulong.MaxValue;
            bool installed = false;
            while (sw.ElapsedMilliseconds < 900000)
            {
                SteamAPI.RunCallbacks();
                state = SteamUGC.GetItemState(pid);
                installed = HasFlag(state, EItemState.k_EItemStateInstalled);
                bool needsUpdate = HasFlag(state, EItemState.k_EItemStateNeedsUpdate);
                bool downloading = HasFlag(state, EItemState.k_EItemStateDownloading);
                bool pending = HasFlag(state, EItemState.k_EItemStateDownloadPending);

                ulong got, total;
                SteamUGC.GetItemDownloadInfo(pid, out got, out total);
                if (total > 0 && got != lastLogged)
                {
                    lastLogged = got;
                    Console.WriteLine("[progress] " + id + " " + (got / 1024) + "/" + (total / 1024) + " KB"
                        + " (" + (got * 100 / (total == 0 ? 1 : total)) + "%)");
                }
                if (installed && !needsUpdate && !pending && !downloading) break;
                Thread.Sleep(500);
            }

            if (!installed)
            {
                Console.Error.WriteLine("[download] " + id + " 超时未完成（可用 --list-subs 复查状态）");
                allOk = false;
                continue;
            }
            ulong sizeOnDisk;
            string folder;
            uint timeStamp;
            if (SteamUGC.GetItemInstallInfo(pid, out sizeOnDisk, out folder, 1024u, out timeStamp))
            {
                Console.WriteLine("[installed] " + id + " " + (sizeOnDisk / 1024) + " KB -> " + folder);
            }
            else
            {
                Console.Error.WriteLine("[installed] " + id + " 拿不到安装目录");
                allOk = false;
            }
        }
        return allOk;
    }

    private static void WaitCallbacks(Func<bool> finished, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (!finished() && sw.ElapsedMilliseconds < timeoutMs)
        {
            SteamAPI.RunCallbacks();
            Thread.Sleep(50);
        }
    }

    // ================= 自查 / 清理自己发布的工坊物品 =================

    /// <summary>列出本账号在创意工坊「已发布」的全部物品（含误创建的空物品）。</summary>
    private static bool ListMyItems()
    {
        var appId = new AppId_t(AppId);
        AccountID_t account = SteamUser.GetSteamID().GetAccountID();
        Console.WriteLine("[mine] 查询已发布物品（account=" + account.m_AccountID + "）");
        int total = 0;
        for (uint page = 1; page <= 20; page++)
        {
            bool done = false;
            bool ioFail = false;
            var result = EResult.k_EResultNone;
            uint returned = 0;
            SteamUGCDetails_t[] found = null;

            UGCQueryHandle_t query = SteamUGC.CreateQueryUserUGCRequest(
                account,
                EUserUGCList.k_EUserUGCList_Published,
                EUGCMatchingUGCType.k_EUGCMatchingUGCType_Items,
                EUserUGCListSortOrder.k_EUserUGCListSortOrder_CreationOrderDesc,
                appId, appId, page);
            if (query == UGCQueryHandle_t.Invalid)
            {
                Console.Error.WriteLine("[mine] CreateQueryUserUGCRequest 失败");
                return false;
            }
            CallResult<SteamUGCQueryCompleted_t> call = CallResult<SteamUGCQueryCompleted_t>.Create((r, io) =>
            {
                ioFail = io;
                result = r.m_eResult;
                returned = r.m_unNumResultsReturned;
                found = new SteamUGCDetails_t[returned];
                for (uint i = 0; i < returned; i++)
                {
                    SteamUGCDetails_t d;
                    if (SteamUGC.GetQueryUGCResult(r.m_handle, i, out d)) found[i] = d;
                }
                SteamUGC.ReleaseQueryUGCRequest(r.m_handle);
                done = true;
            });
            call.Set(SteamUGC.SendQueryUGCRequest(query));
            WaitCallbacks(() => done, 60000);
            GC.KeepAlive(call);

            if (!done || ioFail || result != EResult.k_EResultOK)
            {
                Console.Error.WriteLine("[mine] 查询失败: done=" + done + " io=" + ioFail + " result=" + result);
                return false;
            }
            if (returned == 0) break;

            foreach (SteamUGCDetails_t d in found)
            {
                if (d.m_nPublishedFileId.m_PublishedFileId == 0) continue;
                DateTime created = DateTimeOffset.FromUnixTimeSeconds((long)d.m_rtimeCreated).ToLocalTime().DateTime;
                Console.WriteLine("  " + d.m_nPublishedFileId.m_PublishedFileId
                    + " | " + (string.IsNullOrEmpty(d.m_rgchTitle) ? "(空标题)" : d.m_rgchTitle)
                    + " | 可见性=" + d.m_eVisibility
                    + " | 体积=" + d.m_nFileSize + " B"
                    + " | 创建=" + created.ToString("yyyy-MM-dd HH:mm")
                    + " | url=https://steamcommunity.com/sharedfiles/filedetails/?id=" + d.m_nPublishedFileId.m_PublishedFileId);
                total++;
            }
            if (returned < 50) break;
        }
        Console.WriteLine("[mine] 共 " + total + " 件");
        return true;
    }

    /// <summary>删除本账号自己发布的工坊物品（只用于清理误创建的东西）。</summary>
    private static bool DeleteItems(List<ulong> ids)
    {
        bool allOk = true;
        foreach (ulong id in ids)
        {
            bool done = false;
            bool ioFail = false;
            var result = EResult.k_EResultNone;
            SteamAPICall_t handle = SteamUGC.DeleteItem(new PublishedFileId_t(id));
            if (handle == SteamAPICall_t.Invalid)
            {
                Console.Error.WriteLine("[delete] " + id + " DeleteItem 未返回有效句柄");
                allOk = false;
                continue;
            }
            CallResult<DeleteItemResult_t> call = CallResult<DeleteItemResult_t>.Create((r, io) =>
            {
                ioFail = io;
                result = r.m_eResult;
                done = true;
            });
            call.Set(handle);
            WaitCallbacks(() => done, 60000);
            GC.KeepAlive(call);

            if (!done || ioFail || result != EResult.k_EResultOK)
            {
                Console.Error.WriteLine("[delete] " + id + " 失败: done=" + done + " io=" + ioFail + " result=" + result);
                allOk = false;
            }
            else
            {
                Console.WriteLine("[delete] " + id + " 已删除");
            }
        }
        return allOk;
    }

    // ================= 创建物品 =================

    private static ulong CreateItem()
    {
        bool done = false;
        var result = EResult.k_EResultNone;
        ulong id = 0;
        bool needAgree = false;
        bool ioFail = false;

        CallResult<CreateItemResult_t> call = null;
        SteamAPICall_t handle = SteamUGC.CreateItem(new AppId_t(AppId), EWorkshopFileType.k_EWorkshopFileTypeCommunity);
        if (handle == SteamAPICall_t.Invalid)
        {
            Console.Error.WriteLine("[create] CreateItem 未返回有效 API 调用句柄");
            return 0;
        }
        call = CallResult<CreateItemResult_t>.Create((r, io) =>
        {
            ioFail = io;
            result = r.m_eResult;
            id = r.m_nPublishedFileId.m_PublishedFileId;
            needAgree = r.m_bUserNeedsToAcceptWorkshopLegalAgreement;
            done = true;
        });
        call.Set(handle);

        var sw = Stopwatch.StartNew();
        while (!done && sw.ElapsedMilliseconds < 120000)
        {
            SteamAPI.RunCallbacks();
            Thread.Sleep(50);
        }
        GC.KeepAlive(call);

        if (!done)
        {
            Console.Error.WriteLine("[create] 等待 CreateItem 回调超时");
            return 0;
        }
        if (ioFail || result != EResult.k_EResultOK || id == 0)
        {
            Console.Error.WriteLine("[create] 失败: io=" + ioFail + " result=" + result + " id=" + id);
            return 0;
        }
        Console.WriteLine("[create] 新物品已创建, 需接受工坊协议=" + needAgree);
        return id;
    }

    private static bool Submit(AppId_t appId, ulong fileId, Options opt)
    {
        UGCUpdateHandle_t handle = SteamUGC.StartItemUpdate(appId, new PublishedFileId_t(fileId));
        Console.WriteLine("[update] handle=" + handle);

        if (opt.Title.Length > 0)
        {
            if (!SteamUGC.SetItemTitle(handle, opt.Title)) { Console.Error.WriteLine("[update] SetItemTitle 失败"); return false; }
            Console.WriteLine("[update] title = " + opt.Title);
        }
        if (opt.Description.Length > 0)
        {
            if (!SteamUGC.SetItemDescription(handle, opt.Description)) { Console.Error.WriteLine("[update] SetItemDescription 失败"); return false; }
            Console.WriteLine("[update] description 长度 = " + opt.Description.Length);
        }
        // ⚠️ SetItemUpdateLanguage 会把"本次提交针对的语言版本"切到该语言。
        // 若同时**没有**给描述，Steam 会把那个语言的描述当成空串写进去 ——
        // 作者手写的中文简介就是这么被清空的（实测踩过一次：desc length 从 326 直接变 0）。
        // 铁律：只在确实要改描述时才设语言；只改内容 / 更新说明时一律不碰。
        if (opt.Description.Length > 0 && opt.Language.Length > 0)
        {
            if (!SteamUGC.SetItemUpdateLanguage(handle, opt.Language)) { Console.Error.WriteLine("[update] SetItemUpdateLanguage 失败"); return false; }
            Console.WriteLine("[update] language = " + opt.Language);
        }
        else if (opt.Language.Length > 0)
        {
            Console.WriteLine("[update] 跳过 SetItemUpdateLanguage（本次没改描述，避免清空该语言简介）");
        }
        if (opt.Content.Length > 0)
        {
            string full = Path.GetFullPath(opt.Content);
            if (!SteamUGC.SetItemContent(handle, full)) { Console.Error.WriteLine("[update] SetItemContent 失败"); return false; }
            Console.WriteLine("[update] content = " + full);
        }
        if (opt.Preview.Length > 0)
        {
            string full = Path.GetFullPath(opt.Preview);
            if (!SteamUGC.SetItemPreview(handle, full)) { Console.Error.WriteLine("[update] SetItemPreview 失败"); return false; }
            Console.WriteLine("[update] preview = " + full);
        }
        if (!SteamUGC.SetItemVisibility(handle, (ERemoteStoragePublishedFileVisibility)opt.Visibility))
        {
            Console.Error.WriteLine("[update] SetItemVisibility 失败");
            return false;
        }
        Console.WriteLine("[update] visibility = " + (ERemoteStoragePublishedFileVisibility)opt.Visibility);

        bool done = false;
        var result = EResult.k_EResultNone;
        bool needAgree = false;
        bool ioFail = false;
        ulong lastProcessed = ulong.MaxValue;

        CallResult<SubmitItemUpdateResult_t> call = null;
        SteamAPICall_t submit = SteamUGC.SubmitItemUpdate(handle, opt.Note);
        if (submit == SteamAPICall_t.Invalid)
        {
            Console.Error.WriteLine("[submit] SubmitItemUpdate 未返回有效 API 调用句柄");
            return false;
        }
        call = CallResult<SubmitItemUpdateResult_t>.Create((r, io) =>
        {
            ioFail = io;
            result = r.m_eResult;
            needAgree = r.m_bUserNeedsToAcceptWorkshopLegalAgreement;
            done = true;
        });
        call.Set(submit);

        var sw = Stopwatch.StartNew();
        while (!done && sw.ElapsedMilliseconds < 600000)
        {
            SteamAPI.RunCallbacks();
            ulong processed, total;
            EItemUpdateStatus st = SteamUGC.GetItemUpdateProgress(handle, out processed, out total);
            if (processed != lastProcessed)
            {
                lastProcessed = processed;
                Console.WriteLine("[progress] " + st + " " + processed + "/" + total
                    + (total > 0 ? " (" + (processed * 100 / total) + "%)" : ""));
            }
            Thread.Sleep(200);
        }
        GC.KeepAlive(call);

        if (!done)
        {
            Console.Error.WriteLine("RESULT FAIL: UNCONFIRMED（10 分钟未收到最终确认，可能已提交，请到工坊页面核对）");
            return false;
        }
        if (ioFail)
        {
            Console.Error.WriteLine("RESULT FAIL: IO failure");
            return false;
        }
        if (result != EResult.k_EResultOK)
        {
            Console.Error.WriteLine("RESULT FAIL: " + result + (needAgree ? "（需在 Steam 接受创意工坊法律协议）" : ""));
            return false;
        }
        Console.WriteLine("[submit] 提交成功, 需接受工坊协议=" + needAgree);
        return true;
    }

    private static bool VerifyItem(ulong fileId)
    {
        bool done = false;
        bool ioFail = false;
        var eresult = EResult.k_EResultNone;
        SteamUGCDetails_t details = default(SteamUGCDetails_t);
        string previewUrl = "";

        var ids = new[] { new PublishedFileId_t(fileId) };
        UGCQueryHandle_t query = SteamUGC.CreateQueryUGCDetailsRequest(ids, 1);
        if (query == UGCQueryHandle_t.Invalid)
        {
            Console.Error.WriteLine("[verify] CreateQueryUGCDetailsRequest 失败");
            return false;
        }
        SteamUGC.SetReturnLongDescription(query, true);
        CallResult<SteamUGCQueryCompleted_t> call = null;
        SteamAPICall_t handle = SteamUGC.SendQueryUGCRequest(query);
        call = CallResult<SteamUGCQueryCompleted_t>.Create((r, io) =>
        {
            ioFail = io;
            eresult = r.m_eResult;
            if (!io && r.m_eResult == EResult.k_EResultOK && r.m_unNumResultsReturned > 0)
            {
                SteamUGCDetails_t d;
                if (SteamUGC.GetQueryUGCResult(r.m_handle, 0, out d)) { details = d; }
                string url;
                if (SteamUGC.GetQueryUGCPreviewURL(r.m_handle, 0, out url, 512u)) { previewUrl = url; }
            }
            SteamUGC.ReleaseQueryUGCRequest(r.m_handle);
            done = true;
        });
        call.Set(handle);

        var sw = Stopwatch.StartNew();
        while (!done && sw.ElapsedMilliseconds < 60000)
        {
            SteamAPI.RunCallbacks();
            Thread.Sleep(50);
        }
        GC.KeepAlive(call);

        if (!done) { Console.Error.WriteLine("[verify] 查询超时"); return false; }
        if (ioFail || eresult != EResult.k_EResultOK)
        {
            Console.Error.WriteLine("[verify] 查询失败 io=" + ioFail + " result=" + eresult);
            return false;
        }
        Console.WriteLine("[verify] id            = " + details.m_nPublishedFileId.m_PublishedFileId);
        Console.WriteLine("[verify] title         = " + details.m_rgchTitle);
        Console.WriteLine("[verify] creatorApp    = " + details.m_nCreatorAppID.m_AppId);
        Console.WriteLine("[verify] consumerApp   = " + details.m_nConsumerAppID.m_AppId);
        Console.WriteLine("[verify] owner         = " + details.m_ulSteamIDOwner);
        Console.WriteLine("[verify] visibility    = " + details.m_eVisibility);
        Console.WriteLine("[verify] banned        = " + details.m_bBanned);
        Console.WriteLine("[verify] fileSize      = " + details.m_nFileSize);
        Console.WriteLine("[verify] created       = " + DateTimeOffset.FromUnixTimeSeconds((long)details.m_rtimeCreated).ToLocalTime());
        Console.WriteLine("[verify] updated       = " + DateTimeOffset.FromUnixTimeSeconds((long)details.m_rtimeUpdated).ToLocalTime());
        Console.WriteLine("[verify] desc length   = " + (details.m_rgchDescription != null ? details.m_rgchDescription.Length : 0));
        Console.WriteLine("[verify] preview url   = " + previewUrl);
        Console.WriteLine("[verify] url           = https://steamcommunity.com/sharedfiles/filedetails/?id=" + details.m_nPublishedFileId.m_PublishedFileId);
        return true;
    }

    private static Options Parse(string[] args)
    {
        var o = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            string Next() { if (i + 1 >= args.Length) throw new ArgumentException(a + " 缺少取值"); return args[++i]; }
            switch (a)
            {
                case "--content": o.Content = Next(); break;
                case "--preview": o.Preview = Next(); break;
                case "--title": o.Title = Next(); break;
                case "--desc": o.Description = Next(); break;
                case "--desc-file": o.DescFile = Next(); break;
                case "--note": o.Note = Next(); break;
                case "--lang": o.Language = Next(); break;
                case "--visibility": o.Visibility = int.Parse(Next()); break;
                case "--id": o.PublishedFileId = ulong.Parse(Next()); break;
                case "--dry-run": o.DryRun = true; break;
                case "--verify": o.Verify = true; break;
                case "--sub": o.Subs.AddRange(ParseIds(Next())); break;
                case "--unsub": o.Unsubs.AddRange(ParseIds(Next())); break;
                case "--download": o.Downloads.AddRange(ParseIds(Next())); break;
                case "--fetch": o.Fetches.AddRange(ParseIds(Next())); break;
                case "--list-subs": o.ListSubs = true; break;
                case "--list-mine": o.ListMine = true; break;
                case "--delete": o.DeleteIds.AddRange(ParseIds(Next())); break;
                default: throw new ArgumentException("未知参数 " + a);
            }
        }
        return o;
    }

    /// <summary>解析 id 列表：支持 "a,b,c" / "a b c"，也支持 "@清单文件"（每行一个 id，# 注释）。</summary>
    private static List<ulong> ParseIds(string text)
    {
        var result = new List<ulong>();
        IEnumerable<string> tokens;
        if (text.StartsWith("@"))
        {
            string path = text.Substring(1);
            if (!File.Exists(path)) throw new ArgumentException("清单文件不存在: " + path);
            var fromFile = new List<string>();
            foreach (string line in File.ReadAllLines(path))
            {
                string s = line;
                int hash = s.IndexOf('#');
                if (hash >= 0) s = s.Substring(0, hash);
                fromFile.Add(s);
            }
            tokens = fromFile;
        }
        else
        {
            tokens = new[] { text };
        }
        foreach (string token in tokens)
        {
            foreach (string piece in token.Split(new[] { ',', '、', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                ulong v;
                if (!ulong.TryParse(piece.Trim(), out v)) throw new ArgumentException("不是合法的 PublishedFileId: " + piece);
                result.Add(v);
            }
        }
        if (result.Count == 0) throw new ArgumentException("id 列表为空");
        return result;
    }

    private static void PrintUsage()
    {
        Console.WriteLine(@"用法: WorkshopUploader.exe --content <目录> [选项]
  --content <目录>     要上传的模组目录（SetItemContent）
  --preview <png>      封面图（SetItemPreview）
  --title <标题>       工坊标题
  --desc-file <txt>    工坊描述文本文件（UTF-8）
  --desc <文本>        直接给描述
  --note <说明>        本次更新说明
  --lang <schinese>    元数据语言
  --visibility <0-3>   0公开 1仅好友 2私有 3未列出（默认 2）
  --id <PublishedFileId>  0=新建物品，非0=更新已有物品
  --dry-run            只初始化 Steam 并打印账号信息
  --verify --id <ID>   查询服务端物品详情（标题/可见性/体积/更新时间）

研读第三方模组（订阅/下载/查看，不发布任何内容）:
  --list-subs          列出本账号已订阅的工坊物品 + 本地安装状态与路径
  --sub <ids>          订阅（逗号或空格分隔，也支持 @清单文件）；等到 Steam 正式回调
  --unsub <ids>        取消订阅
  --download <ids>     触发下载并等本地状态变为「已安装」，打印安装目录
  --fetch <ids>        订阅 + 下载 + 打印安装目录（最常用）
  --list-mine          列出本账号在工坊「已发布」的全部物品（自查用）
  --delete <ids>       删除自己发布的工坊物品（只用于清理误创建的东西）
  清单文件格式：一行一个 id，# 开头为注释，可与上面的 id 混用");
    }
}
