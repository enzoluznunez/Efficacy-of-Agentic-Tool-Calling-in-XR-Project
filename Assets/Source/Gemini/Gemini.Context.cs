using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public static partial class Gemini {


    private const int RecentTailLines = 6;

    private static long triggerTokens = 32000;
    private static int targetTokens = 16000;

    public const long ContextWindowTokens = 131072;

    public const long SafetyNetTrigger = 48000;
    public const long SafetyNetTarget = 24000;
    public const long ExhaustTokens = ContextWindowTokens / 2;
    public const long ExhaustWarnTokens = ExhaustTokens - 6000;

    public static long CompactTrigger => triggerTokens;
    public static int CompactTarget => targetTokens;

    private static int usageConnection;
    private static long contextTokens;
    private static long sessionPromptTokens;
    private static long sessionUncachedTokens;
    private static long lastExactContext;
    private static long sessionCachedTokens;
    private static long sessionThoughtTokens;

    public static long ContextTokens => contextTokens;
    public static long SessionPromptTokens => sessionPromptTokens;
    public static long SessionUncachedTokens => sessionUncachedTokens;
    public static long SessionCachedTokens => sessionCachedTokens;

    public static void AddUsage(long uncached, long cached, long thoughts) {
        if (uncached > 0) sessionUncachedTokens += uncached;
        if (cached > 0) sessionCachedTokens += cached;
        if (thoughts > 0) sessionThoughtTokens += thoughts;
    }

    private static volatile bool exhausted;
    private static volatile bool exhaustWarned;

    public static bool Exhausted => exhausted;

    public static void SetBudget(long trigger, int target) {
        triggerTokens = trigger;
        targetTokens = target;
    }

    private static readonly object gate = new object();
    private static long foldedThroughId = -1;
    private static volatile bool compacting;
    private static volatile bool pending;
    private static volatile bool refreshWanted;

    public static bool ConsumeRefreshRequest() {
        if (!refreshWanted || refreshing) return false;
        refreshWanted = false;
        return true;
    }


    public static string ComposeSystemInstruction(string body, string tail) {
        if (!MemoryConfig.MemoryLayerEnabled) return body + tail;

        string summary = EpisodicMemory.Summary;
        var facts = SemanticMemory.FactsSnapshot();
        if (string.IsNullOrEmpty(summary) && facts.Count == 0) return body + tail;

        var memory = new System.Text.StringBuilder();
        memory.Append("# Memory\n");
        if (!string.IsNullOrEmpty(summary))
            memory.Append("Summary of the conversation so far: ").Append(summary).Append('\n');
        if (facts.Count > 0)
            memory.Append("Durable facts about the user (current values): ").Append(string.Join("; ", facts)).Append('\n');
        memory.Append('\n');

        return body + memory + tail;
    }

    public static void ResetWindow() {
        lock (gate) {
            foldedThroughId = -1;
            pending = false;
            refreshWanted = false;
        }
        exhausted = false;
        exhaustWarned = false;
        ResetContextEstimate();
        EpisodicMemory.Reset();
        SemanticMemory.ClearFacts();
        StateChannel.ClearPending();
        compacting = false;
    }

    private static long FoldCursor() {
        lock (gate) return foldedThroughId;
    }

    private static List<EpisodicMemory.Entry> Foldable(long afterId, int reserveTail) {
        var all = EpisodicMemory.After(afterId);
        int take = all.Count - reserveTail;
        if (take <= 0) return new List<EpisodicMemory.Entry>();
        return all.GetRange(0, take);
    }

    public static void ResetContextEstimate() {
        usageConnection = 0;
        contextTokens = 0;
        sessionPromptTokens = 0;
        sessionUncachedTokens = 0;
        lastExactContext = 0;
        sessionCachedTokens = 0;
        sessionThoughtTokens = 0;
        Interlocked.Exchange(ref toolRoundsThisTurn, 0);
    }

    private static bool TrackContext(int conn, long promptTokens, int rounds, out long context, out bool exact) {
        if (conn != usageConnection) {
            usageConnection = conn;
            if (!resumedConnection) contextTokens = 0;
        }

        if (promptTokens > 0) sessionPromptTokens += promptTokens;

        exact = rounds <= 0;
        context = contextTokens;
        if (promptTokens <= 0) return false;

        long reading = promptTokens / (rounds + 1);
        if (exact || reading > contextTokens) contextTokens = reading;

        context = contextTokens;
        return true;
    }

    public static void ObserveTokens(long context, bool exact) {
        if (exact) ObserveCeiling(context);
        ObserveRefresh(context);
        ObserveCompaction(context);
    }

    private static void ObserveCeiling(long context) {
        if (context >= ExhaustTokens) { MarkExhausted(context); return; }
        if (context < ExhaustWarnTokens || exhaustWarned) return;

        exhaustWarned = true;
        Debug.LogWarning($"[Gemini][window] approaching context ceiling: {context} of {ExhaustTokens}");
        StudyLog.Event("context_warning", new Dictionary<string, object> {
            { "contextTokens", context },
            { "ceiling", ExhaustTokens }
        });
        Interlocked.Exchange(ref pendingClosingNotice, 1);
    }

    private static void ObserveRefresh(long context) {
        if (context >= triggerTokens) refreshWanted = true;
    }

    private static void ObserveCompaction(long context) {
        if (!MemoryConfig.MemoryLayerEnabled || compacting || pending) return;
        if (context < triggerTokens) return;
        if (Foldable(FoldCursor(), RecentTailLines).Count == 0) return;
        pending = true;
    }

    public static void ClearExhaustion() {
        exhausted = false;
        exhaustWarned = false;
    }

    private static void MarkExhausted(long context) {
        if (exhausted) return;
        exhausted = true;
        Debug.LogWarning($"[Gemini][window] context ceiling reached: {context} >= {ExhaustTokens}");
        StudyLog.Event("context_exhausted", new Dictionary<string, object> {
            { "contextTokens", context },
            { "ceiling", ExhaustTokens },
            { "memoryLayer", MemoryConfig.MemoryLayerEnabled }
        });
        resumeHandle = null;
        RetireConnection();
        var s = liveSession;
        if (s != null) { try { _ = s.CloseAsync(); } catch { } }
    }

    public static void OnTurnComplete() {
        if (!MemoryConfig.MemoryLayerEnabled || compacting || !pending) return;
        pending = false;
        compacting = true;
        _ = CompactAsync();
    }

    private static async Task CompactAsync() {
        try {
            long cursor;
            lock (gate) cursor = foldedThroughId;

            var toFold = Foldable(cursor, RecentTailLines);
            if (toFold.Count == 0) return;
            int foldCount = toFold.Count;
            long newCursor = toFold[foldCount - 1].id;

            string json = await EpisodicMemory.Summarize(toFold).ConfigureAwait(false);
            if (string.IsNullOrEmpty(json)) return;
            string rendered = EpisodicMemory.Summary;

            lock (gate) {
                if (newCursor > foldedThroughId) foldedThroughId = newCursor;
            }

            Debug.Log($"[Gemini][window] summary updated: folded {foldCount} lines, {rendered.Length} chars, at {contextTokens} context tokens");
            StudyLog.Event("summary_update", new Dictionary<string, object> {
                { "foldedLines", foldCount },
                { "summaryChars", rendered.Length },
                { "contextTokens", contextTokens },
                { "promptTokens", lastLoggedPromptTokens },
                { "summary", rendered },
                { "summaryJson", json }
            });
        }
        catch (Exception e) {
            Debug.LogWarning($"[Gemini][window] compaction failed: {e.Message}");
        }
        finally {
            compacting = false;
        }
    }

    public static async Task FinalizeForRefresh() {
        if (!MemoryConfig.MemoryLayerEnabled) return;
        while (compacting) await Task.Delay(20).ConfigureAwait(false);
        long cursor;
        lock (gate) cursor = foldedThroughId;

        var toFold = Foldable(cursor, 0);
        if (toFold.Count == 0) return;
        long newCursor = toFold[toFold.Count - 1].id;

        compacting = true;
        try {
            string json = await EpisodicMemory.Summarize(toFold).ConfigureAwait(false);
            lock (gate) {
                if (!string.IsNullOrEmpty(json) && newCursor > foldedThroughId) foldedThroughId = newCursor;
                pending = false;
            }
        }
        catch (Exception e) {
            Debug.LogWarning($"[Gemini][window] finalize failed: {e.Message}");
        }
        finally {
            compacting = false;
        }
    }
}
