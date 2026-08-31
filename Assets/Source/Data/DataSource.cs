using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public abstract class DataSource : MonoBehaviour
{

    public enum SortMode { Original, Manual }

    public IReadOnlyList<string> ColumnTitles => _columnTitles;
    public IReadOnlyList<string> RowTitles => _rowTitles;
    public int ColumnCount => _columnTitles.Count;
    public int RowCount => _rowTitles.Count;

    public string ColumnAxisTitle => _columnAxisTitle;
    public string RowAxisTitle => _rowAxisTitle;

    public IReadOnlyList<int> ColumnOrder => _columnOrder;
    public IReadOnlyList<int> RowOrder => _rowOrder;

    public string TitleAt(bool columns, int visIndex)
    {
        IReadOnlyList<int> order = columns ? _columnOrder : _rowOrder;
        IReadOnlyList<string> titles = columns ? _columnTitles : _rowTitles;
        if (order == null || titles == null || visIndex < 0 || visIndex >= order.Count) return null;
        int d = order[visIndex];
        return d >= 0 && d < titles.Count ? titles[d] : null;
    }

    public static string LabelAt(DataSource data, bool columns, int visIndex)
    {
        string title = data != null ? data.TitleAt(columns, visIndex) : null;
        return string.IsNullOrEmpty(title) ? $"{(columns ? "column" : "row")} {visIndex + 1}" : title;
    }
    public static List<string> TitlesFor(DataSource data, bool columns, IReadOnlyList<int> dataIndexes)
    {
        IReadOnlyList<string> titles = columns ? data.ColumnTitles : data.RowTitles;
        var list = new List<string>(dataIndexes.Count);
        for (int i = 0; i < dataIndexes.Count; i++)
        {
            int d = dataIndexes[i];
            list.Add(d >= 0 && d < titles.Count ? titles[d] : null);
        }
        return list;
    }

    public int VisIndexOf(bool columns, int dataIndex)
    {
        IReadOnlyList<int> order = columns ? _columnOrder : _rowOrder;
        if (order == null || dataIndex < 0) return -1;
        for (int v = 0; v < order.Count; v++)
            if (order[v] == dataIndex) return v;
        return -1;
    }

    public SortMode ColumnSortMode => _columnSortMode;
    public SortMode RowSortMode => _rowSortMode;

    public bool IsLoaded => _isLoaded;

    public string RawText => _rawText;
    protected string _rawText;

    public event Action OnDataLoaded;

    public event Action OnLayoutInvalidated;
    public event Action OnOrderChanged;

    protected List<string> _columnTitles = new List<string>();
    protected List<string> _rowTitles = new List<string>();
    protected string _columnAxisTitle;
    protected string _rowAxisTitle;
    protected float[,] _values = new float[0, 0];

    protected bool[,] _valid = new bool[0, 0];
    protected float _globalMin;
    protected float _globalMax = 1f;

    protected List<int> _columnOrder = new List<int>();
    protected List<int> _rowOrder = new List<int>();
    protected SortMode _columnSortMode = SortMode.Original;
    protected SortMode _rowSortMode = SortMode.Original;

    protected bool _isLoaded;

    protected void NotifyChanged()
    {
        EnsureOrders();
        OnLayoutInvalidated?.Invoke();
    }

    protected int FillGrid(int rowCount, int colCount, Func<int, int, float?> sample)
    {
        _values = new float[rowCount, colCount];
        _valid = new bool[rowCount, colCount];
        _globalMin = float.MaxValue;
        _globalMax = float.MinValue;
        int filled = 0;

        for (int r = 0; r < rowCount; r++)
        {
            for (int c = 0; c < colCount; c++)
            {
                float? v = sample(r, c);
                if (v.HasValue)
                {
                    _values[r, c] = v.Value;
                    _valid[r, c] = true;
                    filled++;
                    if (v.Value < _globalMin) _globalMin = v.Value;
                    if (v.Value > _globalMax) _globalMax = v.Value;
                }
                else
                {
                    _values[r, c] = float.NaN;
                    _valid[r, c] = false;
                }
            }
        }

        if (filled == 0)
        {
            _globalMin = 0f;
            _globalMax = 1f;
        }
        else if (Mathf.Approximately(_globalMin, _globalMax))
        {
            if (_globalMin > 0f) _globalMin = 0f;
            else if (_globalMax < 0f) _globalMax = 0f;
            else _globalMax = _globalMin + 1f;
        }

        return filled;
    }

    protected virtual void EnsureOrders()
    {
        EnsureOrder(_columnOrder, ColumnCount);
        EnsureOrder(_rowOrder, RowCount);
    }

    private void RaiseOrderChanged()
    {
        NotifyChanged();
        OnOrderChanged?.Invoke();
    }

    public void ResetOrder()
    {
        _columnSortMode = SortMode.Original;
        _rowSortMode = SortMode.Original;
        InitIdentity(_columnOrder, ColumnCount);
        InitIdentity(_rowOrder, RowCount);
        RaiseOrderChanged();
    }

    public bool SetColumnOrder(IReadOnlyList<int> order, SortMode mode)
    {
        if (!ApplyOrder(_columnOrder, order, ColumnCount)) return false;
        _columnSortMode = mode;
        RaiseOrderChanged();
        return true;
    }

    public bool SetRowOrder(IReadOnlyList<int> order, SortMode mode)
    {
        if (!ApplyOrder(_rowOrder, order, RowCount)) return false;
        _rowSortMode = mode;
        RaiseOrderChanged();
        return true;
    }

    public void MoveColumn(int fromPos, int toPos)
    {
        if (MoveWithin(_columnOrder, fromPos, toPos)) SetColumnOrder(_columnOrder, SortMode.Manual);
    }

    public void MoveRow(int fromPos, int toPos)
    {
        if (MoveWithin(_rowOrder, fromPos, toPos)) SetRowOrder(_rowOrder, SortMode.Manual);
    }

    private static bool MoveWithin(List<int> order, int fromPos, int toPos)
    {
        if (fromPos < 0 || fromPos >= order.Count) return false;
        toPos = Mathf.Clamp(toPos, 0, order.Count - 1);
        if (toPos == fromPos) return false;
        int value = order[fromPos];
        order.RemoveAt(fromPos);
        order.Insert(toPos, value);
        return true;
    }

    private static bool ApplyOrder(List<int> target, IReadOnlyList<int> order, int count)
    {
        if (order == null || order.Count != count || !IsPermutation(order, count)) return false;

        if (!ReferenceEquals(target, order))
        {
            target.Clear();
            for (int i = 0; i < order.Count; i++) target.Add(order[i]);
        }
        return true;
    }

    private static bool IsPermutation(IReadOnlyList<int> order, int count)
    {
        if (count == 0) return order.Count == 0;
        bool[] seen = new bool[count];
        for (int i = 0; i < order.Count; i++)
        {
            int v = order[i];
            if (v < 0 || v >= count || seen[v]) return false;
            seen[v] = true;
        }
        return true;
    }

    private static void InitIdentity(List<int> order, int count)
    {
        order.Clear();
        for (int i = 0; i < count; i++) order.Add(i);
    }

    private static void EnsureOrder(List<int> order, int count)
    {
        if (order.Count == count) return;
        InitIdentity(order, count);
    }

    public float GetValue(int rowIndex, int colIndex)
    {
        if (rowIndex < 0 || rowIndex >= RowCount || colIndex < 0 || colIndex >= ColumnCount)
            return 0f;
        return _values[rowIndex, colIndex];
    }

    public bool HasValue(int rowIndex, int colIndex)
    {
        return rowIndex >= 0 && rowIndex < _valid.GetLength(0) &&
               colIndex >= 0 && colIndex < _valid.GetLength(1) &&
               _valid[rowIndex, colIndex];
    }

    private static bool TryBaselineRange(float min, float max, out float lo, out float range)
    {
        lo = Mathf.Min(0f, min);
        range = Mathf.Max(0f, max) - lo;
        return range > 0f;
    }

    public float GetHeightFraction(int rowIndex, int colIndex)
    {
        if (!HasValue(rowIndex, colIndex)) return 0f;
        if (!TryBaselineRange(_globalMin, _globalMax, out float lo, out float range)) return 0f;
        return (GetValue(rowIndex, colIndex) - lo) / range;
    }

    public float ZeroFraction =>
        TryBaselineRange(_globalMin, _globalMax, out float lo, out float range) ? -lo / range : 0f;

    public float GetColorFraction(int rowIndex, int colIndex)
    {
        float range = _globalMax - _globalMin;
        if (range <= 0f) return 0f;

        float v = GetValue(rowIndex, colIndex);
        if (float.IsNaN(v)) return 0f;
        return (v - _globalMin) / range;
    }

    protected void RaiseDataLoaded() => RaiseLoadComplete(true);

    protected void RaiseLoadFailed() => RaiseLoadComplete(false);

    private void RaiseLoadComplete(bool loaded)
    {
        _isLoaded = loaded;
        _columnSortMode = SortMode.Original;
        _rowSortMode = SortMode.Original;
        InitIdentity(_columnOrder, ColumnCount);
        InitIdentity(_rowOrder, RowCount);
        OnDataLoaded?.Invoke();
    }
}
