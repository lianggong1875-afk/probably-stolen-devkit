using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ModDevBridge;

// A bounded content precondition, not a durable business identity. Geometry is
// deliberately absent: moving a window must not invalidate its controls.
internal static class UiObservation
{
    internal static GameObject Scope(GameObject target)
    {
        var parent = target.transform.parent;
        for (int i = 0; parent != null && i < 3; i++, parent = parent.parent)
        {
            if (parent.GetComponent<Canvas>() != null) break;
            if (parent.GetComponent<Selectable>() != null || parent.GetComponent<IPointerClickHandler>() != null)
                return parent.gameObject;
        }
        return target;
    }
    internal static JObject Read(GameObject target, UiQueries refs, string world)
    {
        var scope = Scope(target);
        var result = new JObject { ["complete"] = false, ["scopeRef"] = refs.Remember(scope,world), ["actionRef"] = null };
        var pending = new Queue<Tuple<Transform,int>>(); pending.Enqueue(Tuple.Create(scope.transform,0));
        var stamp = new StringBuilder(); int count = 0, textSize = 0;
        Action<string> add = value => { value = value ?? ""; stamp.Append(value.Length).Append(':').Append(value); };
        add(world); add(target.GetInstanceID().ToString());
        while (pending.Count > 0)
        {
            var next = pending.Dequeue(); var transform = next.Item1;
            if (transform == null) { result["reason"] = "object_disappeared"; return result; }
            var go = transform.gameObject;
            if (++count > 256) { result["reason"] = "node_limit"; return result; }
            add(go.GetInstanceID().ToString()); add(go.name); add(go.activeInHierarchy.ToString());
            // Hidden branches are irrelevant until activated. Their root active
            // flag is still part of the stamp, so activation is detected.
            if (!go.activeInHierarchy) continue;
            string text = UiQueries.TextValue(go);
            textSize += text?.Length ?? 0;
            if (textSize > 8192) { result["reason"] = "text_limit"; return result; }
            add(text);
            if (next.Item2 >= 12 && transform.childCount > 0) { result["reason"] = "depth_limit"; return result; }
            for (int i = 0; i < transform.childCount; i++)
            {
                if (count + pending.Count >= 256) { result["reason"] = "node_limit"; return result; }
                pending.Enqueue(Tuple.Create(transform.GetChild(i),next.Item2+1));
            }
        }
        using (var sha = SHA256.Create())
            result["actionRef"] = UiActionReference.Pack(refs.Remember(target,world),BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(stamp.ToString()))).Replace("-", "").ToLowerInvariant());
        result["complete"] = true;
        return result;
    }
    internal static void Check(UiQueries refs, string reference, string world)
    {
        if (reference == null) return;
        string token = UiActionReference.Token(reference);
        if (string.IsNullOrEmpty(token))
            throw new BridgeFailure("ui_observation_required","Use targetRef from a fresh query_ui result; for drag destinations use observation.actionRef. Bare uiRef is for reads, not actions.","ui.preflight",
                hint:"Pass the returned action reference unchanged in the existing target_ref/to_ref string parameter. If the host shows outdated tool definitions, refresh its MCP tools; direct JSON-RPC is not required.");
        var current = Read(refs.Resolve(reference,world),refs,world);
        if (!(bool)current["complete"])
            throw new BridgeFailure("ui_observation_incomplete","Target content could not be fully checked; narrow the target or inspect the UI before using explicit coordinates","ui.preflight",current);
        if (!string.Equals(token,UiActionReference.Token((string)current["actionRef"]),StringComparison.Ordinal))
            throw new BridgeFailure("ui_observation_changed","Target or nearby container content changed since observation; query again before acting","ui.preflight",new { scopeRef = (string)current["scopeRef"] });
    }
}
