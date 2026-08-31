using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

public struct SheetLabelStyle
{
    public bool show;
    public Color color;
    public float gap;
}

public class SheetLabels
{
    private static readonly Vector2 LabelBox = new Vector2(20f, 4f);

    private const float FadeSeconds = 0.12f;

    private class Side
    {
        public bool high;
        public bool flipping;
        public bool want;
        public float fade = 1f;
    }

    private readonly Transform _owner;
    private readonly List<TextMeshPro> _pool = new List<TextMeshPro>();
    private readonly Dictionary<int, TextMeshPro> _colLabel = new Dictionary<int, TextMeshPro>();
    private readonly Dictionary<int, TextMeshPro> _rowLabel = new Dictionary<int, TextMeshPro>();
    private readonly Side _cols = new Side();
    private readonly Side _rows = new Side();
    private Transform _root;
    private int _used;
    private bool _placed;

    private float _lowZ;
    private float _highZ;
    private float _lowX;
    private float _highX;
    private float _margin;

    public SheetLabels(Transform owner) => _owner = owner;

    public void FaceViewer(float dt)
    {
        if (_used == 0 || _owner == null) return;

        Transform cam = CameraRig.MainTransform;
        if (cam == null) return;

        Vector3 local = _owner.InverseTransformPoint(cam.position);

        if (!_placed)
        {
            _placed = true;
            Snap(_cols, local.z > 0f, _colLabel, _lowZ, _highZ, true);
            Snap(_rows, local.x > 0f, _rowLabel, _lowX, _highX, false);
            return;
        }

        Step(_cols, Decide(local.z, _cols.high), dt, _colLabel, _lowZ, _highZ, true);
        Step(_rows, Decide(local.x, _rows.high), dt, _rowLabel, _lowX, _highX, false);
    }

    private static void Snap(Side side, bool high, Dictionary<int, TextMeshPro> map,
        float lowEdge, float highEdge, bool columns)
    {
        side.high = high;
        side.flipping = false;
        side.fade = 1f;

        ApplyEdge(map, high ? highEdge : lowEdge, columns, high);
        ApplyFade(map, 1f);
    }

    private bool Decide(float coord, bool high) =>
        high ? coord > -_margin : coord > _margin;

    private void Step(Side side, bool desired, float dt,
        Dictionary<int, TextMeshPro> map, float lowEdge, float highEdge, bool columns)
    {
        if (!side.flipping && desired != side.high)
        {
            side.flipping = true;
            side.want = desired;
        }

        if (side.flipping)
        {
            side.fade -= dt / FadeSeconds;
            if (side.fade <= 0f)
            {
                side.fade = 0f;
                side.high = side.want;
                side.flipping = false;
                ApplyEdge(map, side.high ? highEdge : lowEdge, columns, side.high);
            }
        }
        else if (side.fade < 1f)
        {
            side.fade = Mathf.Min(1f, side.fade + dt / FadeSeconds);
        }
        else return;

        ApplyFade(map, side.fade);
    }

    private const float UpwardTiltDegrees = 45f;

    private static Quaternion Facing(bool columns, bool high) =>
        (columns
            ? Quaternion.Euler(0f, high ? 180f : 0f, 0f)
            : Quaternion.Euler(0f, high ? -90f : 90f, 0f))
        * Quaternion.Euler(UpwardTiltDegrees, 0f, 0f);

    private static void ApplyEdge(Dictionary<int, TextMeshPro> map, float edge, bool columns, bool high)
    {
        Quaternion facing = Facing(columns, high);

        foreach (KeyValuePair<int, TextMeshPro> pair in map)
        {
            if (pair.Value == null) continue;

            Vector3 p = pair.Value.transform.localPosition;
            if (columns) p.z = edge;
            else p.x = edge;
            pair.Value.transform.localPosition = p;
            pair.Value.transform.localRotation = facing;
        }
    }

    private static void ApplyFade(Dictionary<int, TextMeshPro> map, float alpha)
    {
        foreach (KeyValuePair<int, TextMeshPro> pair in map)
            if (pair.Value != null) pair.Value.alpha = alpha;
    }

    public void MoveLine(bool columns, int line, float coord)
    {
        Dictionary<int, TextMeshPro> map = columns ? _colLabel : _rowLabel;
        if (!map.TryGetValue(line, out TextMeshPro label) || label == null) return;

        Vector3 p = label.transform.localPosition;
        if (columns) p.x = coord;
        else p.z = coord;
        label.transform.localPosition = p;
    }

    public void Rebuild(DataSource data, int rowMin, int rowMax, int colMin, int colMax,
        float cellSize, float cubeSide, float baseY, SheetLabelStyle style)
    {
        _used = 0;
        _colLabel.Clear();
        _rowLabel.Clear();

        _cols.fade = 1f;
        _cols.flipping = false;
        _rows.fade = 1f;
        _rows.flipping = false;

        if (data != null && style.show && cellSize > 0f)
        {
            float centerX = CreateSheet.Center(colMin, colMax, cellSize);
            float centerZ = CreateSheet.Center(rowMin, rowMax, cellSize);
            float half = cubeSide * 0.5f;
            float y = baseY;

            _margin = cellSize;
            _lowZ = rowMin * cellSize - centerZ - half - style.gap;
            _highZ = rowMax * cellSize - centerZ + half + style.gap;
            _lowX = colMin * cellSize - centerX - half - style.gap;
            _highX = colMax * cellSize - centerX + half + style.gap;

            IReadOnlyList<int> colOrder = data.ColumnOrder;
            IReadOnlyList<string> colTitles = data.ColumnTitles;
            float edgeZ = _cols.high ? _highZ : _lowZ;
            for (int vc = colMin; vc <= colMax; vc++)
            {
                if (vc < 0 || vc >= colOrder.Count) continue;
                int dCol = colOrder[vc];
                if (dCol < 0 || dCol >= colTitles.Count) continue;
                TextMeshPro label = Place(colTitles[dCol],
                    new Vector3(vc * cellSize - centerX, y, edgeZ), style.color,
                    Facing(true, _cols.high));
                if (label != null) _colLabel[vc] = label;
            }

            IReadOnlyList<int> rowOrder = data.RowOrder;
            IReadOnlyList<string> rowTitles = data.RowTitles;
            float edgeX = _rows.high ? _highX : _lowX;
            for (int vr = rowMin; vr <= rowMax; vr++)
            {
                if (vr < 0 || vr >= rowOrder.Count) continue;
                int dRow = rowOrder[vr];
                if (dRow < 0 || dRow >= rowTitles.Count) continue;
                TextMeshPro label = Place(rowTitles[dRow],
                    new Vector3(edgeX, y, vr * cellSize - centerZ), style.color,
                    Facing(false, _rows.high));
                if (label != null) _rowLabel[vr] = label;
            }
        }

        for (int i = _used; i < _pool.Count; i++)
            if (_pool[i] != null) _pool[i].gameObject.SetActive(false);
    }

    private TextMeshPro Place(string text, Vector3 localPosition, Color color, Quaternion facing)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        TextMeshPro label = Acquire(_used++);
        if (label == null) return null;

        label.text = text;
        label.color = color;

        Transform t = label.transform;
        t.localPosition = localPosition;
        t.localRotation = facing;
        t.localScale = Vector3.one * Style.WorldTextScale;

        label.gameObject.SetActive(true);
        return label;
    }

    private TextMeshPro Acquire(int index)
    {
        EnsureRoot();
        if (_root == null) return null;

        while (_pool.Count <= index)
        {
            GameObject go = new GameObject($"Label_{_pool.Count}");
            go.transform.SetParent(_root, false);

            TextMeshPro label = go.AddComponent<TextMeshPro>();
            Style.ApplyBody(label);
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.rectTransform.sizeDelta = LabelBox;
            _pool.Add(label);
        }

        return _pool[index];
    }

    private void EnsureRoot()
    {
        if (_root != null || _owner == null) return;
        GameObject go = new GameObject("Labels");
        _root = go.transform;
        _root.SetParent(_owner, false);
    }
}
