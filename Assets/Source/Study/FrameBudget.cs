using System;
using System.Collections.Generic;

public struct FrameStats
{
    public int frames;
    public float p50;
    public float p95;
    public float p99;
    public float max;
    public float mean;
    public int over1x;
    public int over2x;
    public int over4x;

    public void AppendTo(Dictionary<string, object> into, string prefix)
    {
        into[prefix + "Frames"] = frames;
        into[prefix + "P50"] = Round(p50);
        into[prefix + "P95"] = Round(p95);
        into[prefix + "P99"] = Round(p99);
        into[prefix + "Max"] = Round(max);
        into[prefix + "Mean"] = Round(mean);
        into[prefix + "Over1x"] = over1x;
        into[prefix + "Over2x"] = over2x;
        into[prefix + "Over4x"] = over4x;
    }

    private static double Round(float v) => Math.Round(v, 2);
}

public static class FrameBudget
{
    public const float DefaultHz = 72f;
    public const int Capacity = 4096;

    private static float _displayHz = DefaultHz;

    public static float DisplayHz => _displayHz;
    public static float BudgetMs => 1000f / _displayHz;

    public static void SetDisplayHz(float hz)
    {
        if (hz > 1f && hz < 1000f) _displayHz = hz;
    }

    private static readonly List<float> _window = new List<float>(Capacity);
    private static readonly List<float> _phase = new List<float>(Capacity);
    private static readonly List<float> _scratch = new List<float>(Capacity);
    private static readonly Random _rng = new Random(12345);

    private static int _phaseFrames;
    private static double _phaseTotal;
    private static float _phaseMax;
    private static int _phaseOver1x;
    private static int _phaseOver2x;
    private static int _phaseOver4x;

    private static string _phaseName = "startup";
    public static string Phase => _phaseName;

    public static int WindowCount => _window.Count;

    public static void Record(float ms)
    {
        if (_window.Count < Capacity) _window.Add(ms);

        _phaseFrames++;
        _phaseTotal += ms;
        if (ms > _phaseMax) _phaseMax = ms;
        float budget = BudgetMs;
        if (budget > 0f)
        {
            if (ms > budget * 4f) _phaseOver4x++;
            else if (ms > budget * 2f) _phaseOver2x++;
            else if (ms > budget) _phaseOver1x++;
        }

        if (_phase.Count < Capacity) _phase.Add(ms);
        else
        {
            int slot = _rng.Next(_phaseFrames);
            if (slot < Capacity) _phase[slot] = ms;
        }
    }

    public static void BeginPhase(string name)
    {
        _phaseName = string.IsNullOrEmpty(name) ? "unnamed" : name;
        ClearPhase();
    }

    private static void ClearPhase()
    {
        _phase.Clear();
        _phaseFrames = 0;
        _phaseTotal = 0d;
        _phaseMax = 0f;
        _phaseOver1x = _phaseOver2x = _phaseOver4x = 0;
    }

    public static void AppendPhaseStats(Dictionary<string, object> into)
    {
        FrameStats stats = Analyse(_phase, BudgetMs);
        stats.frames = _phaseFrames;
        stats.mean = _phaseFrames > 0 ? (float)(_phaseTotal / _phaseFrames) : 0f;
        stats.max = _phaseMax;
        stats.over1x = _phaseOver1x;
        stats.over2x = _phaseOver2x;
        stats.over4x = _phaseOver4x;
        stats.AppendTo(into, "frame");
        into["phase"] = _phaseName;
        into["budgetMs"] = Math.Round(BudgetMs, 2);
    }

    public static bool TryTakeWindow(Dictionary<string, object> into)
    {
        if (_window.Count == 0) return false;

        Analyse(_window, BudgetMs).AppendTo(into, "frame");
        into["phase"] = _phaseName;
        into["budgetMs"] = Math.Round(BudgetMs, 2);
        _window.Clear();
        return true;
    }

    public static void Reset()
    {
        _window.Clear();
        ClearPhase();
        _phaseName = "startup";
        _displayHz = DefaultHz;
    }

    public static FrameStats Analyse(IReadOnlyList<float> samples, float budgetMs)
    {
        var stats = new FrameStats();
        if (samples == null || samples.Count == 0) return stats;

        stats.frames = samples.Count;

        double total = 0d;
        for (int i = 0; i < samples.Count; i++)
        {
            float ms = samples[i];
            total += ms;
            if (ms > stats.max) stats.max = ms;
            if (budgetMs > 0f)
            {
                if (ms > budgetMs * 4f) stats.over4x++;
                else if (ms > budgetMs * 2f) stats.over2x++;
                else if (ms > budgetMs) stats.over1x++;
            }
        }
        stats.mean = (float)(total / samples.Count);

        _scratch.Clear();
        for (int i = 0; i < samples.Count; i++) _scratch.Add(samples[i]);
        _scratch.Sort();

        stats.p50 = Percentile(_scratch, 0.50f);
        stats.p95 = Percentile(_scratch, 0.95f);
        stats.p99 = Percentile(_scratch, 0.99f);
        return stats;
    }

    public static float Percentile(IReadOnlyList<float> sorted, float fraction)
    {
        if (sorted == null || sorted.Count == 0) return 0f;
        if (sorted.Count == 1) return sorted[0];

        float clamped = fraction < 0f ? 0f : fraction > 1f ? 1f : fraction;
        int index = (int)Math.Round(clamped * (sorted.Count - 1), MidpointRounding.AwayFromZero);
        if (index < 0) index = 0;
        if (index >= sorted.Count) index = sorted.Count - 1;
        return sorted[index];
    }

    public static int FramesLost(float ms, float budgetMs)
    {
        if (budgetMs <= 0f || ms <= budgetMs) return 0;
        return (int)(ms / budgetMs) - 1;
    }
}
