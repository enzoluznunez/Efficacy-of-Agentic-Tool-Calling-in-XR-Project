using System;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using UnityEngine;

public class Scanner : MonoBehaviour
{
    public ManageDatasets manageDatasets;

    [Tooltip("Seconds between sweeps for QR payloads that arrive after their trackable is created.")]
    public float sweepSeconds = 0.5f;

    [Tooltip("Hosts allowed for http(s) QR payloads. Leave empty to refuse all web links.")]
    public string[] allowedHosts = { };

    private readonly List<MRUKTrackable> _trackables = new List<MRUKTrackable>();
    private readonly HashSet<string> _handled = new HashSet<string>();
    private readonly HashSet<int> _reportedEmpty = new HashSet<int>();

    private bool _subscribed;
    private float _nextSweep;

    private void Start()
    {
        if (manageDatasets == null) manageDatasets = FindAnyObjectByType<ManageDatasets>();
        if (manageDatasets != null) manageDatasets.OnDatasetLoadFailed += OnDatasetLoadFailed;
        TrySubscribe();
    }

    private void OnDestroy()
    {
        if (manageDatasets != null) manageDatasets.OnDatasetLoadFailed -= OnDatasetLoadFailed;
        if (!_subscribed || MRUK.Instance == null) return;
        MRUK.Instance.SceneSettings.TrackableAdded.RemoveListener(OnTrackableAdded);
        _subscribed = false;
    }

    private void OnDatasetLoadFailed(string payload)
    {
        if (!string.IsNullOrEmpty(payload)) _handled.Remove(payload);
    }

    private void Update()
    {
        if (!TrySubscribe()) return;

        if (Time.unscaledTime < _nextSweep) return;
        _nextSweep = Time.unscaledTime + Mathf.Max(sweepSeconds, 0.1f);

        MRUK.Instance.GetTrackables(_trackables);
        for (int i = 0; i < _trackables.Count; i++) Consume(_trackables[i], false);
    }

    private bool TrySubscribe()
    {
        if (_subscribed) return true;
        if (MRUK.Instance == null) return false;

        MRUK.Instance.SceneSettings.TrackableAdded.AddListener(OnTrackableAdded);
        _subscribed = true;
        Debug.Log("[Scanner] Subscribed to MRUK trackables; QR scanning is live.");
        return true;
    }

    private void OnTrackableAdded(MRUKTrackable trackable) => Consume(trackable, true);

    private void Consume(MRUKTrackable trackable, bool fromEvent)
    {
        if (trackable == null) return;
        if (trackable.TrackableType != OVRAnchor.TrackableType.QRCode) return;

        string payload = trackable.MarkerPayloadString;
        if (string.IsNullOrEmpty(payload))
        {
            if (fromEvent && _reportedEmpty.Add(trackable.GetInstanceID()))
                Debug.Log("[Scanner] QR trackable appeared before its payload decoded; waiting for a sweep to pick it up.");
            return;
        }

        if (!_handled.Add(payload)) return;

        if (!Accepted(payload))
        {
            Debug.Log($"[Scanner] Ignored a QR payload that does not look like a dataset ({payload.Length} chars).");
            return;
        }

        if (manageDatasets == null)
        {
            Debug.LogError("[Scanner] Read a QR payload but no ManageDatasets is in the scene; it was dropped.");
            return;
        }

        Debug.Log($"[Scanner] QR payload read ({payload.Length} chars, {(fromEvent ? "on detection" : "on sweep")}); opening dataset.");
        manageDatasets.AddDataset(payload);
    }

    private bool Accepted(string payload)
    {
        string p = payload.Trim();

        if (p.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            p.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(p, UriKind.Absolute, out Uri uri)) return false;
            for (int i = 0; i < allowedHosts.Length; i++)
                if (string.Equals(uri.Host, allowedHosts[i], StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        if (p.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) return true;

        int firstBreak = p.IndexOf('\n');
        return firstBreak > 0 && p.IndexOf(',', 0, firstBreak) >= 0;
    }
}
