using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using Debug = UnityEngine.Debug;

public static class StudyLog {

    private static readonly object gate = new object();
    private static StreamWriter writer;
    private static BlockingCollection<string> pending;
    private static Thread drain;
    private static Stopwatch clock;
    private static volatile bool active;
    private static long owner;
    private static long nextOwner;

    public static bool Active => active;

    public static int Frame;
    public static float RealtimeMs;

    public static long Begin(string dir, string participant, string arm) {
        long token;
        lock (gate) {
            EndNoLock();
            token = owner = ++nextOwner;
            try {
                Directory.CreateDirectory(dir);
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                string safeParticipant = string.IsNullOrEmpty(participant) ? "P" : participant.Replace(' ', '_');
                string file = Path.Combine(dir, $"{stamp}_{safeParticipant}_{arm}.jsonl");
                writer = new StreamWriter(file, append: false) { AutoFlush = false };
                pending = new BlockingCollection<string>(new ConcurrentQueue<string>());
                StreamWriter w = writer;
                BlockingCollection<string> q = pending;
                drain = new Thread(() => Drain(w, q)) { IsBackground = true, Name = "StudyLog" };
                drain.Start();
                clock = Stopwatch.StartNew();
                active = true;
            }
            catch (Exception e) {
                Debug.LogWarning($"[StudyLog] begin failed: {e.Message}");
                active = false;
            }
        }
        Event("session_begin", new Dictionary<string, object> {
            { "participant", participant },
            { "arm", arm }
        });
        return token;
    }

    public static void Event(string type, Dictionary<string, object> fields = null) {
        lock (gate) {
            if (!active || writer == null) return;
            try {
                var record = new Dictionary<string, object> {
                    { "t_ms", clock.ElapsedMilliseconds },
                    { "rt_ms", Math.Round(RealtimeMs, 1) },
                    { "frame", Frame },
                    { "utc", DateTime.UtcNow.ToString("o") },
                    { "type", type }
                };
                if (fields != null)
                    foreach (var kv in fields) record[kv.Key] = kv.Value;
                pending.Add(JsonSerializer.Serialize(record));
            }
            catch (Exception e) {
                Debug.LogWarning($"[StudyLog] write failed: {e.Message}");
            }
        }
    }

    private const int FlushIdleMs = 1000;

    private static void Drain(StreamWriter w, BlockingCollection<string> q) {
        try {
            var sinceFlush = Stopwatch.StartNew();
            while (!q.IsCompleted) {
                if (q.TryTake(out string line, FlushIdleMs)) w.WriteLine(line);
                if (sinceFlush.ElapsedMilliseconds >= FlushIdleMs) {
                    w.Flush();
                    sinceFlush.Restart();
                }
            }
            while (q.TryTake(out string line)) w.WriteLine(line);
            w.Flush();
        }
        catch (Exception e) {
            Debug.LogWarning($"[StudyLog] writer thread failed: {e.Message}");
        }
    }

    public static void End(long token) {
        lock (gate) {
            if (token == 0 || token != owner) return;
            EndNoLock();
        }
    }

    private static void EndNoLock() {
        active = false;
        owner = 0;

        if (pending != null) {
            try { pending.CompleteAdding(); } catch { }
        }
        if (drain != null) {
            try { drain.Join(3000); } catch { }
            drain = null;
        }
        if (writer != null) {
            try { writer.Flush(); writer.Dispose(); } catch { }
            writer = null;
        }
        if (pending != null) {
            try { pending.Dispose(); } catch { }
            pending = null;
        }
    }
}
