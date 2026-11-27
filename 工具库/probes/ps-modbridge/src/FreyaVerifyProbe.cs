using System;
using System.Collections.Generic;
using System.Text;
using ModDevBridge;
using UnityEngine;

namespace FreyaVerify
{
    public sealed class FreyaVerifyProbe1 : ModDevBridge.IDevModule
    {
        public string Start() { return "FreyaVerifyProbe1 ready: watertest | summon <id> | step | state | clear"; }
        public void Stop() { }

        public string Execute(string command)
        {
            string cmd = (command ?? "").Trim();
            string[] parts = cmd.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "empty";
            string verb = parts[0].ToLowerInvariant();
            string arg = parts.Length > 1 ? parts[1] : "";

            try
            {
                if (verb == "watertest") return RunWaterTest();
                if (verb == "summon") return SummonNow(arg);
                if (verb == "advance") return AdvanceDialog();
                if (verb == "choose") return ChooseOption(parts.Length > 1 ? int.Parse(parts[1]) : 0);
                if (verb == "state") return GetState();
                if (verb == "bg") { Application.runInBackground = true; return "runInBackground=True"; }
                if (verb == "dismiss") { PlayerStore.Instance.DismissCurrentClient(false); return "dismissed"; }
                return "unknown: " + verb;
            }
            catch (Exception e)
            {
                return "ERROR: " + e;
            }
        }

        private static string RunWaterTest()
        {
            StoreClient siren = StoreClientListDict.CreateStoreClient("siren_buy");
            if (siren == null) return "FAIL: siren_buy is null";

            var sb = new StringBuilder();
            sb.AppendLine("siren_buy buyingTags=" + siren.clientBuyingTagList.Count
                + " buyingIds=" + siren.clientBuyingIdList.Count);

            int pass = 0, fail = 0;

            void TestItem(string label, GameItem it, bool expected)
            {
                if (it == null) { sb.AppendLine("  FAIL " + label + " -> null item"); fail++; return; }
                try
                {
                    GeneralHelper.SetItemOwned(it, true);
                    bool actual = siren.IsClientBuyingThisItem(it);
                    bool ok = actual == expected;
                    if (ok) pass++; else fail++;
                    int purity = WaterHelper.GetWaterPurity(it);
                    int idx = WaterFeatureHelper.GetPurityArrayIndex(purity);
                    sb.AppendLine(string.Format("  {0} {1}: actual={2} expected={3} (id={4}, purity={5}, idx={6}, isWaterTag={7}, isWaterType={8}, isBrevTag={9})",
                        ok ? "PASS" : "FAIL", label, actual, expected, it.identifier, purity, idx,
                        it.IsTag("WATER"), it.IsGameItemType("WATER"), it.IsTag("BREVAGE")));
                }
                finally
                {
                    try { it.Destroy(); } catch { }
                }
            }

            // 1. 纯水 (9995)
            GameItem pure = DirectoryMaster.Item("bottled_water");
            WaterHelper.FillWithPureWater(pure);
            TestItem("pure_water", pure, true);

            // 2. 优质水
            TestItem("high_quality_water", WaterPremadeHelper.AccurateHighQualityWater(), true);

            // 3. 成品优质水
            TestItem("bottled_water_premium", DirectoryMaster.Item("bottled_water_premium"), true);

            // 4. 基础水
            TestItem("base_water", WaterPremadeHelper.AccurateBaseWater(), false);

            // 5. 假标签基础水（标成优质水）
            TestItem("fake_base_as_hq", WaterPremadeHelper.BaseWaterAsHQWater(), false);

            // 6. 幽灵水（真标签）
            TestItem("ghost_water", WaterPremadeHelper.AccurateGhostWater(), false);

            // 7. 假标签幽灵水（标成纯水！）
            TestItem("fake_ghost_as_pure", WaterPremadeHelper.GhostWaterAsPureWater(), false);

            // 8. 铁锈水（大瓶 + 普通瓶）
            TestItem("rust_water", WaterPremadeHelper.AccurateRustWater("bottled_water"), false);
            TestItem("rust_water_large", WaterPremadeHelper.AccurateRustWater("large_bottled_water"), false);
            TestItem("fake_rust_as_base", WaterPremadeHelper.RustWaterAsBaseWater("large_bottled_water"), false);

            // 9. 沟渠水（大瓶 + 普通瓶）
            GameItem gutter = DirectoryMaster.Item("large_bottled_water");
            WaterHelper.FillWithGutterflow(gutter);
            TestItem("gutterflow_large", gutter, false);
            TestItem("fake_gutter_as_base", WaterPremadeHelper.GutterflowAsBaseWater("bottled_water"), false);

            // 10. 空水瓶
            GameItem empty = DirectoryMaster.Item("bottled_water");
            WaterHelper.RemoveMl(empty, 999999);
            TestItem("empty_bottle", empty, false);

            // 11. 水配给 / 口粮水
            TestItem("water_ration", DirectoryMaster.Item("water_ration"), false);

            // 12. 真饮料（红啤）
            TestItem("red_beer", DirectoryMaster.Item("red_beer"), true);

            sb.AppendLine(string.Format("SUMMARY: pass={0} fail={1}", pass, fail));
            return sb.ToString();
        }

        private static string SummonNow(string id)
        {
            Application.runInBackground = true;
            PlayerStore ps = PlayerStore.Instance;
            if (ps.currentClientInstance != null)
            {
                ps.DismissCurrentClient(callNextWhenPossible: false, right: true, interruptDialog: true);
            }
            StoreClient c = StoreClientListDict.CreateStoreClient(id);
            if (c == null) return "FAIL: CreateStoreClient returned null for " + id;
            ps.storeClientManager.AddNextClient(c);
            StoreShutterButton shutter = UnityEngine.Object.FindObjectOfType<StoreShutterButton>();
            if (shutter != null && ps.storeState != PlayerStore.StoreState.OPEN)
            {
                shutter.OpenShutter();
            }
            StoreBell bell = UnityEngine.Object.FindObjectOfType<StoreBell>();
            if (bell != null) bell.OnClick();
            return "summoned " + id + " (intent=" + c.clientIntent + ", dismissable=" + c.dismissable + ")";
        }

        private static string AdvanceDialog()
        {
            DialogUIManager ui = DialogUIManager.Instance;
            if (ui == null) return "no DialogUIManager";
            ui.OnAdvancePressed();
            return GetState();
        }

        private static string ChooseOption(int idx)
        {
            DialogUIManager ui = DialogUIManager.Instance;
            if (ui == null) return "no DialogUIManager";
            ui.SelectChoice(idx);
            return GetState();
        }

        private static string GetState()
        {
            PlayerStore ps = PlayerStore.Instance;
            StoreClientInstance inst = ps == null ? null : ps.currentClientInstance;
            StoreClient bp = inst == null ? null : inst.GetClientBlueprint();
            DialogUIManager ui = DialogUIManager.Instance;
            Dialogue d = ui == null ? null : ui.currentDialog;
            int nonOwned = 0;
            var counterNames = new List<string>();
            if (EmporiumEntry.Instance != null && EmporiumEntry.Instance.frontInvinvElement != null)
            {
                foreach (GameItem it in EmporiumEntry.Instance.frontInvinvElement.childItems)
                {
                    if (!GeneralHelper.IsItemOwned(it)) nonOwned++;
                    counterNames.Add(it.identifier + "(" + it.GetBaseValue() + (GeneralHelper.IsItemOwned(it) ? ",own" : ",sale") + ")");
                }
            }
            return string.Format(
                "client={0} dismissable={1} intent={2} | dialog={3} choices={4} | frontCount={5} nonOwned={6} items=[{7}]",
                bp == null ? "null" : bp.identifier,
                bp == null ? "-" : bp.dismissable.ToString(),
                bp == null ? "-" : bp.clientIntent.ToString(),
                d == null ? "null" : ("\"" + d.plainText + "\""),
                d == null ? 0 : d.choices.Count,
                counterNames.Count,
                nonOwned,
                string.Join(", ", counterNames.ToArray()));
        }
    }
}
