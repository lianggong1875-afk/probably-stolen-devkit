using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ModDevBridge;

// Reads and applies native ModConfig entries without rebuilding the MOD.
// ApplyBoxed is used for in-memory changes; Config.Save is only called when
// persist=true. FireOnModConfigApplied mirrors the native config window.
internal static class ConfigService
{
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

    public static object List()
    {
        var field = typeof(ModConfig).GetField("configs", StaticPrivate);
        var configs = field?.GetValue(null) as IDictionary;
        var mods = configs == null
            ? Array.Empty<string>()
            : configs.Keys.Cast<object>().Select(key => key.ToString()).OrderBy(key => key, StringComparer.Ordinal).ToArray();
        return new { mods, count = mods.Length };
    }

    public static object Get(string modId, bool includeLabels)
    {
        if (string.IsNullOrEmpty(modId)) throw new ArgumentException("modId is required");
        ModConfig config;
        try { config = ModConfig.Find(modId); }
        catch (Exception error) { throw new ArgumentException("Unknown or invalid mod id '" + modId + "': " + error.Message); }
        if (config == null) return new { modId, found = false, status = "not_found", entries = Array.Empty<object>() };
        var entries = config.Entries.Select(entry => Describe(entry, includeLabels)).ToArray();
        return new { modId = config.ModId, file = config.FilePath, found = true, hasEntries = config.HasEntries, entries };
    }

    // The native Save catches IO exceptions. Only a target-key readback can
    // establish persistence; it cannot prove callbacks or byte-for-byte rollback.
    static JObject Disk(ModConfig config, string key)
    {
        var r = new JObject { ["fileExists"] = false,["entryFound"] = false,["readable"] = true,["value"] = null };
        try
        {
            if (!File.Exists(config.FilePath)) return r;
            r["fileExists"] = true;
            if (new FileInfo(config.FilePath).Length > 1024*1024) throw new IOException("Config exceeds 1 MiB readback limit");
            var entries = JObject.Parse(File.ReadAllText(config.FilePath))["entries"] as JArray;
            if (entries == null) throw new IOException("Config entries array is absent");
            var matching = entries.Where(e => (string)e["key"] == key).ToArray();
            if (matching.Length > 1) throw new IOException("Duplicate config key in file");
            if (matching.Length == 1)
            {
                if (matching[0]["value"]?.Type != JTokenType.String) throw new IOException("Config value is not a serialized string");
                r["entryFound"] = true; r["value"] = matching[0]["value"];
            }
        }
        catch (Exception error) { r["readable"] = false; r["error"] = BridgeFailure.Describe(error,"config.readback"); }
        return r;
    }
    static bool Matches(JObject disk, string value) => (bool)disk["readable"] && (bool)disk["entryFound"] && (string)disk["value"] == value;
    static bool SameDisk(JObject a, JObject b) => (bool)a["readable"] && (bool)b["readable"] &&
        JToken.DeepEquals(a["fileExists"],b["fileExists"]) && JToken.DeepEquals(a["entryFound"],b["entryFound"]) && JToken.DeepEquals(a["value"],b["value"]);
    public static object Set(string modId, string key, JToken value, bool persist, bool track, ResourceScope owner, Func<bool> sameWorld)
    {
        if (track && owner == null) throw new BridgeFailure("session_required","Tracking requires an explicit owning session","config.preflight");
        if (string.IsNullOrEmpty(modId) || string.IsNullOrEmpty(key)) throw new ArgumentException("modId and key are required");
        if (value == null || value.Type == JTokenType.Object || value.Type == JTokenType.Array) throw new ArgumentException("Config value must be a scalar");
        var config = ModConfig.Find(modId) ?? throw new BridgeFailure("config_not_found","Config not registered: "+modId,"config.preflight");
        var entry = config.Get(key) ?? throw new BridgeFailure("config_key_not_found","Config key not registered: "+key,"config.preflight");
        var converted = ConvertValue(value,entry.ValueType); var before = entry.BoxedValue;
        string beforeSerialized = entry.Serialize();
        var baselineDisk = persist ? Disk(config,key) : null;
        if (track && persist && !(bool)baselineDisk["readable"])
            throw new BridgeFailure("config_baseline_unreadable","Cannot guard tracked persistence without readable disk state","config.preflight",baselineDisk);
        var result = new JObject { ["modId"] = modId,["key"] = key,["file"] = config.FilePath,["type"] = entry.ValueType.Name,
            ["before"] = ConfigEntry.Format(before),["requested"] = ConfigEntry.Format(converted),["changed"] = false,["tracked"] = false,
            ["persisted"] = false,["persistenceRequested"] = persist,["saveAttempted"] = false,["persistence"] = "not_requested",
            ["notified"] = false,["effectVerification"] = "not_checked",
            ["restorationScope"] = persist ? "Before-memory entry value; if Save attempted, also persisted on cleanup. Not original file bytes/existence or other entries" : "Before-memory entry value" };
        if (persist) result["diskBefore"] = baselineDisk;
        object applied = before; bool memoryChanged = false, diskTouched = false;
        JObject lastDisk = baselineDisk;
        if (track)
            owner.Track("config:"+modId+":"+key,() => {
                if (!memoryChanged && !diskTouched) return;
                if (!sameWorld()) throw new CleanupSkippedException("Session world changed; config preserved");
                if (!ReferenceEquals(ModConfig.Find(modId),config) || !ReferenceEquals(config.Get(key),entry)) throw new CleanupSkippedException("Config registration replaced");
                if (!Equals(entry.BoxedValue,applied)) throw new CleanupSkippedException("Config changed after tracked write; later value preserved");
                if (diskTouched && !SameDisk(Disk(config,key),lastDisk)) throw new CleanupSkippedException("Persisted entry changed after tracked write; later value preserved");
                try { Apply(entry,before); } finally { applied = entry.BoxedValue; }
                if (!Equals(applied,before)) throw new InvalidOperationException("Config memory restoration mismatch");
                if (diskTouched)
                {
                    try { config.Save(); } finally { lastDisk = Disk(config,key); }
                    if (!Matches(lastDisk,beforeSerialized)) throw new InvalidOperationException("Config restoration persistence readback mismatch");
                }
                if (memoryChanged)
                {
                    var notice = Notify(modId);
                    if (!(bool)notice["returned"]) throw new InvalidOperationException("Config restoration notification failed: "+notice.ToString());
                }
            });
        string stage = "config.apply";
        try
        {
            try { Apply(entry,converted); }
            finally
            {
                applied = entry.BoxedValue; memoryChanged = !Equals(before,applied);
                result["after"] = ConfigEntry.Format(applied); result["changed"] = memoryChanged;
                result["requestedMatched"] = Equals(applied,converted); result["tracked"] = track && memoryChanged;
            }
            Exception saveError = null;
            if (persist)
            {
                stage = "config.save";
                if (!Matches(baselineDisk,entry.Serialize()))
                {
                    diskTouched = true; result["saveAttempted"] = true; result["persistence"] = "unknown";
                    try { config.Save(); } catch (Exception error) { saveError = error; result["saveError"] = BridgeFailure.Describe(error,stage); }
                }
                lastDisk = Disk(config,key); result["readback"] = lastDisk;
                result["persisted"] = Matches(lastDisk,entry.Serialize());
                result["persistence"] = (bool)result["persisted"] ? "verified_value" : "unverified";
            }
            stage = "config.notify";
            var notification = memoryChanged ? Notify(modId) : new JObject { ["invoked"] = false,["returned"] = true,["reason"] = "memory_unchanged" };
            result["notification"] = notification; result["notified"] = memoryChanged && (bool)notification["returned"];
            if (saveError != null || persist && !(bool)result["persisted"]) { stage = "config.save"; throw new InvalidOperationException("Persistence was not confirmed by readback",saveError); }
            if (!(bool)notification["returned"]) throw new InvalidOperationException("Config notification failed");
            result["completed"] = true;
            return result;
        }
        catch (Exception error) { result["completed"] = false; throw new BridgeFailure("config_write_failed",error.Message,stage,result,inner:error); }
        finally { result["tracked"] = track && (memoryChanged || diskTouched); }
    }

    private static object Describe(ConfigEntry entry, bool includeLabels)
    {
        object value = entry.BoxedValue is Enum ? entry.BoxedValue.ToString() : entry.BoxedValue;
        object defaultValue = entry.BoxedDefault is Enum ? entry.BoxedDefault.ToString() : entry.BoxedDefault;
        string label = null;
        if (includeLabels && entry.Label != null)
        {
            try { label = entry.Label(); } catch { }
        }
        return new
        {
            key = entry.Key,
            section = entry.Section,
            type = entry.ValueType.Name,
            value,
            defaultValue,
            min = entry.HasRange ? (float?)entry.Min : null,
            max = entry.HasRange ? (float?)entry.Max : null,
            choices = entry.Choices,
            requiresRestart = entry.RequiresRestart,
            label
        };
    }

    private static object ConvertValue(JToken token, Type type)
    {
        try
        {
            if (type == typeof(bool))
                return token.Type == JTokenType.Boolean ? token.Value<bool>() : bool.Parse(token.ToString());
            if (type == typeof(int))
                return token.Type == JTokenType.Integer
                    ? token.Value<int>()
                    : int.Parse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture);
            if (type == typeof(float))
            {
                float number = token.Type == JTokenType.Float || token.Type == JTokenType.Integer
                    ? token.Value<float>()
                    : float.Parse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture);
                if (float.IsNaN(number) || float.IsInfinity(number))
                    throw new ArgumentException("float value must be finite");
                return number;
            }
            if (type == typeof(string))
                return token.Type == JTokenType.Null ? "" : token.Value<string>() ?? token.ToString();
            if (type.IsEnum)
                return Enum.Parse(type, token.ToString(), ignoreCase: true);
            return Convert.ChangeType(token.ToString(), type, CultureInfo.InvariantCulture);
        }
        catch (Exception error)
        {
            throw new ArgumentException("Cannot convert '" + token + "' to " + type.Name + ": " + error.Message);
        }
    }

    private static bool Apply(ConfigEntry entry, object value)
    {
        var method = typeof(ConfigEntry).GetMethod("ApplyBoxed", BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null) throw new InvalidOperationException("ConfigEntry.ApplyBoxed is unavailable in this game build");
        try { return (bool)method.Invoke(entry, new[] { value }); }
        catch (TargetInvocationException error)
        {
            throw new InvalidOperationException("Config apply failed: " + (error.InnerException?.Message ?? error.Message));
        }
    }

    private static JObject Notify(string modId)
    {
        var method = typeof(ModHook).GetMethod("FireOnModConfigApplied",StaticPrivate);
        var result = new JObject { ["available"] = method != null,["invoked"] = false,["returned"] = false,["effectVerification"] = "not_checked" };
        if (method == null) return result;
        result["invoked"] = true;
        try { method.Invoke(null,new object[] { modId }); result["returned"] = true; }
        catch (Exception error) { result["error"] = BridgeFailure.Describe(error is TargetInvocationException wrapper ? wrapper.InnerException ?? error : error,"config.notify"); }
        return result;
    }
}
