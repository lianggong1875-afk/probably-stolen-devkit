using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace ModDevBridge;

internal static class LocalizationQueries
{
    public static JObject Read(JObject args)
    {
        string tableName = (string)args["table"];
        var keys = (args["keys"] as JArray)?.Values<string>().ToArray();
        var values = args["arguments"] as JArray ?? new JArray();
        if (string.IsNullOrWhiteSpace(tableName) || tableName.Length > 128 || keys == null || keys.Length < 1 || keys.Length > 50 || keys.Any(k => string.IsNullOrWhiteSpace(k) || k.Length > 256))
            throw new ArgumentException("Use a table name (1..128 characters) and 1..50 nonempty keys (at most 256 characters each)");
        if (values.Count > 16 || values.Any(v => v.Type != JTokenType.String && v.Type != JTokenType.Integer && v.Type != JTokenType.Float && v.Type != JTokenType.Boolean) || values.Any(v => v.Type == JTokenType.String && v.Value<string>().Length > 2000))
            throw new ArgumentException("arguments must contain up to 16 scalar values; strings at most 2000 characters");
        var selected = LocalizationSettings.SelectedLocale;
        var db = LocalizationSettings.StringDatabase;
        if (selected == null || db == null) throw new BridgeFailure("localization_unavailable","Native localization is not initialized","localization.preflight");
        string selectedCode = selected.Identifier.Code, requestedCode = (string)args["locale"] ?? selectedCode;
        var locale = args["locale"] == null ? selected : LocalizationSettings.AvailableLocales.GetLocale(requestedCode);
        if (locale == null) throw new BridgeFailure("locale_not_found","Requested locale is not installed: "+requestedCode,"localization.preflight");
        requestedCode = locale.Identifier.Code;
        IList<object> parameters = values.Select(v => ((JValue)v).Value).ToArray();
        var entries = new JArray(); bool complete = true;
        foreach (string key in keys)
        {
            var row = new JObject { ["key"] = key,["found"] = null,["existsInRequestedLocale"] = null,
                ["resolvedLocale"] = null,["fallbackUsed"] = null,["value"] = null,["rawValue"] = null };
            entries.Add(row);
            try
            {
                var localTable = db.GetTable(tableName,locale);
                row["existsInRequestedLocale"] = localTable?.GetEntry(key) != null;
                var resolved = db.GetTableEntry(tableName,key,locale,FallbackBehavior.UseProjectSettings);
                bool found = resolved.Entry != null;
                row["found"] = found; row["rawValue"] = resolved.Entry?.Value;
                string resolvedCode = resolved.Table == null ? null : resolved.Table.LocaleIdentifier.Code;
                row["resolvedLocale"] = found ? resolvedCode : null;
                row["fallbackUsed"] = found && resolvedCode != null ? (bool?)(resolvedCode != requestedCode) : null;
                string text = db.GetLocalizedString(tableName,key,parameters,locale,FallbackBehavior.UseProjectSettings);
                row["value"] = text;
                if (found && text == null) throw new InvalidOperationException("Native formatter returned null; raw entry is retained, formatted value is unverified");
            }
            catch (Exception error) { complete = false; row["error"] = BridgeFailure.Describe(error,"localization.read"); }
        }
        return new JObject { ["table"] = tableName,["selectedLocale"] = selectedCode,["requestedLocale"] = requestedCode,
            ["selectedLocaleAfter"] = LocalizationSettings.SelectedLocale?.Identifier.Code,["entries"] = entries,["completed"] = complete,
            ["localeChangedByTool"] = false,["valueSource"] = "Unity StringDatabase using native formatting and project fallback settings",
            ["scope"] = "String-table lookup only; item-cached strings, mod delegates and LocHelper error/raw fallback may differ" };
    }
}
