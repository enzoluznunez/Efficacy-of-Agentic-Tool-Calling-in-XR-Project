using UnityEngine;

public class DetailTool : Tool
{
    public float liftAboveMaximumHeight = 0f;

    protected override ToolType Kind => ToolType.Detail;

    protected override bool UsesSheetEvents => true;

    protected override void OnResetTool() => StatsTooltip.Hide();

    protected override void OnActiveChanged(bool active)
    {
        if (!active)
        {
            ClearTint();
            StatsTooltip.Hide();
        }
    }

    protected override void OnSheetSelect(ReadSheets.Reading reading)
    {
        if (!Active || sheetManager == null || !reading.valid || reading.cube == null) return;
        sheetManager.SetCellTint(reading.cube);
    }

    protected override void OnSheetRelease(ReadSheets.Reading reading) => ClearTint();

    protected override void OnSheetCleared() => ClearTint();

    protected override void OnSheetCommit(ReadSheets.Reading reading)
    {
        ClearTint();
        if (!Active || sheetManager == null || !reading.valid || reading.cube == null) return;

        if (!Project(reading.cube.dataRow, reading.cube.dataCol, reading.visRow, reading.visCol)) return;

        ShowStats(reading);
    }

    private void ShowStats(ReadSheets.Reading reading)
    {
        if (!StatsTooltip.TryResolve(sheetManager, reading,
                out Tooltip tooltip, out DataSource data, out CreateSheet piece)) return;

        string rowName = data.TitleAt(false, reading.visRow);
        string colName = data.TitleAt(true, reading.visCol);

        Tooltip.SelectionStats selection = new Tooltip.SelectionStats
        {
            title = $"{(string.IsNullOrEmpty(rowName) ? "Row " + (reading.visRow + 1) : rowName)} / " +
                    $"{(string.IsNullOrEmpty(colName) ? "Column " + (reading.visCol + 1) : colName)}",
            stats = SheetStats.Over(data, reading.visRow, reading.visRow, reading.visCol, reading.visCol)
        };

        ManageSheets sheets = sheetManager;
        CreateSheet target = piece;
        int visRow = reading.visRow;
        int visCol = reading.visCol;
        float lift = liftAboveMaximumHeight;
        Vector3 fallback = reading.point;

        tooltip.ShowStats(
            () => sheets != null && sheets.TryProjectionTopPoint(target, visRow, visCol, lift, out Vector3 raised)
                ? raised
                : fallback,
            selection);
    }

    public bool Show(int visRow, int visCol)
    {
        if (!Active || sheetManager == null) return false;

        CreateCube cube = sheetManager.CubeAt(visRow, visCol);
        if (cube == null) return false;

        return Project(cube.dataRow, cube.dataCol, visRow, visCol);
    }

    private bool Project(int dataRow, int dataCol, int visRow, int visCol)
    {
        ProjectionRecord rec = new ProjectionRecord
        {
            isStrip = false,
            dataRow = dataRow,
            dataCol = dataCol,
            lift = liftAboveMaximumHeight
        };

        if (!sheetManager.PushProjection(rec, EditKind.Detail)) return false;

        DataSource data = Scene.Data;
        Report($"projected the cell at {DataSource.LabelAt(data, false, visRow)}" +
               $" / {DataSource.LabelAt(data, true, visCol)}");
        return true;
    }
}
