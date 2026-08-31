using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class HitchLog : MonoBehaviour
{
    private const int StallReportMs = 2000;
    private const int WatchTickMs = 250;
    private const float WindowSeconds = 5f;
    private const float SuspendMs = 2000f;
    private const int MarkSlots = 16;

    public static bool VerboseConsole = true;

    private struct MarkEntry
    {
        public string label;
        public float at;
    }

    private static readonly MarkEntry[] _marks = new MarkEntry[MarkSlots];
    private static int _markHead;
    private static readonly object _markGate = new object();

    private static volatile int _heartbeat;
    private static volatile bool _watching;
    private static volatile bool _paused;
    private static volatile string _lastMark = "none";

    public static void Mark(string label)
    {
        float now = Time.realtimeSinceStartup;
        lock (_markGate)
        {
            _marks[_markHead] = new MarkEntry { label = label, at = now };
            _markHead = (_markHead + 1) % MarkSlots;
        }
        _lastMark = label;
    }

    private static List<string> MarksSince(float since)
    {
        var list = new List<string>(MarkSlots);
        lock (_markGate)
        {
            for (int i = 0; i < MarkSlots; i++)
            {
                MarkEntry e = _marks[(_markHead + i) % MarkSlots];
                if (e.label != null && e.at >= since) list.Add(e.label);
            }
        }
        return list;
    }

    private static string RecentMarkTrail()
    {
        var parts = new List<string>(MarkSlots);
        lock (_markGate)
        {
            for (int i = 0; i < MarkSlots; i++)
            {
                MarkEntry e = _marks[(_markHead + i) % MarkSlots];
                if (e.label != null) parts.Add(e.label);
            }
        }
        return parts.Count == 0 ? "none" : string.Join(" > ", parts);
    }

    private float _lastRealtime;
    private int _lastGc;
    private long _lastAllocated;
    private float _nextWindowAt;
    private bool _resumed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        GameObject host = new GameObject("HitchLog");
        DontDestroyOnLoad(host);
        host.AddComponent<HitchLog>();
    }

    private void OnApplicationPause(bool paused)
    {
        _paused = paused;
        if (!paused)
        {
            _resumed = true;
            _heartbeat++;
        }
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) _paused = true;
        else { _paused = false; _resumed = true; }
    }

    private void Start()
    {
        FrameBudget.SetDisplayHz(OVRPlugin.systemDisplayFrequency);
        FrameBudget.BeginPhase("startup");

        _watching = true;
        new Thread(WatchLoop) { IsBackground = true, Name = "HitchWatch" }.Start();

        _lastRealtime = Time.realtimeSinceStartup;
        _nextWindowAt = _lastRealtime + WindowSeconds;
        _lastGc = System.GC.CollectionCount(0);
        _lastAllocated = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();

        Debug.Log($"[Hitch] armed at {FrameBudget.DisplayHz:F0} Hz, budget {FrameBudget.BudgetMs:F1} ms");
    }

    private static volatile bool _userPresent = true;

    private static bool Presented =>
        !_paused && _userPresent;

    private static void WatchLoop()
    {
        int last = -1;
        int stalledMs = 0;

        while (_watching)
        {
            Thread.Sleep(WatchTickMs);

            if (!Presented) { last = _heartbeat; stalledMs = 0; continue; }

            int beat = _heartbeat;
            if (beat != last)
            {
                last = beat;
                stalledMs = 0;
                continue;
            }

            stalledMs += WatchTickMs;
            if (stalledMs % StallReportMs != 0) continue;

            string trail = RecentMarkTrail();
            StudyLog.Event("frame_stall", new Dictionary<string, object> {
                { "stalledMs", stalledMs },
                { "marks", trail }
            });
            Debug.Log($"[Hitch] STALL {stalledMs / 1000}s, marks: {trail}");
        }
    }

    private void OnDestroy() => _watching = false;

    private void OnApplicationQuit() => _watching = false;

    private void Update()
    {
        _heartbeat++;
        _userPresent = OVRPlugin.userPresent;

        float now = Time.realtimeSinceStartup;
        float ms = (now - _lastRealtime) * 1000f;
        _lastRealtime = now;

        StudyLog.Frame = Time.frameCount;
        StudyLog.RealtimeMs = now * 1000f;

        int gc = System.GC.CollectionCount(0);
        long allocated = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
        int gcDelta = gc - _lastGc;
        long allocDelta = allocated - _lastAllocated;
        _lastGc = gc;
        _lastAllocated = allocated;

        if (_resumed || _paused || ms >= SuspendMs)
        {
            bool suspend = ms >= SuspendMs;
            _resumed = false;
            _nextWindowAt = now + WindowSeconds;

            if (suspend)
            {
                StudyLog.Event("frame_gap", new Dictionary<string, object> {
                    { "ms", System.Math.Round(ms, 1) },
                    { "focused", !_paused },
                    { "phase", FrameBudget.Phase }
                });
                Debug.Log($"[Hitch] ignored a {ms / 1000f:F1}s gap (app suspended, not a hitch)");
            }
            return;
        }

        FrameBudget.Record(ms);

        float budget = FrameBudget.BudgetMs;
        if (ms > budget) ReportHitch(ms, budget, gcDelta, allocDelta);

        if (now >= _nextWindowAt)
        {
            _nextWindowAt = now + WindowSeconds;
            EmitWindow();
        }
    }

    private void ReportHitch(float ms, float budget, int gcDelta, long allocDelta)
    {
        int lost = FrameBudget.FramesLost(ms, budget);
        if (lost < 1) return;

        List<string> marks = MarksSince(Time.realtimeSinceStartup - (ms / 1000f) - 0.05f);

        StudyLog.Event("frame_hitch", new Dictionary<string, object> {
            { "ms", System.Math.Round(ms, 2) },
            { "budgetMs", System.Math.Round(budget, 2) },
            { "framesLost", lost },
            { "gc", gcDelta },
            { "allocMb", System.Math.Round(allocDelta / 1048576f, 3) },
            { "marks", marks },
            { "phase", FrameBudget.Phase }
        });

        if (VerboseConsole && lost >= 2)
        {
            string blame = marks.Count > 0 ? "during " + string.Join(" > ", marks) : "after " + _lastMark;
            Debug.Log($"[Hitch] frame {Time.frameCount} took {ms:F0} ms " +
                      $"(budget {budget:F1}, lost {lost}, gc {gcDelta}, " +
                      $"alloc {allocDelta / 1048576f:F1} MB) {blame}");
        }
    }

    private static void EmitWindow()
    {
        if (!StudyLog.Active) return;

        var fields = new Dictionary<string, object>();
        if (!FrameBudget.TryTakeWindow(fields)) return;

        float appFps = OVRPlugin.GetAppFramerate();
        if (appFps > 0f) fields["appFps"] = System.Math.Round(appFps, 2);

        StudyLog.Event("frame_window", fields);
    }
}
