using System;
using System.Collections.Generic;
using UnityEngine;

public class ManageDatasets : MonoBehaviour
{
    public static ManageDatasets Instance { get; private set; }
    public static DataSource ActiveSource => Instance != null ? Instance.Active : null;

    public ManageSheets sheetManager;
    public PanelUI dataPanelUI;
    public ManageTools toolManager;

    private IDataPanel Panel => dataPanelUI as IDataPanel;

    public event Action OnDatasetsChanged;
    public event Action<int> OnActiveDatasetChanged;
    public event Action<string> OnDatasetLoadFailed;

    public class Dataset
    {
        public DataSource source;
        public string label;
        public string payload;
        public int sheetId = ManageSheets.FirstSheetId;
        public bool loaded;
        public readonly EditList Edits = new EditList();
    }

    private readonly List<Dataset> _datasets = new List<Dataset>();
    private int _active = -1;
    private int _datasetsCreated;
    private static readonly EditList Unowned = new EditList();

    public IReadOnlyList<Dataset> Datasets => _datasets;
    public int ActiveIndex => _active;
    public int DatasetCount => _datasets.Count;
    public DataSource Active => (_active >= 0 && _active < _datasets.Count) ? _datasets[_active].source : null;
    public Dataset ActiveDataset => (_active >= 0 && _active < _datasets.Count) ? _datasets[_active] : null;

    public static EditList ActiveEdits
    {
        get
        {
            Dataset d = Instance != null ? Instance.ActiveDataset : null;
            return d != null ? d.Edits : Unowned;
        }
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        ResolveRefs();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void ResolveRefs()
    {
        if (sheetManager == null) sheetManager = FindAnyObjectByType<ManageSheets>();
        if (dataPanelUI == null)
        {
            var panels = FindObjectsByType<PanelUI>(FindObjectsSortMode.None);
            for (int i = 0; i < panels.Length; i++)
                if (panels[i] is IDataPanel) { dataPanelUI = panels[i]; break; }
        }
        if (toolManager == null) toolManager = FindAnyObjectByType<ManageTools>();
    }

    public void AddDataset(string payload, string label = null)
    {
        if (string.IsNullOrEmpty(payload)) return;

        for (int i = 0; i < _datasets.Count; i++)
            if (_datasets[i].payload == payload)
            {
                bool alreadyActive = i == _active;
                string openLabel = _datasets[i].label;
                SwitchDataset(i);
                Notices.Show(this, "Already Open",
                    alreadyActive ? $"{openLabel} is already open." : $"Switched to {openLabel}.");
                return;
            }

        int ordinal = _datasetsCreated++;
        GameObject host = new GameObject($"Dataset_{ordinal}");
        host.transform.SetParent(transform, false);

        Parser reader = host.AddComponent<Parser>();

        Dataset dataset = new Dataset { source = reader, payload = payload, label = label ?? Stylize(DeriveLabel(payload, ordinal)) };
        _datasets.Add(dataset);

        reader.onLoadResult = (ok, reason) => OnDatasetLoadResult(reader, ok, reason);
        reader.Load(payload);
    }

    private int IndexOfSource(DataSource source)
    {
        for (int i = 0; i < _datasets.Count; i++)
            if (_datasets[i].source == source) return i;
        return -1;
    }

    private void OnDatasetLoadResult(Parser reader, bool ok, string reason)
    {
        int index = IndexOfSource(reader);
        if (index < 0) return;

        if (!ok)
        {
            string payload = _datasets[index].payload;
            RemoveDataset(index);
            OnDatasetLoadFailed?.Invoke(payload);
            Notices.Show(this, "Scan Failed",
                reason ?? "Couldn't read a dataset from that QR code.");
            return;
        }

        Dataset dataset = _datasets[index];
        dataset.loaded = true;

        StateChannel.Record("Dataset", $"loaded a new dataset, {dataset.label}");
        OnDatasetsChanged?.Invoke();
        SwitchDataset(index);
    }

    public void RemoveDataset(int index)
    {
        if (index < 0 || index >= _datasets.Count) return;

        Dataset dataset = _datasets[index];
        bool wasActive = index == _active;

        if (wasActive && sheetManager != null) sheetManager.CommitPendingGrabs();

        _datasets.RemoveAt(index);
        if (_active > index) _active--;
        else if (wasActive) _active = -1;

        if (dataset.source != null) Destroy(dataset.source.gameObject);

        if (wasActive)
        {
            if (_datasets.Count > 0) SwitchDataset(_datasets.Count - 1);
            else
            {
                if (Panel != null) Panel.Rebind(null);
                if (sheetManager != null) sheetManager.SetDataSource(null);
            }
        }

        if (dataset.loaded)
            StateChannel.Record("Dataset", $"closed the dataset {dataset.label}");
        OnDatasetsChanged?.Invoke();
        if (wasActive && _datasets.Count == 0) OnActiveDatasetChanged?.Invoke(-1);
    }


    public void SwitchDataset(int index)
    {
        HitchLog.Mark($"SwitchDataset {index}");
        if (index < 0 || index >= _datasets.Count || index == _active) return;
        if (_datasets[index].source == null)
        {
            Debug.LogWarning($"[ManageDatasets] Dataset {index} has no source; ignoring switch.");
            return;
        }

        if (sheetManager != null) sheetManager.CommitPendingGrabs();
        if (toolManager != null) toolManager.DeselectTool();

        if (_active >= 0 && _active < _datasets.Count && Panel != null)
            _datasets[_active].sheetId = Panel.ActiveSheetId;

        _active = index;
        Dataset next = _datasets[index];

        Rebind(next);
        if (sheetManager != null) sheetManager.PlaySwitchGrow();
        OnActiveDatasetChanged?.Invoke(index);

        if (Panel != null) Panel.ShowSheet(next.sheetId);

        StateChannel.RecordState("dataset",
            $"the {(string.IsNullOrEmpty(next.label) ? "dataset" : next.label)} dataset is open" +
            "; sheet ids, row and column numbers and edits all belong to it");

        ReportAxes(next.source);
    }

    private static void ReportAxes(DataSource source)
    {
        if (source == null) return;

        string columns = AxisSketch(source, true);
        string rows = AxisSketch(source, false);
        if (columns == null || rows == null) return;

        StateChannel.SetState("axes", $"the columns are {columns}; the rows are {rows}");
    }

    private static string AxisSketch(DataSource source, bool columns)
    {
        IReadOnlyList<int> order = columns ? source.ColumnOrder : source.RowOrder;
        if (order == null || order.Count == 0) return null;

        var shown = new List<string>(2);
        for (int i = 0; i < order.Count && shown.Count < 2; i++)
        {
            string title = source.TitleAt(columns, i);
            if (!string.IsNullOrEmpty(title)) shown.Add(title);
        }
        if (shown.Count == 0) return null;

        int rest = order.Count - shown.Count;
        string list = string.Join(", ", shown);
        return rest > 0 ? $"{list} and {rest} more" : list;
    }

    private void Rebind(Dataset dataset)
    {
        TryStep("dataPanel", () => { if (Panel != null) Panel.Rebind(dataset.source); });
        TryStep("sheetManager", () => { if (sheetManager != null) sheetManager.SetDataSource(dataset.source); });
        TryStep("replay", () => { if (sheetManager != null) sheetManager.ReplayEdits(dataset.Edits); });
    }

    private void TryStep(string label, Action step)
    {
        try { step(); }
        catch (Exception e) { Debug.LogError($"[ManageDatasets] Rebind step '{label}' failed: {e}"); }
    }

    private static string Stylize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;

        string[] words = raw.Split(new[] { '_', '-', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return raw;

        for (int i = 0; i < words.Length; i++)
        {
            if (HasInnerUppercase(words[i])) continue;
            string w = words[i].ToLowerInvariant();
            bool edge = i == 0 || i == words.Length - 1;
            words[i] = (!edge && SmallWords.Contains(w))
                ? w
                : char.ToUpperInvariant(w[0]) + w.Substring(1);
        }
        return string.Join(" ", words);
    }

    private static readonly HashSet<string> SmallWords = new HashSet<string>
    {
        "a", "an", "and", "as", "at", "but", "by", "for", "if", "in", "nor", "of",
        "on", "or", "per", "so", "the", "to", "up", "via", "vs", "yet"
    };

    private static bool HasInnerUppercase(string word)
    {
        for (int i = 1; i < word.Length; i++)
            if (char.IsUpper(word[i])) return true;
        return false;
    }

    private static string DeriveLabel(string payload, int ordinal)
    {
        string fallback = $"Dataset {ordinal + 1}";
        if (string.IsNullOrEmpty(payload)) return fallback;

        if (payload.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return DeriveDataUriName(payload) ?? fallback;

        if (payload.IndexOf('\n') >= 0)
            return fallback;

        string trimmed = payload.Trim();
        int query = trimmed.IndexOf('?');
        if (query >= 0) trimmed = trimmed.Substring(0, query);
        trimmed = trimmed.TrimEnd('/');

        int slash = trimmed.LastIndexOf('/');
        string name = slash >= 0 ? trimmed.Substring(slash + 1) : trimmed;

        int dot = name.LastIndexOf('.');
        if (dot > 0) name = name.Substring(0, dot);

        return string.IsNullOrEmpty(name) ? fallback : name;
    }

    private static string DeriveDataUriName(string payload)
    {
        int comma = payload.IndexOf(',');
        string header = comma >= 0 ? payload.Substring(0, comma) : payload;

        int nameIdx = header.IndexOf("name=", StringComparison.OrdinalIgnoreCase);
        if (nameIdx < 0) return null;

        string name = header.Substring(nameIdx + 5);
        int semi = name.IndexOf(';');
        if (semi >= 0) name = name.Substring(0, semi);
        name = name.Trim();

        int dot = name.LastIndexOf('.');
        if (dot > 0) name = name.Substring(0, dot);

        return string.IsNullOrEmpty(name) ? null : name;
    }
}
