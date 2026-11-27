using System;
using System.Collections.Generic;

namespace ModDevBridge;

// Phase actions are supplied by the game adapter; compensation is independently testable.
public sealed class HotReloadPlan
{
    public Action OldPrepare, OldDisable, OldUnpatch, OldPatch, OldInit, OldEnable, OldActivate;
    public Action NextPatch, NextInit, NextEnable, NextPrepare, NextDisable, NextUnpatch, NextActivate, Commit;
}
public sealed class HotReloadResult
{
    public bool success, compensationAttempted, compensationCompleted;
    public bool oldLifecycleTouched, candidateLifecycleTouched;
    public readonly string stateVerification = "not_checked";
    public readonly bool assembliesRemainLoaded = true;
    public string failedStage, error;
    public readonly List<string> stages = new List<string>();
    public readonly List<string> recoveryErrors = new List<string>();
}
public static class HotReloadTransaction
{
    public static HotReloadResult Run(HotReloadPlan p)
    {
        var r = new HotReloadResult();
        bool disableStarted = false, disableCompleted = false, unpatchStarted = false, unpatchCompleted = false;
        bool nextPatchStarted = false, nextInitStarted = false;
        string stage = "preflight";
        Action<string, Action> run = (name, action) => { stage = name; r.stages.Add(name + ":started"); action(); r.stages.Add(name + ":complete"); };
        try
        {
            r.oldLifecycleTouched = true;
            run("old.prepare", p.OldPrepare);
            disableStarted = true; run("old.disable", p.OldDisable); disableCompleted = true;
            unpatchStarted = true; run("old.unpatch", p.OldUnpatch); unpatchCompleted = true;
            nextPatchStarted = true; run("next.patch", p.NextPatch);
            nextInitStarted = true; r.candidateLifecycleTouched = true;
            run("next.init", p.NextInit); run("next.enable", p.NextEnable); run("next.activate", p.NextActivate);
            run("commit", p.Commit); r.success = true;
        }
        catch (Exception e)
        {
            r.failedStage = stage; r.error = e.ToString();
            r.compensationAttempted = r.oldLifecycleTouched;
            Func<string, Action, bool> recover = (name, action) => {
                try { run(name, action); return true; }
                catch (Exception failure) { r.recoveryErrors.Add(name + ": " + failure); return false; }
            };
            // Even partial Init/OnEnable can acquire resources. Every cleanup is attempted.
            if (nextInitStarted)
            {
                recover("next.cleanup.prepare", p.NextPrepare);
                recover("next.cleanup.disable", p.NextDisable);
            }
            if (nextPatchStarted) recover("next.cleanup.unpatch", p.NextUnpatch);
            if (r.oldLifecycleTouched)
            {
                // Retry partially failed cleanup, not an already completed disable.
                // Prepare failure must not double-enable a still-enabled instance.
                if (disableStarted && !disableCompleted) recover("old.recover.disable", p.OldDisable);
                bool canPatch = unpatchCompleted;
                if (unpatchStarted && !unpatchCompleted) canPatch = recover("old.recover.unpatch", p.OldUnpatch);
                if (unpatchStarted && canPatch) recover("old.recover.patch", p.OldPatch);
                if (disableStarted)
                {
                    // Some MODs undo Init-owned bindings in OnDisable.
                    recover("old.recover.init", p.OldInit);
                    recover("old.recover.enable", p.OldEnable);
                }
                recover("old.recover.activate", p.OldActivate);
            }
            r.compensationCompleted = r.oldLifecycleTouched && r.recoveryErrors.Count == 0;
        }
        return r;
    }
}
