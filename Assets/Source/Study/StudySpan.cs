using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

public sealed class StudySpan : IDisposable
{
    public static bool VerboseConsole = true;

    private readonly string _name;
    private readonly Stopwatch _watch;
    private readonly long _alloc0;
    private readonly int _frame0;
    private Dictionary<string, object> _detail;
    private bool _closed;

    private StudySpan(string name)
    {
        _name = name;
        _frame0 = Time.frameCount;
        _alloc0 = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
        _watch = Stopwatch.StartNew();
    }

    public static StudySpan Begin(string name) => new StudySpan(name);

    public void Detail(string key, object value)
    {
        _detail ??= new Dictionary<string, object>(4);
        _detail[key] = value;
    }

    public void Dispose()
    {
        if (_closed) return;
        _closed = true;

        _watch.Stop();
        double ms = _watch.Elapsed.TotalMilliseconds;
        long allocDelta = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() - _alloc0;
        int frames = Time.frameCount - _frame0;

        HitchLog.Mark(_name);

        if (StudyLog.Active)
        {
            var fields = new Dictionary<string, object> {
                { "name", _name },
                { "ms", Math.Round(ms, 2) },
                { "frames", frames },
                { "allocMb", Math.Round(allocDelta / 1048576d, 3) },
                { "phase", FrameBudget.Phase }
            };
            if (_detail != null)
                foreach (var kv in _detail) fields[kv.Key] = kv.Value;

            StudyLog.Event("ui_span", fields);
        }

        if (VerboseConsole && ms >= FrameBudget.BudgetMs)
            Debug.Log($"[Span] {_name} {ms:F1} ms{DetailText()} " +
                      $"(budget {FrameBudget.BudgetMs:F1} ms, alloc {allocDelta / 1048576f:F2} MB)");
    }

    private string DetailText()
    {
        if (_detail == null || _detail.Count == 0) return "";

        var parts = new List<string>(_detail.Count);
        foreach (var kv in _detail) parts.Add($"{kv.Key}={kv.Value}");
        return " [" + string.Join(", ", parts) + "]";
    }
}
