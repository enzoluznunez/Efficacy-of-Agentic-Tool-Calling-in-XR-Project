using System;
using System.Threading.Tasks;
using UnityEngine;

public enum InitPolicy { OnFirstConnect, AtLaunch }

public enum ConnectPolicy { OnFirstUse, OnIntent }

public class GeminiClient : MonoBehaviour {

    [Tooltip("Enable Google Search grounding. Requires a paid-tier (billing-enabled) Gemini API key; leave off on the free tier or the session will be rejected.")]
    public bool enableWebSearch = false;

    [Tooltip("When the Gemini SDK, mic permission and audio are initialised. OnFirstConnect defers all of it until the assistant is first needed.")]
    public InitPolicy initPolicy = InitPolicy.AtLaunch;

    [Tooltip("When a session handshake is opened. OnIntent connects when the tool panel opens; OnFirstUse waits until the assistant is switched on.")]
    public ConnectPolicy connectPolicy = ConnectPolicy.OnIntent;

    private const float IntentLingerSeconds = 8f;
    private const float ResumeGraceSeconds = 2f;

    private bool _ready;
    private bool _active;
    private bool _intentHeld;
    private int _connectQueuedFrame = -1;
    private float _intentEndAt = -1f;
    private GeminiStatus _lastStatus = GeminiStatus.Off;

    public bool Ready => _ready;
    public bool Active => _active;
    public string ArmLabel => $"{initPolicy}/{connectPolicy}";
    public event Action<bool> ActiveChanged;
    public event Action<GeminiStatus> StatusChanged;
    public event Action ContextWarning;
    public event Action ContextExhausted;

    async void Start() {
        if (initPolicy == InitPolicy.AtLaunch) {
            await Gemini.EnsureInit(enableWebSearch);
            if (this == null) return;
        }

        _ready = true;
        Gemini.SetKeepAlive(_active);
        if (_active && isActiveAndEnabled) {
            await ConnectNow("active");
            return;
        }
    }

    private async Task ConnectNow(string reason) {
        await Gemini.EnsureInit(enableWebSearch);
        if (this == null || !isActiveAndEnabled) return;

        HitchLog.Mark($"Gemini.Connect.{reason}");
        Gemini.Connect();
        if (_active) Gemini.Listen();
    }

    private static bool CanWarm =>
        Gemini.Status == GeminiStatus.Off || Gemini.Status == GeminiStatus.Failed;

    private void QueueConnect() => _connectQueuedFrame = Time.frameCount;

    private void OnApplicationFocus(bool focused) {
        if (focused) DeferLinger();
    }

    private void DeferLinger() {
        if (_intentEndAt >= 0f)
            _intentEndAt = Mathf.Max(_intentEndAt, Time.unscaledTime + ResumeGraceSeconds);
    }

    public void SetActive(bool active) {
        bool changed = _active != active;
        _active = active;
        _intentEndAt = -1f;

        if (_ready) {
            Gemini.SetKeepAlive(active);
            if (active) {
                _ = ConnectNow("activate");
            }
            else {
                Gemini.Mute();
                Gemini.Disconnect();
            }
        }

        if (changed) ActiveChanged?.Invoke(active);
    }

    public void LogStudyMarker(string label) {
        StudyLog.Event("marker", new System.Collections.Generic.Dictionary<string, object> { { "label", label } });
    }

    public void LogProbeMarker(System.Collections.Generic.Dictionary<string, object> probe) {
        if (probe == null) return;
        var m = new System.Collections.Generic.Dictionary<string, object>(probe);
        if (!m.ContainsKey("prompt_tokens_at_probe")) m["prompt_tokens_at_probe"] = Gemini.LastPromptTokens;
        if (!m.ContainsKey("context_tokens_at_probe")) m["context_tokens_at_probe"] = Gemini.ContextTokens;
        StudyLog.Event("marker", m);
    }

    public void BeginRun(int compTrigger, int compTarget) {
        if (!_ready) return;
        Gemini.BeginRun(compTrigger, compTarget);
        bool changed = !_active;
        _active = true;
        Gemini.SetKeepAlive(true);
        Gemini.Connect();
        Gemini.Listen();
        if (changed) ActiveChanged?.Invoke(true);
    }

    public void LogCondition() {
        var toolPanel = Scene.ToolPanel;
        StudyLog.Event("study_condition", new System.Collections.Generic.Dictionary<string, object> {
            { "memoryLayer", MemoryConfig.MemoryLayerEnabled },
            { "assistantMotion", toolPanel != null ? toolPanel.AssistantSpeedName : "unknown" },
            { "model", Gemini.ModelId },
            { "compTrigger", Gemini.CompactTrigger },
            { "compTarget", Gemini.CompactTarget },
            { "contextCeiling", Gemini.ExhaustTokens },
            { "contextWindow", Gemini.ContextWindowTokens }
        });
    }

    public void NotifyIntent() {
        _intentHeld = true;
        _intentEndAt = -1f;
        if (!_ready || _active) return;
        if (connectPolicy == ConnectPolicy.OnFirstUse) return;
        if (!CanWarm) return;

        StudyLog.Event("warm_trigger", new System.Collections.Generic.Dictionary<string, object> {
            { "arm", ArmLabel },
            { "source", "panel_open" },
            { "initialised", Gemini.Initialised }
        });
        QueueConnect();
    }

    public void NotifyIntentEnded() {
        _intentHeld = false;
        if (!_ready || _active) return;
        _connectQueuedFrame = -1;
        _intentEndAt = Time.unscaledTime + IntentLingerSeconds;
    }

    void Update() {
        if (!_ready) return;

        if (_connectQueuedFrame >= 0 && Time.frameCount > _connectQueuedFrame) {
            _connectQueuedFrame = -1;
            if (CanWarm) _ = ConnectNow("queued");
        }

        if (_intentEndAt >= 0f && Time.unscaledTime >= _intentEndAt) {
            _intentEndAt = -1f;
            if (!_active) {
                bool warm = Gemini.Status != GeminiStatus.Off;
                Debug.Log(warm
                    ? $"[Gemini][session] intent socket idle for {IntentLingerSeconds}s; dropping it"
                    : "[Gemini][session] intent window expired; assistant was already off");
                Gemini.Disconnect();
            }
        }

        var status = Gemini.Status;
        if (status != _lastStatus) {
            _lastStatus = status;
            HitchLog.Mark($"GeminiStatus {status}");
            StudyLog.Event("status", new System.Collections.Generic.Dictionary<string, object> {
                { "status", status.ToString() }
            });

            StatusChanged?.Invoke(status);
        }

        if (Gemini.ConsumeClosingNotice()) ContextWarning?.Invoke();
        if (Gemini.ConsumeClosedNotice()) ContextExhausted?.Invoke();

    }

    void OnDisable() {
        if (_ready) Gemini.Disconnect();
    }

    void OnDestroy() {
        Gemini.Destroy();
    }

    void OnApplicationPause(bool paused) {
        if (!_ready) return;

        if (paused) {
            _connectQueuedFrame = -1;
            Gemini.Disconnect();
            return;
        }

        DeferLinger();

        if (_active) {
            _ = ConnectNow("resume");
        }
        else if (_intentHeld && CanWarm && connectPolicy != ConnectPolicy.OnFirstUse) {
            QueueConnect();
        }
    }
}
