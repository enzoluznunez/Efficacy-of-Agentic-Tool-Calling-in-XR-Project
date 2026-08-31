using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UnityEngine;

public class MainThread : MonoBehaviour {

    private static MainThread instance;
    private static readonly ConcurrentQueue<Action> queue = new ConcurrentQueue<Action>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() {
        if (instance != null) return;
        var go = new GameObject("MainThread");
        instance = go.AddComponent<MainThread>();
        DontDestroyOnLoad(go);
    }

    public static Task Run(Action action) {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        queue.Enqueue(() => {
            try {
                action();
                tcs.SetResult(true);
            }
            catch (Exception e) {
                tcs.SetException(e);
            }
        });
        return tcs.Task;
    }

    private void Update() {
        int drained = 0;
        while (queue.TryDequeue(out var action)) {
            action();
            drained++;
        }
        if (drained > 0) HitchLog.Mark($"MainThread x{drained}");
    }
}
