using System.Collections.Generic;

public static class PiecesFact
{
    public static string Layout()
    {
        var mgr = Scene.Sheets;
        if (mgr == null || !mgr.IsBuilt || mgr.Sheets.Count <= 1) return null;

        var ordered = new List<CreateSheet>(mgr.Sheets);
        ordered.Sort((x, y) => x.colMin != y.colMin ? x.colMin.CompareTo(y.colMin) : x.rowMin.CompareTo(y.rowMin));
        var ids = new List<string>(ordered.Count);
        foreach (CreateSheet s in ordered) ids.Add(s.sheetId.ToString());
        return string.Join(", ", ids);
    }

    public static string Update()
    {
        var mgr = Scene.Sheets;
        if (mgr == null || !mgr.IsBuilt) return null;

        string layout = Layout();
        StateChannel.SetState("pieces", layout == null
            ? "the sheet is in one piece"
            : $"the sheet is in {mgr.Sheets.Count} pieces, ids {layout} in order");
        return layout;
    }
}
