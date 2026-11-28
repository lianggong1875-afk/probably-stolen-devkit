using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace ModDevBridge;

internal static class RuntimeInspector
{
    public static object Inspect(string kind, string target) => Inspect(kind, target, new JObject());
    public static object Inspect(string kind, string target, JObject args)
    {
        int limit = ObservationOutput.Limit(args), offset = ObservationOutput.Offset(args);
        bool details = (bool?)args["include_details"] ?? false;
        string contains = (string)args["contains"];
        Func<string, bool> matches = s => string.IsNullOrEmpty(contains) || (s ?? "").IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0;
        JToken[] values;
        if (kind == "mods") values = ModLoader.Mods.Where(m => (string.IsNullOrEmpty(target) || m.Manifest.ID == target) && matches(m.Manifest.ID)).Select(m => {
            var patches = m.HarmonyInstance?.GetPatchedMethods().ToArray();
            var v = JObject.FromObject(new { id = m.Manifest.ID, version = m.Manifest.ModVersion, enabled = m.IsEnabled,
                assembly = m.Assembly?.GetName().Name, entry = m.Instance?.GetType().FullName, loadError = m.LoadError,
                hotReloadHooksPresent = ReleaseInfo.DynamicModules ? (bool?)NativeModReload.HasHooks(m.Instance) : null,
                hotReloadVerification = ReleaseInfo.DynamicModules ? "not_checked" : "full_edition_required", patchCount = patches?.Length ?? 0 });
            if (details) { v["folder"] = m.FolderPath; v["patches"] = new JArray(patches?.Select(x => x.DeclaringType?.FullName + "." + x.Name) ?? Array.Empty<string>()); }
            return (JToken)v;
        }).ToArray();
        else if (kind == "patches") values = Harmony.GetAllPatchedMethods().Where(m => (string.IsNullOrEmpty(target) || (m.DeclaringType?.FullName + "." + m.Name).Contains(target)) && matches(m.DeclaringType?.FullName + "." + m.Name)).Select(m => {
            var p = Harmony.GetPatchInfo(m); var patches = p.Prefixes.Concat(p.Postfixes).Concat(p.Transpilers).Concat(p.Finalizers).ToArray();
            var v = JObject.FromObject(new { method = m.ToString(), type = m.DeclaringType?.FullName, patchCount = patches.Length, owners = patches.Select(x => x.owner).Distinct().ToArray() });
            if (details) v["patches"] = JArray.FromObject(patches.Select(x => new { owner = x.owner, priority = x.priority, before = x.before, after = x.after, patch = x.PatchMethod.ToString(), assembly = x.PatchMethod.DeclaringType?.Assembly.GetName().Name }));
            return (JToken)v;
        }).ToArray();
        else if (kind == "events") values = typeof(ModHook).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).Where(f => typeof(Delegate).IsAssignableFrom(f.FieldType) && matches(f.Name)).OrderBy(f => f.Name, StringComparer.Ordinal).Select(f => {
            var handlers = (f.GetValue(null) as Delegate)?.GetInvocationList() ?? Array.Empty<Delegate>();
            if (!string.IsNullOrEmpty(target)) handlers = handlers.Where(d => (d.Method.DeclaringType?.Assembly.GetName().Name ?? "").Contains(target)).ToArray();
            var v = new JObject { ["name"] = f.Name, ["handlerCount"] = handlers.Length };
            if (details) v["handlers"] = JArray.FromObject(handlers.Select(d => new { method = d.Method.ToString(), assembly = d.Method.DeclaringType?.Assembly.GetName().Name }));
            return (JToken)v;
        }).Where(v => string.IsNullOrEmpty(target) || (int)v["handlerCount"] > 0).ToArray();
        else if (kind == "config")
        {
            var c = ModConfig.Find(target ?? "");
            var page = ObservationOutput.Page(c?.Entries.Where(e => matches(e.Key)).Select(e => (JToken)JObject.FromObject(new { key = e.Key, value = ConfigEntry.Format(e.BoxedValue) })) ?? Array.Empty<JToken>(), offset, limit);
            page["found"] = c != null; page["detailsIncluded"] = details; return page;
        }
        else if (kind == "item")
        {
            if (!PlayerStore.HasInstance || NewGameData.Instance == null || NewGameData.Instance.isInMainMenu || EmporiumEntry.Instance == null) throw new InvalidOperationException("Load a save before inspecting item factories");
            if (string.IsNullOrEmpty(target)) throw new ArgumentException("Item ID required");
            var factory = DirectoryMaster.GetFactory<GameItem>(target);
            var all = EmporiumEntry.Instance.GetAllOwnedItems().Where(i => i.identifier == target && matches(i.name)).ToArray();
            // Page actual instances; never silently stop at 100.
            var page = ObservationOutput.Page(all.Select(item => (JToken)JObject.FromObject(new { name = item.name, count = item.unitCount })), offset, limit);
            if (details) for (int n = 0; n < ((JArray)page["entries"]).Count; n++) {
                var item = all[offset + n];
                page["entries"][n]["callbacks"] = JArray.FromObject(typeof(GameItemFunc).GetProperties().Where(p => typeof(Delegate).IsAssignableFrom(p.PropertyType)).Select(p => new {
                    name = p.Name, handlers = (p.GetValue(item, null) as Delegate)?.GetInvocationList().Select(d => new { method = d.Method.ToString(), assembly = d.Method.DeclaringType?.Assembly.GetName().Name }).ToArray() }));
            }
            page["id"] = target; page["factory"] = factory?.Method.ToString(); page["assembly"] = factory?.Method.DeclaringType?.Assembly.GetName().Name;
            page["detailsIncluded"] = details; return page;
        }
        else throw new ArgumentException("kind must be mods, patches, events, config or item");
        var result = ObservationOutput.Page(values, offset, limit); result["detailsIncluded"] = details; return result;
    }
}
