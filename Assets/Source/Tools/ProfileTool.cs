using UnityEngine;

public class ProfileTool : AxisTool
{
    public float liftAboveMaximumHeight = 0f;

    protected override ToolType Kind => ToolType.Profile;

    protected override bool UsesSheetEvents => true;

    protected override void OnResetTool() => StatsTooltip.Hide();

    protected override void OnOptionChanged()
    {
        ClearTint();
        StatsTooltip.Hide();
    }

    protected override void OnActiveChanged(bool active)
    {
        if (!active)
        {
            ClearTint();
            StatsTooltip.Hide();
        }
    }

    protected override void OnSheetHover(ReadSheets.Reading reading)
    {
        if (!Active || !HasOption || sheetManager == null || !reading.valid || reading.cube == null)
        {
            ClearTint();
            return;
        }

        bool columns = Axis == SliceAxis.Column;
        int line = columns ? reading.visCol : reading.visRow;
        sheetManager.SetLineTint(reading.sheet, columns ? 1 : 2, line, line);
    }

    protected override void OnSheetSelect(ReadSheets.Reading reading)
    {
        if (!Active || !HasOption || sheetManager == null || !reading.valid || reading.cube == null) return;

        bool columns = Axis == SliceAxis.Column;
        int line = columns ? reading.visCol : reading.visRow;
        sheetManager.SetLineTint(reading.sheet, columns ? 1 : 2, line, line,
            Style.PreviewSwell + Style.EngageSwell);
    }

    protected override void OnSheetRelease(ReadSheets.Reading reading) => ClearTint();

    protected override void OnSheetCleared() => ClearTint();

    protected override void OnSheetCommit(ReadSheets.Reading reading)
    {
        ClearTint();
        if (!Active || !HasOption || sheetManager == null || !reading.valid || reading.cube == null) return;

        if (!Project(reading.cube.dataRow, reading.cube.dataCol, reading.visRow, reading.visCol)) return;

        ShowStats(reading);
    }

    private void ShowStats(ReadSheets.Reading reading)
    {
        if (!StatsTooltip.TryResolve(sheetManager, reading,
                out Tooltip tooltip, out DataSource data, out CreateSheet piece)) return;

        bool columns = Axis == SliceAxis.Column;
        int line = columns ? reading.visCol : reading.visRow;
        string name = data.TitleAt(columns, line);

        Tooltip.SelectionStats selection = new Tooltip.SelectionStats
        {
            title = string.IsNullOrEmpty(name) ? $"{(columns ? "Column" : "Row")} {line + 1}" : name,
            stats = columns
                ? SheetStats.Over(data, piece.rowMin, piece.rowMax, line, line)
                : SheetStats.Over(data, line, line, piece.colMin, piece.colMax)
        };

        ManageSheets sheets = sheetManager;
        CreateSheet target = piece;
        float lift = liftAboveMaximumHeight;
        Vector3 fallback = reading.point;

        tooltip.ShowStats(
            () => sheets != null && sheets.TryStripTopPoint(target, columns, line, lift, out Vector3 raised)
                ? raised
                : fallback,
            selection);
    }

    public bool ShowProfile(int visRow, int visCol)
    {
        if (!Active || !HasOption || sheetManager == null) return false;

        CreateCube cube = sheetManager.CubeAt(visRow, visCol);
        if (cube == null) return false;

        return Project(cube.dataRow, cube.dataCol, visRow, visCol);
    }

    private bool Project(int dataRow, int dataCol, int visRow, int visCol)
    {
        bool columns = Axis == SliceAxis.Column;

        ProjectionRecord rec = new ProjectionRecord
        {
            isStrip = true,
            isColumn = columns,
            dataRow = dataRow,
            dataCol = dataCol,
            lift = liftAboveMaximumHeight
        };

        if (!sheetManager.PushProjection(rec, EditKind.Profile)) return false;

        DataSource data = Scene.Data;
        int line = columns ? visCol : visRow;
        Report($"projected {DataSource.LabelAt(data, columns, line)}");
        return true;
    }
}
