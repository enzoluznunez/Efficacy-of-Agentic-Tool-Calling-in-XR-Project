using UnityEngine;

public class SliceTool : AxisTool
{
    public float sliceGapCells = 0.5f;

    private struct CutInfo
    {
        public bool valid;
        public CreateSheet sheet;
        public int boundary;
    }

    protected override ToolType Kind => ToolType.Slice;

    protected override bool UsesSheetEvents => true;

    protected override void OnResetTool()
    {
        if (sheetManager != null) sheetManager.ResetSlices();
        ClearTint();
    }

    protected override void OnOptionChanged() => ClearTint();

    protected override void OnActiveChanged(bool active)
    {
        if (!active) ClearTint();
    }

    private CutInfo ComputeCut(ReadSheets.Reading reading)
    {
        CutInfo info = default;
        if (!Active || !HasOption || sheetManager == null) return info;
        if (!reading.valid || reading.sheet == null) return info;

        CreateSheet sheet = reading.sheet;
        Vector3 local = sheet.transform.InverseTransformPoint(reading.point);

        int min, max;
        float fractional;

        if (Axis == SliceAxis.Column)
        {
            min = sheet.colMin; max = sheet.colMax;
            fractional = sheet.LineFraction(true, local.x);
        }
        else
        {
            min = sheet.rowMin; max = sheet.rowMax;
            fractional = sheet.LineFraction(false, local.z);
        }

        if (max - min < 1) return info;

        info.valid = true;
        info.sheet = sheet;
        info.boundary = Mathf.Clamp(Mathf.RoundToInt(fractional - 0.5f), min, max - 1);
        return info;
    }

    protected override void OnSheetHover(ReadSheets.Reading reading)
    {
        CutInfo cut = ComputeCut(reading);
        if (!cut.valid) { ClearTint(); return; }

        sheetManager.SetLineTint(cut.sheet, Axis == SliceAxis.Column ? 1 : 2,
            cut.boundary, cut.boundary + 1);
    }

    protected override void OnSheetSelect(ReadSheets.Reading reading)
    {
        CutInfo cut = ComputeCut(reading);
        if (!cut.valid) { ClearTint(); return; }

        sheetManager.SetLineTint(cut.sheet, Axis == SliceAxis.Column ? 1 : 2,
            cut.boundary, cut.boundary + 1, Style.PreviewSwell + Style.EngageSwell);
    }

    protected override void OnSheetCleared() => ClearTint();

    protected override void OnSheetCommit(ReadSheets.Reading reading)
    {
        CutInfo cut = ComputeCut(reading);
        if (cut.valid) CutAt(cut.sheet, cut.boundary, out _);
        ClearTint();
    }

    public bool CutAt(CreateSheet sheet, int boundary, out SliceRecord record)
    {
        record = default;
        if (!Active || !HasOption || sheetManager == null || sheet == null) return false;

        float gap = sliceGapCells * sheetManager.CellSize;
        if (!sheetManager.Slice(sheet, Axis, boundary, gap, out record, StateChannel.InAgentCall)) return false;

        ManageDatasets.ActiveEdits.PushSlice(record);

        DataSource data = Scene.Data;
        bool columns = Axis == SliceAxis.Column;
        string line = DataSource.LabelAt(data, columns, record.boundary);

        string layout = PiecesFact.Update();
        Report($"sliced piece {record.aId} after {line}, making pieces {record.aId} and {record.bId}; " +
               $"the pieces now run {layout} in order along the sheet");
        return true;
    }
}
