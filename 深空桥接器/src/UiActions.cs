using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ModDevBridge;

// Standard uGUI pointer lifecycle, not OS input or a game-specific load-save API.
// No pointer is left held between separate MCP calls. Iterator disposal releases
// without clicking/dropping on cancellation, shutdown, world change or failure.
internal static class UiActions
{
    static Vector2 Point(JObject args, string xKey, string yKey)
    {
        double x = (double?)args[xKey] ?? double.NaN, y = (double?)args[yKey] ?? double.NaN;
        if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y) || x < 0 || y < 0 || x > Screen.width || y > Screen.height)
            throw new ArgumentException(xKey + "/" + yKey + " must be finite primary-screen pixels inside the game screen");
        return new Vector2((float)x,(float)y);
    }
    static RaycastResult Hit(EventSystem system, PointerEventData data)
    {
        var hits = new List<RaycastResult>(); system.RaycastAll(data,hits);
        return hits.Count == 0 ? new RaycastResult() : hits[0];
    }
    public static IEnumerator Run(string world, JObject args, UiQueries ui, OperationQueue queue, Func<string> currentWorld)
    {
        string action = (string)args["action"];
        if (action != "click" && action != "drag") throw new ArgumentException("action must be click or drag");
        bool coordinates = args["x"] != null || args["y"] != null;
        if (!coordinates && args["target_ref"] == null) throw new ArgumentException("Supply target_ref or both x/y");
        UiObservation.Check(ui,(string)args["target_ref"],world);
        UiObservation.Check(ui,(string)args["to_ref"],world);
        if (args["to_ref"] != null && (args["to_x"] != null || args["to_y"] != null)) throw new ArgumentException("Use to_ref or to_x/to_y, not both");
        Vector2 start = coordinates ? Point(args,"x","y") : ui.Center((string)args["target_ref"],world);
        Vector2 end = action == "drag" ? args["to_ref"] == null ? Point(args,"to_x","to_y") : ui.Center((string)args["to_ref"],world) : start;
        // Projected centers are current geometry, not proof of a usable hit.
        Point(new JObject { ["x"] = start.x,["y"] = start.y },"x","y");
        Point(new JObject { ["x"] = end.x,["y"] = end.y },"x","y");
        int frames = (int?)args["frames"] ?? 12;
        if (frames < 1 || frames > 120) throw new ArgumentException("frames must be 1..120");
        if (action == "click" && (args["to_x"] != null || args["to_y"] != null || args["to_ref"] != null)) throw new ArgumentException("to_ref/to_x/to_y only apply to drag");
        var system = EventSystem.current;
        if (system == null) throw new BridgeFailure("event_system_unavailable","No active EventSystem","ui.preflight");
        var data = new PointerEventData(system) { pointerId = -1001,button = PointerEventData.InputButton.Left,position = start,pressPosition = start,
            clickCount = 1,clickTime = Time.unscaledTime,useDragThreshold = false,eligibleForClick = true };
        data.pointerCurrentRaycast = Hit(system,data); data.pointerPressRaycast = data.pointerCurrentRaycast;
        var initial = data.pointerCurrentRaycast.gameObject;
        if (initial == null) throw new BridgeFailure("ui_no_hit","Nothing was hit at the start point; no press dispatched","ui.preflight");
        var expected = args["target_ref"] == null ? null : ui.Resolve((string)args["target_ref"],world);
        if (expected != null && initial != expected && !initial.transform.IsChildOf(expected.transform))
            throw new BridgeFailure("ui_target_obstructed","Top candidate is outside target_ref; no press dispatched","ui.preflight",new { topHitRef = ui.Remember(initial,world),topHitName = initial.name });
        // Also reject a label/child reference whose nearest handler is outside
        // that subtree. query_ui.targetRef supplies the owning control instead.
        var selectable = initial.GetComponentInParent<Selectable>();
        if (selectable != null && (!selectable.isActiveAndEnabled || !selectable.IsInteractable()))
            throw new BridgeFailure("ui_not_interactable","Hit control is disabled or not interactable; no press dispatched","ui.preflight");
        var press = ExecuteEvents.GetEventHandler<IPointerDownHandler>(initial) ?? ExecuteEvents.GetEventHandler<IPointerClickHandler>(initial);
        var drag = ExecuteEvents.GetEventHandler<IDragHandler>(initial);
        if (expected != null)
            foreach (var handler in action == "drag" ? new[] { press,drag } : new[] { press })
                if (handler != null && handler != expected && !handler.transform.IsChildOf(expected.transform))
                    throw new BridgeFailure("ui_target_handler_outside","Pointer handler is outside target_ref; use the query row's targetRef to address its owning control","ui.preflight");
        if (action == "drag" && drag == null) throw new BridgeFailure("ui_not_draggable","No standard IDragHandler found; no press dispatched","ui.preflight");
        if (action == "click" && press == null) throw new BridgeFailure("ui_no_pointer_handler","No pointer down/click handler found; no press dispatched","ui.preflight");
        var events = new JArray();
        var report = new JObject { ["action"] = action,["worldId"] = world,["sourceRef"] = ui.Remember(initial,world),["sourceName"] = initial.name,
            ["startPoint"] = new JObject { ["x"] = start.x,["y"] = start.y },["endPoint"] = new JObject { ["x"] = end.x,["y"] = end.y },
            ["pointSource"] = coordinates ? "coordinates" : "target_center",
            ["observationVerified"] = args["target_ref"] != null, ["destinationObservationVerified"] = args["to_ref"] != null,
            ["pointerDownDispatched"] = false,["pointerUpDispatched"] = false,["clickDispatched"] = false,["dropDispatched"] = false,
            ["dragBegan"] = false,["dragEnded"] = false,["moveFrames"] = 0,["released"] = false,["events"] = events,
            ["inputModel"] = "synthetic_Unity_EventSystem_left_pointer",["effectVerification"] = "not_checked",
            ["limitation"] = "Does not change real mouse position/buttons. Handlers that poll Input.mousePosition/GetMouseButton may need a separate native-input path; no game action is inferred from dispatch." };
        queue.RoutineResult = report;
        var hovered = new List<GameObject>();
        bool pressed = false, dragging = false, finished = false;
        Action<GameObject> hover = next => {
            var chain = new List<GameObject>();
            for (var t = next == null ? null : next.transform; t != null; t = t.parent) chain.Add(t.gameObject);
            foreach (var old in hovered) if (old != null && !chain.Contains(old)) ExecuteEvents.Execute(old,data,ExecuteEvents.pointerExitHandler);
            foreach (var go in chain) if (!hovered.Contains(go)) ExecuteEvents.Execute(go,data,ExecuteEvents.pointerEnterHandler);
            hovered = chain; data.pointerEnter = next;
        };
        Action<bool> release = commit => {
            try
            {
                if (pressed)
                {
                    pressed = false;
                    if (data.pointerPress != null) { report["pointerUpDispatched"] = true; events.Add("pointer_up"); ExecuteEvents.Execute(data.pointerPress,data,ExecuteEvents.pointerUpHandler); }
                    else if (press != null) report["releaseHandlerDestroyed"] = true;
                }
                if (commit)
                {
                    try
                    {
                        UiObservation.Check(ui,(string)args["target_ref"],world);
                        UiObservation.Check(ui,(string)args["to_ref"],world);
                    }
                    catch (BridgeFailure error)
                    { throw new BridgeFailure(error.Code,error.Message,"ui.after_pointer_up",report,inner:error); }
                }
                if (commit && action == "click" && data.eligibleForClick && data.pointerPress != null &&
                    ExecuteEvents.GetEventHandler<IPointerClickHandler>(data.pointerCurrentRaycast.gameObject) == data.pointerPress)
                { report["clickDispatched"] = true; events.Add("pointer_click"); ExecuteEvents.Execute(data.pointerPress,data,ExecuteEvents.pointerClickHandler); }
                if (commit && dragging && data.pointerCurrentRaycast.gameObject != null)
                { events.Add("drop"); report["dropDispatched"] = ExecuteEvents.ExecuteHierarchy(data.pointerCurrentRaycast.gameObject,data,ExecuteEvents.dropHandler) != null; }
            }
            finally
            {
                try
                {
                    if (dragging)
                    {
                        dragging = false; events.Add("end_drag"); report["dragEnded"] = true;
                        if (data.pointerDrag != null) ExecuteEvents.Execute(data.pointerDrag,data,ExecuteEvents.endDragHandler);
                    }
                }
                finally
                {
                    data.dragging = false; data.eligibleForClick = false; data.pointerPress = null; data.rawPointerPress = null; data.pointerDrag = null;
                    report["released"] = true;
                }
            }
        };
        try
        {
            hover(initial);
            if (currentWorld() != world || EventSystem.current != system) throw new BridgeFailure("world_changed","World/EventSystem changed before press","ui.checkpoint",report);
            UiObservation.Check(ui,(string)args["target_ref"],world);
            var selectHandler = ExecuteEvents.GetEventHandler<ISelectHandler>(initial);
            if (system.currentSelectedGameObject != selectHandler) system.SetSelectedGameObject(null,data);
            data.pointerPress = press; data.rawPointerPress = initial; data.pointerDrag = drag;
            pressed = true; report["pointerDownDispatched"] = press != null; events.Add(press != null ? "pointer_down" : "press_without_handler");
            if (press != null) ExecuteEvents.Execute(press,data,ExecuteEvents.pointerDownHandler);
            if (drag != null) ExecuteEvents.Execute(drag,data,ExecuteEvents.initializePotentialDrag);
            yield return null;
            int moves = action == "drag" ? frames : 0;
            for (int i = 1; i <= moves; i++)
            {
                if (currentWorld() != world || EventSystem.current != system) throw new BridgeFailure("world_changed","World/EventSystem changed while pointer was held","ui.checkpoint",report);
                if (drag == null) throw new BridgeFailure("ui_object_destroyed","Drag handler disappeared while pointer was held","ui.drag",report);
                Vector2 next = Vector2.Lerp(start,end,(float)i/moves); data.delta = next - data.position; data.position = next;
                data.pointerCurrentRaycast = Hit(system,data); hover(data.pointerCurrentRaycast.gameObject);
                if (!dragging)
                {
                    dragging = true; data.dragging = true; data.eligibleForClick = false; report["dragBegan"] = true; events.Add("begin_drag");
                    ExecuteEvents.Execute(drag,data,ExecuteEvents.beginDragHandler);
                    if (data.pointerPress != data.pointerDrag && pressed)
                    {
                        pressed = false; report["pointerUpDispatched"] = true; events.Add("pointer_up_before_drag");
                        if (data.pointerPress != null) ExecuteEvents.Execute(data.pointerPress,data,ExecuteEvents.pointerUpHandler);
                        data.pointerPress = null; data.rawPointerPress = null;
                    }
                }
                ExecuteEvents.Execute(drag,data,ExecuteEvents.dragHandler); report["moveFrames"] = i;
                queue.ReportProgress(new { phase = "drag",completedFrames = i,totalFrames = moves });
                yield return null;
            }
            if (currentWorld() != world || EventSystem.current != system) throw new BridgeFailure("world_changed","World/EventSystem changed before release","ui.checkpoint",report);
            // A down/drag handler can itself rebind the object. Release without
            // a click/drop if the observed content changed while held.
            try
            {
                UiObservation.Check(ui,(string)args["target_ref"],world);
                UiObservation.Check(ui,(string)args["to_ref"],world);
            }
            catch (BridgeFailure error)
            { throw new BridgeFailure(error.Code,error.Message,"ui.before_release",report,inner:error); }
            data.delta = Vector2.zero; data.pointerCurrentRaycast = Hit(system,data);
            release(true); finished = true;
            report["worldChangedDuringDispatch"] = currentWorld() != world;
        }
        finally
        {
            try { if (!finished) release(false); }
            finally
            {
                report["completedDispatch"] = finished;
                foreach (var go in hovered) if (go != null) ExecuteEvents.Execute(go,data,ExecuteEvents.pointerExitHandler);
                data.pointerEnter = null;
            }
        }
    }
}
