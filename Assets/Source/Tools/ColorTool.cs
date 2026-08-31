using System.Collections.Generic;
using UnityEngine;

public class ColorTool : ToolOptions
{
    public Color[] palette =
    {
        new Color(0.90f, 0.22f, 0.22f),
        new Color(0.95f, 0.55f, 0.15f),
        new Color(0.95f, 0.85f, 0.22f),
        new Color(0.32f, 0.80f, 0.38f),
        new Color(0.25f, 0.55f, 0.95f),
        new Color(0.29f, 0.00f, 0.51f),
        new Color(0.56f, 0.00f, 1.00f),
    };

    public static readonly string[] DefaultPaletteNames =
    {
        "Red", "Orange", "Yellow", "Green", "Blue", "Indigo", "Violet",
    };

    public string[] paletteNames = (string[])DefaultPaletteNames.Clone();

    protected override ToolType Kind => ToolType.Color;

    protected override bool UsesSheetEvents => true;

    public override IReadOnlyList<string> Options => paletteNames;
    public override string OptionNoun => "color";

    private bool _stroking;
    private CreateSheet _strokeSheet;
    private float _strokeStart;
    private readonly HashSet<long> _strokeVisited = new HashSet<long>();
    private readonly List<ColorCell> _strokeCells = new List<ColorCell>();

    private Color CurrentColor =>
        Selected >= 0 && Selected < palette.Length ? palette[Selected] : default;

    protected override ButtonList BuildOptions() =>
        toolPanelUI == null ? null : toolPanelUI.AddSwatchGrid(Kind, palette, Select);

    protected override void OnResetTool()
    {
        EndStroke();
        if (sheetManager != null) sheetManager.ResetColors();
        ClearOption(false);
    }

    protected override void OnOptionChanged()
    {
        EndStroke();
        ClearTint();
    }

    protected override void OnActiveChanged(bool active)
    {
        if (active) return;
        EndStroke();
        ClearTint();
    }

    protected override void OnSheetHover(ReadSheets.Reading reading)
    {
        if (!Active || !HasOption || sheetManager == null || !reading.valid || reading.cube == null)
        {
            if (!_stroking) ClearTint();
            return;
        }

        if (_stroking)
        {
            if (reading.sheet == _strokeSheet) Paint(reading.cube);
            return;
        }

    }

    protected override void OnSheetSelect(ReadSheets.Reading reading)
    {
        if (!Active || !HasOption || sheetManager == null || !reading.valid || reading.cube == null) return;

        EndStroke();
        sheetManager.SetCellTint(reading.cube);
        _stroking = true;
        _strokeSheet = reading.sheet;
        _strokeStart = Time.realtimeSinceStartup;
        Paint(reading.cube);
    }

    protected override void OnSheetRelease(ReadSheets.Reading reading) => EndStroke();

    protected override void OnSheetCleared()
    {
        if (_stroking) { EndStroke(); return; }
        ClearTint();
    }

    private void Paint(CreateCube cube)
    {
        if (cube == null) return;
        long key = ((long)cube.dataRow << 32) | (uint)cube.dataCol;
        if (!_strokeVisited.Add(key)) return;

        _strokeCells.Add(RecordCell(cube.dataRow, cube.dataCol));
        sheetManager.AddCellColor(cube.dataRow, cube.dataCol, CurrentColor);
    }

    private void EndStroke()
    {
        bool commit = _stroking && _strokeCells.Count > 0;
        _stroking = false;
        _strokeSheet = null;

        if (commit) CommitStroke(new List<ColorCell>(_strokeCells), true);
        _strokeVisited.Clear();
        _strokeCells.Clear();
    }

    public bool PaintCell(int visRow, int visCol)
    {
        if (!Active || !HasOption || sheetManager == null) return false;

        CreateCube cube = sheetManager.CubeAt(visRow, visCol);
        if (cube == null) return false;

        return PaintCubes(new List<CreateCube> { cube }) > 0;
    }

    public int PaintLine(bool columns, int visLine)
    {
        if (!Active || !HasOption || sheetManager == null) return 0;

        List<CreateCube> cubes = new List<CreateCube>();
        sheetManager.CubesInLine(columns, visLine, cubes);
        return PaintCubes(cubes);
    }

    private int PaintCubes(List<CreateCube> cubes)
    {
        List<ColorCell> cells = new List<ColorCell>();
        List<CreateCube> toPaint = new List<CreateCube>();
        for (int i = 0; i < cubes.Count; i++)
        {
            CreateCube cube = cubes[i];
            if (cube == null) continue;
            cells.Add(RecordCell(cube.dataRow, cube.dataCol));
            toPaint.Add(cube);
        }

        if (cells.Count == 0) return 0;
        sheetManager.AddCellColorsSwept(toPaint, CurrentColor);
        CommitStroke(cells, false);
        return cells.Count;
    }

    private ColorCell RecordCell(int dataRow, int dataCol)
    {
        string prevHex = sheetManager.TryGetCellColor(dataRow, dataCol, out Color prev)
            ? "#" + ColorUtility.ToHtmlStringRGB(prev)
            : null;
        return new ColorCell { dataRow = dataRow, dataCol = dataCol, prevColorHex = prevHex };
    }

    private void CommitStroke(List<ColorCell> cells, bool byHand)
    {
        ManageDatasets.ActiveEdits.PushColorStroke(CurrentColorName,
            "#" + ColorUtility.ToHtmlStringRGB(CurrentColor), cells);

        StudyLog.Event("color_stroke", new Dictionary<string, object>
        {
            { "color", CurrentColorName },
            { "cells", cells.Count },
            { "byHand", byHand },
            { "durationMs", byHand ? (int)((Time.realtimeSinceStartup - _strokeStart) * 1000f) : 0 }
        });

        Report(cells.Count == 1
            ? $"colored the cell at {CellLabel(cells[0])} {CurrentColorName}"
            : $"colored {cells.Count} cells {CurrentColorName}");
    }

    private static string CellLabel(ColorCell cell)
    {
        DataSource data = Scene.Data;
        string row = TitleOf(data != null ? data.RowTitles : null, cell.dataRow) ?? $"row {cell.dataRow + 1}";
        string col = TitleOf(data != null ? data.ColumnTitles : null, cell.dataCol) ?? $"column {cell.dataCol + 1}";
        return $"{row} / {col}";
    }

    private static string TitleOf(IReadOnlyList<string> titles, int index) =>
        titles != null && index >= 0 && index < titles.Count && !string.IsNullOrEmpty(titles[index])
            ? titles[index]
            : null;

    public string CurrentColorName => CurrentOptionName;
}
