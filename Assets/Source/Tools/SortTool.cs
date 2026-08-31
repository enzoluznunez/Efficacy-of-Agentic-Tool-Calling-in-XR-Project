using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SortTool : AxisTool
{
    public float reflowSmoothing = 18f;

    private readonly List<SortLineProxy> _proxies = new List<SortLineProxy>();

    private SortLineProxy _held;
    private CreateSheet _dragSheet;
    private int _dragLine = -1;
    private int _dragTarget;
    private int _lineMin;
    private int _lineMax;

    protected override ToolType Kind => ToolType.Sort;

    protected override void OnToolStart()
    {
        if (sheetManager != null) sheetManager.OnSheetsChanged += RebuildProxies;
    }

    protected override void OnToolDestroy()
    {
        if (sheetManager != null) sheetManager.OnSheetsChanged -= RebuildProxies;
        CompleteOrderSequence();
        CancelDrag();
        ClearProxies();
    }

    private void OnDisable() => CompleteOrderSequence();

    protected override void OnResetTool()
    {
        CompleteOrderSequence();
        CancelDrag();
        if (sheetManager != null) sheetManager.SuppressNextReflow();
        Scene.Data?.ResetOrder();
        RebuildProxies();
    }

    protected override void OnOptionChanged()
    {
        CancelDrag();
        RebuildProxies();
    }

    protected override void OnActiveChanged(bool active)
    {
        CancelDrag();
        RebuildProxies();
    }

    protected override void ClearToolState()
    {
        base.ClearToolState();
        CancelDrag();
        RebuildProxies();
    }

    private void RebuildProxies()
    {
        if (_sequence != null) return;
        if (_held != null) CancelDrag();
        ClearProxies();

        if (!Active || !HasOption || sheetManager == null || !sheetManager.IsBuilt) return;

        bool columns = Axis == SliceAxis.Column;
        IReadOnlyList<CreateSheet> sheets = sheetManager.Sheets;

        for (int i = 0; i < sheets.Count; i++)
        {
            CreateSheet sheet = sheets[i];
            if (sheet == null || !sheet.IsBuilt) continue;

            int min = columns ? sheet.colMin : sheet.rowMin;
            int max = columns ? sheet.colMax : sheet.rowMax;
            for (int line = min; line <= max; line++)
            {
                SortLineProxy proxy = SortLineProxy.Create(sheet, columns, line, sheetManager.Height);
                if (proxy != null) _proxies.Add(proxy);
            }
        }
    }

    private void ClearProxies()
    {
        for (int i = 0; i < _proxies.Count; i++)
            if (_proxies[i] != null) Destroy(_proxies[i].gameObject);
        _proxies.Clear();
    }

    private SortLineProxy FirstGrabbedProxy()
    {
        for (int i = 0; i < _proxies.Count; i++)
            if (_proxies[i] != null && _proxies[i].IsGrabbed) return _proxies[i];
        return null;
    }

    private void Update()
    {
        if (!Active || !HasOption || sheetManager == null) return;

        if (_held != null && !_held.IsGrabbed) { CommitDrag(); return; }

        if (_held == null)
        {
            SortLineProxy grabbed = FirstGrabbedProxy();
            if (grabbed == null) return;
            BeginDrag(grabbed);
            if (_held == null) return;
        }

        bool columns = Axis == SliceAxis.Column;
        float dragged = _held.Coord;

        _dragTarget = Mathf.Clamp(
            Mathf.RoundToInt(_dragSheet.LineFraction(columns, dragged)), _lineMin, _lineMax);

        Reflow(dragged);
    }

    private void BeginDrag(SortLineProxy proxy)
    {
        CreateSheet sheet = proxy.sheet;
        if (sheet == null) return;

        bool columns = Axis == SliceAxis.Column;

        _dragSheet = sheet;
        _lineMin = columns ? sheet.colMin : sheet.rowMin;
        _lineMax = columns ? sheet.colMax : sheet.rowMax;
        _dragLine = proxy.line;
        _dragTarget = _dragLine;
        _held = proxy;
    }

    private void Reflow(float dragged)
    {
        float t = 1f - Mathf.Exp(-reflowSmoothing * Time.deltaTime);
        bool columns = Axis == SliceAxis.Column;

        for (int line = _lineMin; line <= _lineMax; line++)
        {
            if (line == _dragLine)
            {
                _dragSheet.LayoutLine(columns, line, dragged);
                continue;
            }

            float goal = _dragSheet.LineCoord(columns, DisplaySlot(line, _dragLine, _dragTarget));
            float now = _dragSheet.LineOffset(columns, line);
            _dragSheet.LayoutLine(columns, line, Mathf.Lerp(now, goal, t));
        }
    }

    private void CommitDrag()
    {
        int from = _dragLine;
        int target = _dragTarget;
        CreateSheet sheet = _dragSheet;

        _held = null;
        ClearDrag();

        if (!MoveLine(Axis == SliceAxis.Column, from, target, sheet)) RestLayout(sheet);
        RebuildProxies();
    }

    [Tooltip("Beyond this many steps a set-order request stops animating one line at a time and moves them together.")]
    public int maxSequencedSteps = 24;

    public int LastReorderedLines { get; private set; }

    public bool SequenceRunning => _sequence != null;

    private Coroutine _sequence;
    private System.Action _sequenceFinish;

    public void CompleteOrderSequence()
    {
        if (_sequence != null) { StopCoroutine(_sequence); _sequence = null; }
        var finish = _sequenceFinish;
        _sequenceFinish = null;
        finish?.Invoke();
    }

    public bool HaltOrderSequence()
    {
        if (_sequence == null) return false;

        StopCoroutine(_sequence);
        _sequence = null;
        _sequenceFinish = null;
        if (sheetManager != null) sheetManager.ForceAgentMotion = false;
        RebuildProxies();
        return true;
    }

    public bool SetOrder(bool columns, IReadOnlyList<int> targetOrder)
    {
        if (!Active || !HasOption) return false;

        DataSource src = Scene.Data;
        if (src == null || targetOrder == null || targetOrder.Count == 0) return false;

        CompleteOrderSequence();

        IReadOnlyList<int> live = columns ? src.ColumnOrder : src.RowOrder;
        var preOrder = new List<int>(live);
        DataSource.SortMode preMode = columns ? src.ColumnSortMode : src.RowSortMode;

        var steps = PlanSteps(preOrder, targetOrder);
        if (steps.Count == 0) return false;

        int changed = 0;
        for (int i = 0; i < targetOrder.Count && i < preOrder.Count; i++)
            if (preOrder[i] != targetOrder[i]) changed++;
        LastReorderedLines = changed;

        ManageDatasets.ActiveEdits.PushReorder(columns, preOrder, preMode, changed);
        Report($"set the order of {changed} {(columns ? "columns" : "rows")}");

        var final = new List<int>(targetOrder);
        bool stepwise = sheetManager != null && sheetManager.AgentMotionAnimates
                        && steps.Count <= maxSequencedSteps;

        if (!stepwise)
        {
            Apply(src, columns, final);
            return true;
        }

        sheetManager.ForceAgentMotion = true;
        _sequenceFinish = () => {
            if (sheetManager != null) sheetManager.ForceAgentMotion = false;
            Apply(src, columns, final);
        };
        _sequence = StartCoroutine(WalkOrder(src, columns, steps, final));
        return true;
    }

    private static void Apply(DataSource src, bool columns, IReadOnlyList<int> order)
    {
        if (columns) src.SetColumnOrder(order, DataSource.SortMode.Manual);
        else src.SetRowOrder(order, DataSource.SortMode.Manual);
    }

    private struct OrderStep
    {
        public int key;
        public int pos;
    }

    private static List<OrderStep> PlanSteps(IReadOnlyList<int> preOrder, IReadOnlyList<int> targetOrder)
    {
        var working = new List<int>(preOrder);
        var steps = new List<OrderStep>();
        for (int i = 0; i < targetOrder.Count && i < working.Count; i++)
        {
            int want = targetOrder[i];
            int at = working.IndexOf(want);
            if (at < 0 || at == i) continue;
            working.RemoveAt(at);
            working.Insert(i, want);
            steps.Add(new OrderStep { key = want, pos = i });
        }
        return steps;
    }

    private IEnumerator WalkOrder(DataSource src, bool columns, List<OrderStep> steps, List<int> final)
    {
        for (int i = 0; i < steps.Count; i++)
        {
            OrderStep step = steps[i];
            IReadOnlyList<int> live = columns ? src.ColumnOrder : src.RowOrder;
            int at = -1;
            for (int v = 0; v < live.Count; v++) if (live[v] == step.key) { at = v; break; }
            if (at < 0 || at == step.pos) continue;

            if (columns) src.MoveColumn(at, step.pos);
            else src.MoveRow(at, step.pos);

            if (sheetManager != null) yield return sheetManager.WaitForReflow();
            else yield return null;
        }

        if (sheetManager != null) sheetManager.ForceAgentMotion = false;
        _sequence = null;
        _sequenceFinish = null;
        Apply(src, columns, final);
    }

    public bool MoveLine(bool columns, int from, int to, CreateSheet piece)
    {
        if (!Active || !HasOption) return false;

        DataSource src = Scene.Data;
        if (src == null || from == to) return false;

        IReadOnlyList<int> order = columns ? src.ColumnOrder : src.RowOrder;
        if (from < 0 || from >= order.Count || to < 0 || to >= order.Count) return false;

        var preOrder = new List<int>(order);
        DataSource.SortMode preMode = columns ? src.ColumnSortMode : src.RowSortMode;

        int lineMin = piece == null ? 0 : (columns ? piece.colMin : piece.rowMin);
        string where = piece != null ? $" in piece {piece.sheetId}" : "";

        string line = DataSource.LabelAt(src, columns, from);

        var postOrder = new List<int>(preOrder);
        int moved = postOrder[from];
        postOrder.RemoveAt(from);
        postOrder.Insert(to, moved);

        string arrangement = "";
        if (postOrder.Count <= 40)
        {
            List<string> names = DataSource.TitlesFor(src, columns, postOrder);
            arrangement = $"; the {(columns ? "columns" : "rows")} now run: {string.Join(", ", names)}";
        }

        Report($"moved {line} from position {from - lineMin + 1} to {to - lineMin + 1}{where}{arrangement}");

        if (StateChannel.UserDriven) StalePositions.MarkDirty(columns);

        ManageDatasets.ActiveEdits.PushSort(columns, preOrder, preMode, from, to);

        if (columns) src.MoveColumn(from, to);
        else src.MoveRow(from, to);
        return true;
    }

    private void CancelDrag()
    {
        CreateSheet sheet = _dragSheet;
        _held = null;
        ClearDrag();
        RestLayout(sheet);
    }

    private void ClearDrag()
    {
        _dragSheet = null;
        _dragLine = -1;
    }

    private void RestLayout(CreateSheet sheet)
    {
        if (sheet != null) sheet.RestLines();
    }

    private static int DisplaySlot(int slot, int from, int target)
    {
        if (slot == from) return target;
        int p = slot > from ? slot - 1 : slot;
        if (p >= target) p += 1;
        return p;
    }
}
