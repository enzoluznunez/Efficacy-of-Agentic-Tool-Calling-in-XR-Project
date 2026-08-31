using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Google.GenAI;
using Google.GenAI.Types;
using UnityEngine;
using GType = Google.GenAI.Types.Type;

public static class EpisodicMemory {

    public struct Entry {
        public long id;
        public string kind;
        public string text;
    }

    private const int MaxEntries = 10000;
    private static readonly object gate = new object();
    private static readonly List<Entry> entries = new List<Entry>();
    private static long nextId;

    public static void Record(string kind, string text) {
        if (string.IsNullOrEmpty(text)) return;
        lock (gate) {
            entries.Add(new Entry { id = nextId++, kind = kind, text = text });
            while (entries.Count > MaxEntries) {
                Debug.LogWarning("[EpisodicMemory] over hard cap; dropping oldest entry");
                entries.RemoveAt(0);
            }
        }
    }

    public static List<Entry> After(long afterId) {
        var result = new List<Entry>();
        lock (gate) {
            for (int i = entries.Count - 1; i >= 0 && entries[i].id > afterId; i--)
                result.Add(entries[i]);
        }
        result.Reverse();
        return result;
    }

    public static void Reset() {
        lock (gate) {
            entries.Clear();
            nextId = 0;
            summary = "";
            epoch++;
        }
    }

    private const string SummaryModelId = "gemini-3.5-flash-lite";
    private const int MaxSummaryChars = 1600;
    private const int SummaryMaxOutputTokens = 700;

    private static Client client;
    private static string summary = "";
    private static int epoch;

    private static readonly JsonSerializerOptions jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

    public static void Init(Client c) { client = c; }

    public static string Summary {
        get { lock (gate) return summary; }
    }

    public static async Task<string> Summarize(List<Entry> lines) {
        if (client == null || lines == null || lines.Count == 0) return null;

        string prior;
        int startEpoch;
        lock (gate) { prior = summary; startEpoch = epoch; }

        string json = await Call(BuildPrompt(prior, lines)).ConfigureAwait(false);
        if (string.IsNullOrEmpty(json)) return null;

        MemoryFields fields = Parse(json);
        string rendered = ClampText(RenderText(json, fields), MaxSummaryChars);
        if (string.IsNullOrEmpty(rendered)) return null;

        lock (gate) {
            if (epoch != startEpoch) {
                Debug.Log("[EpisodicMemory] discarded a summary that finished after Reset");
                return null;
            }
            summary = rendered;
        }
        if (fields?.facts != null) SemanticMemory.SetFacts(fields.facts);
        return json;
    }

    private static readonly Schema MemorySchema = new Schema {
        Type = GType.Object,
        Properties = new Dictionary<string, Schema> {
            { "currentGoal", new Schema { Type = GType.String, Description = "The user's current objective in one sentence.", MaxLength = 240 } },
            { "entities", StrArray("Specific rows, columns, dataset names, or labels referenced.", 12) },
            { "edits", StrArray("Edits made via tools: slices, colors, moves, rotations, sorts. Say whether Ada or the user made each one.", 12) },
            { "unresolvedRequest", new Schema { Type = GType.String, Description = "Any pending request not yet fulfilled, or empty.", MaxLength = 240 } },
            { "facts", StrArray("Durable user facts as complete self-contained sentences, e.g. 'Favorite color is turquoise'. Re-state every fact that still holds, with its current value; drop any that no longer hold.", 12) }
        },
        PropertyOrdering = new List<string> { "currentGoal", "entities", "edits", "unresolvedRequest", "facts" },
        Required = new List<string> { "currentGoal" }
    };

    private static Schema StrArray(string desc, int maxItems) {
        return new Schema {
            Type = GType.Array,
            Description = desc,
            MaxItems = maxItems,
            Items = new Schema { Type = GType.String, MaxLength = 160 }
        };
    }

    private static async Task<string> Call(string prompt) {
        if (client == null) return null;
        try {
            var config = new GenerateContentConfig {
                ResponseMimeType = "application/json",
                ResponseSchema = MemorySchema,
                Temperature = 0,
                MaxOutputTokens = SummaryMaxOutputTokens
            };
            var resp = await client.Models
                .GenerateContentAsync(SummaryModelId, prompt, config, CancellationToken.None)
                .ConfigureAwait(false);
            return resp?.Text?.Trim();
        }
        catch (Exception e) {
            Debug.LogWarning($"[EpisodicMemory] summarizer call failed: {e.Message}");
            return null;
        }
    }

    private static string Label(string kind) {
        switch (kind) {
            case "user": return "User";
            case "ada": return "Ada";
            case "action": return "Ada action";
            case "user_action": return "User action";
            default: return kind;
        }
    }

    private static string BuildPrompt(string priorSummary, List<Entry> lines) {
        var sb = new StringBuilder();
        sb.Append("You maintain a running structured memory of an ongoing voice conversation between a user and Ada, ");
        sb.Append("a voice assistant that operates a VR data-visualization app with a spreadsheet-like Sheet. ");
        sb.Append("Update the memory so a fresh assistant could read it and continue seamlessly. ");
        sb.Append("Lines marked 'Ada action' are things Ada did; lines marked 'User action' are changes the user made by hand, which Ada did not cause and must not assume it caused. ");
        sb.Append("Keep durable facts and drop greetings and small talk. ");
        sb.Append("Record durable user facts (preferences, goals, names) in facts as complete self-contained sentences; the list replaces the previous one, so re-state every fact that still holds and drop any that no longer do. ");
        sb.Append("Keep each list short and specific.\n\n");
        sb.Append("PRIOR MEMORY:\n");
        sb.Append(string.IsNullOrEmpty(priorSummary) ? "(none yet)" : priorSummary);
        sb.Append("\n\nNEW DIALOGUE:\n");
        for (int i = 0; i < lines.Count; i++)
            sb.Append(Label(lines[i].kind)).Append(": ").Append(lines[i].text).Append('\n');
        return sb.ToString();
    }

    private static MemoryFields Parse(string json) {
        try { return JsonSerializer.Deserialize<MemoryFields>(json, jsonOpts); }
        catch { return null; }
    }

    private static string RenderText(string json, MemoryFields m) {
        if (m == null) return json.Trim();

        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(m.currentGoal)) sb.Append("Goal: ").Append(m.currentGoal).Append('\n');
        AppendList(sb, "Entities", m.entities);
        AppendList(sb, "Edits", m.edits);
        if (!string.IsNullOrEmpty(m.unresolvedRequest)) sb.Append("Unresolved: ").Append(m.unresolvedRequest).Append('\n');

        string outp = sb.ToString().Trim();
        return outp.Length == 0 ? json.Trim() : outp;
    }

    private static void AppendList(StringBuilder sb, string label, string[] items) {
        if (items == null || items.Length == 0) return;
        var parts = new List<string>();
        for (int i = 0; i < items.Length; i++) {
            if (!string.IsNullOrEmpty(items[i])) parts.Add(items[i]);
        }
        if (parts.Count == 0) return;
        sb.Append(label).Append(": ").Append(string.Join("; ", parts)).Append('\n');
    }

    private static string ClampText(string s, int maxChars) {
        if (string.IsNullOrEmpty(s) || s.Length <= maxChars) return s;
        Debug.LogWarning($"[EpisodicMemory] summary clamped from {s.Length} to {maxChars} chars");
        return s.Substring(0, maxChars);
    }

    private class MemoryFields {
        public string currentGoal { get; set; }
        public string[] entities { get; set; }
        public string[] edits { get; set; }
        public string unresolvedRequest { get; set; }
        public string[] facts { get; set; }
    }
}
