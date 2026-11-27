using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ModDevBridge;

internal sealed class UiQueries
{
    private sealed class Reference { public string Id, World; public WeakReference Object; public DateTime Issued; }
    readonly List<Reference> references = new List<Reference>();
    readonly Func<DateTime> clock;
    public UiQueries(Func<DateTime> clock = null) { this.clock = clock ?? (() => DateTime.UtcNow); }
    internal string Remember(GameObject go, string world)
    {
        references.RemoveAll(r => r.World != world || clock() - r.Issued > TimeSpan.FromMinutes(5) || r.Object.Target as GameObject == null);
        var old = references.FirstOrDefault(r => ReferenceEquals(r.Object.Target, go));
        if (old != null) return old.Id;
        var entry = new Reference { Id = Guid.NewGuid().ToString("N"), World = world, Object = new WeakReference(go), Issued = clock() };
        references.Add(entry); while (references.Count > 512) references.RemoveAt(0); return entry.Id;
    }
    internal GameObject Resolve(string id, string world)
    {
        id = UiActionReference.ObjectId(id);
        var entry = references.FirstOrDefault(r => r.Id == id);
        if (entry == null || clock() - entry.Issued > TimeSpan.FromMinutes(5)) throw new BridgeFailure("ui_reference_unavailable", "UI reference expired, was evicted or belongs to another process; query again", "ui.lookup");
        if (entry.World != world) throw new BridgeFailure("ui_world_mismatch", "UI reference belongs to a previous world; query again", "ui.lookup");
        var go = entry.Object.Target as GameObject;
        if (go == null) throw new BridgeFailure("ui_object_destroyed", "Referenced Unity object was destroyed; a rebuilt object requires a new reference", "ui.lookup");
        return go;
    }
    internal static string Path(Transform transform, out bool complete)
    {
        var parts = new List<string>(); var p = transform;
        while (p != null && parts.Count < 128) { parts.Add(p.name + "[" + p.GetSiblingIndex() + "]"); p = p.parent; }
        complete = p == null; parts.Reverse();
        return transform.gameObject.scene.name + "#" + transform.gameObject.scene.handle + ":/" + (complete ? "" : "<ancestors omitted>/") + string.Join("/", parts);
    }
    internal static string TextValue(GameObject go)
    {
        var tmpInput = go.GetComponent<TMP_InputField>(); if (tmpInput != null) return tmpInput.text;
        var input = go.GetComponent<InputField>(); if (input != null) return input.text;
        var tmp = go.GetComponent<TMP_Text>(); if (tmp != null) return tmp.text;
        return go.GetComponent<Text>()?.text;
    }
    JObject Summary(GameObject go, string world)
    {
        string path = Path(go.transform, out bool complete);
        return new JObject { ["uiRef"] = Remember(go, world), ["name"] = go.name, ["path"] = path, ["pathComplete"] = complete,
            ["instanceId"] = go.GetInstanceID(), ["activeSelf"] = go.activeSelf, ["activeInHierarchy"] = go.activeInHierarchy,
            ["componentTypes"] = new JArray(go.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().FullName)), ["text"] = TextValue(go) };
    }
    public JObject Find(string world, JObject args) => UiDiscovery.Find(this, world, args);
    static JObject V2(Vector2 value) => new JObject { ["x"] = value.x, ["y"] = value.y };
    internal Vector2 Center(string reference, string world)
    {
        var go = Resolve(reference,world); var rect = go.transform as RectTransform;
        var canvas = go.GetComponentInParent<Canvas>(true);
        if (rect == null || canvas == null || canvas.targetDisplay != 0 ||
            canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera == null)
            throw new BridgeFailure("ui_center_unavailable","Target needs a RectTransform and a primary-display Canvas with a usable camera; supply explicit coordinates if appropriate","ui.preflight");
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));
    }
    static JObject RectValue(Rect value) => new JObject { ["x"] = value.x, ["y"] = value.y, ["width"] = value.width, ["height"] = value.height };
    public JObject Details(string world, JObject args)
    {
        var go = Resolve((string)args["ui_ref"],world); var value = Summary(go,world); value["worldId"] = world;
        value["observation"] = UiObservation.Read(go,this,world);
        var components = go.GetComponents<Component>();
        value["components"] = new JArray(components.Where(c => c != null).Select(c => new JObject { ["type"] = c.GetType().FullName,
            ["enabled"] = c is Behaviour b ? new JValue(b.enabled) : JValue.CreateNull(), ["isActiveAndEnabled"] = c is Behaviour b2 ? new JValue(b2.isActiveAndEnabled) : JValue.CreateNull() }));
        var selectable = go.GetComponent<Selectable>();
        if (selectable != null) value["selectable"] = new JObject { ["interactable"] = selectable.interactable, ["isInteractable"] = selectable.IsInteractable(), ["enabled"] = selectable.enabled };
        var toggle = go.GetComponent<Toggle>(); if (toggle != null) value["toggle"] = new JObject { ["isOn"] = toggle.isOn };
        var slider = go.GetComponent<Slider>(); if (slider != null) value["slider"] = new JObject { ["value"] = slider.value, ["min"] = slider.minValue, ["max"] = slider.maxValue, ["wholeNumbers"] = slider.wholeNumbers };
        var scrollbar = go.GetComponent<Scrollbar>(); if (scrollbar != null) value["scrollbar"] = new JObject { ["value"] = scrollbar.value, ["size"] = scrollbar.size };
        var input = go.GetComponent<InputField>(); if (input != null) value["input"] = new JObject { ["text"] = input.text, ["readOnly"] = input.readOnly, ["isFocused"] = input.isFocused };
        var tmpInput = go.GetComponent<TMP_InputField>(); if (tmpInput != null) value["input"] = new JObject { ["text"] = tmpInput.text, ["readOnly"] = tmpInput.readOnly, ["isFocused"] = tmpInput.isFocused };
        value["graphics"] = new JArray(go.GetComponents<Graphic>().Select(g => new JObject { ["type"] = g.GetType().FullName,
            ["enabled"] = g.enabled, ["raycastTarget"] = g.raycastTarget, ["colorAlpha"] = g.color.a, ["canvasRendererCull"] = g.canvasRenderer.cull }));
        var canvas = go.GetComponentInParent<Canvas>(true);
        if (canvas != null) value["canvas"] = new JObject { ["uiRef"] = Remember(canvas.gameObject,world), ["enabled"] = canvas.enabled,
            ["sortingOrder"] = canvas.sortingOrder, ["sortingLayerId"] = canvas.sortingLayerID, ["overrideSorting"] = canvas.overrideSorting,
            ["renderMode"] = canvas.renderMode.ToString(), ["targetDisplay"] = canvas.targetDisplay, ["worldCamera"] = canvas.worldCamera?.name };
        var rect = go.transform as RectTransform;
        if (rect != null)
        {
            var geometry = new JObject { ["localRect"] = RectValue(rect.rect), ["anchoredPosition"] = V2(rect.anchoredPosition), ["anchorMin"] = V2(rect.anchorMin), ["anchorMax"] = V2(rect.anchorMax), ["pivot"] = V2(rect.pivot) };
            if (canvas != null && (canvas.renderMode == RenderMode.ScreenSpaceOverlay || canvas.worldCamera != null))
            {
                var corners = new Vector3[4]; rect.GetWorldCorners(corners); var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                var points = corners.Select(c => RectTransformUtility.WorldToScreenPoint(camera,c)).ToArray();
                geometry["screenBounds"] = RectValue(Rect.MinMaxRect(points.Min(p => p.x),points.Min(p => p.y),points.Max(p => p.x),points.Max(p => p.y)));
                geometry["screenCorners"] = new JArray(points.Select(V2)); geometry["boundsKind"] = "axis_aligned_projection_not_visibility";
            }
            else geometry["screenBoundsUnavailable"] = canvas == null ? "no_canvas" : "canvas_camera_unassigned";
            value["rect"] = geometry;
        }
        var groups = new JArray(); var parent = go.transform; int levels = 0;
        while (parent != null && levels++ < 64)
        {
            foreach (var group in parent.GetComponents<CanvasGroup>()) groups.Add(new JObject { ["path"] = Path(parent,out _), ["enabled"] = group.enabled,
                ["alpha"] = group.alpha, ["interactable"] = group.interactable, ["blocksRaycasts"] = group.blocksRaycasts, ["ignoreParentGroups"] = group.ignoreParentGroups });
            parent = parent.parent;
        }
        value["ancestorCanvasGroups"] = groups; value["ancestorGroupsComplete"] = parent == null;
        value["visibility"] = "not_verified"; value["clickability"] = "not_verified";
        value["screenCoordinates"] = "Unity screen pixels, origin bottom-left; target Canvas display; projected bounds do not prove visibility, occlusion or clickability.";
        return value;
    }
    public JObject Raycast(string world, JObject args)
    {
        int limit = ObservationOutput.Limit(args), offset = ObservationOutput.Offset(args);
        double x = (double?)args["x"] ?? double.NaN, y = (double?)args["y"] ?? double.NaN;
        if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y) || x < 0 || y < 0 || x > Screen.width || y > Screen.height) throw new ArgumentException("x/y must be finite pixels inside the primary game screen");
        var system = EventSystem.current;
        if (system == null) throw new BridgeFailure("event_system_unavailable", "No active Unity EventSystem; no hit query was performed", "ui.raycast");
        var target = args["target_ref"] == null ? null : Resolve((string)args["target_ref"],world);
        var hits = new List<RaycastResult>(); system.RaycastAll(new PointerEventData(system) { position = new Vector2((float)x,(float)y) },hits);
        var result = ObservationOutput.Page(hits.Select((hit, index) => (JToken)new JObject { ["rank"] = index,
            ["uiRef"] = hit.gameObject == null ? null : Remember(hit.gameObject,world), ["path"] = hit.gameObject == null ? null : Path(hit.gameObject.transform,out _),
            ["module"] = hit.module?.GetType().FullName, ["distance"] = hit.distance, ["depth"] = hit.depth, ["sortingLayer"] = hit.sortingLayer,
            ["sortingOrder"] = hit.sortingOrder }),offset,limit);
        result["worldId"] = world; result["point"] = new JObject { ["x"] = x,["y"] = y };
        result["screen"] = new JObject { ["width"] = Screen.width,["height"] = Screen.height,["origin"] = "bottom-left",["display"] = 0 };
        if (target != null)
        {
            int index = hits.FindIndex(h => h.gameObject != null && (h.gameObject == target || h.gameObject.transform.IsChildOf(target.transform)));
            result["target"] = new JObject { ["uiRef"] = (string)args["target_ref"], ["firstHitWithinTarget"] = index < 0 ? JValue.CreateNull() : new JValue(index), ["topHitWithinTarget"] = index == 0 };
        }
        result["clickability"] = "not_verified";
        result["interpretation"] = "EventSystem candidate order at this primary-display point; no pointer events dispatched. Custom raycasters execute their own query logic. A candidate does not prove a successful click.";
        return result;
    }
}
