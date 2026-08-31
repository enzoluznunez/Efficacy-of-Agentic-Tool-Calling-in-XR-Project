using System.Collections.Generic;
using Google.GenAI.Types;

public sealed class GetStatistics : AgenticTool<GetStatistics.Args> {

    public class Args {
        [Doc("A row: its name, or its 1-based position. Returns stats for that whole row."), Optional]
        public string row;
        [Doc("A column: its name, or its 1-based position. Returns stats for that whole column."), Optional]
        public string column;
        [Doc("'rows' or 'columns': returns stats for every line on that axis at once."), Optional]
        public string axis;
    }

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "GetStatistics",
        Description = "Read a row's or column's statistics: count, minimum, maximum, average and sum, the same " +
                      "numbers the user's Profile tooltip shows them, plus 'minAt' and 'maxAt', naming the line " +
                      "on the other axis where each extreme sits. " +
                      "Give 'row' or 'column' for one line, or " +
                      "'axis' ('rows' or 'columns') for every line on that axis in one call. " +
                      "Names and 1-based " +
                      "positions run across the whole dataset in display order. Use this for totals, averages and " +
                      "highest/lowest questions instead of reading raw cells and computing; GetNumbers stays the " +
                      "source for individual cell values.",
        Parameters = ParametersFor(typeof(Args))
    };

    protected override void Run(Args args, Dictionary<string, object> result) {
        var data = Scene.Data;
        var mgr = Scene.Sheets;
        if (data == null || mgr == null || !mgr.IsBuilt) { result["error"] = "No sheet in scene."; return; }

        bool hasRow = !string.IsNullOrWhiteSpace(args.row);
        bool hasColumn = !string.IsNullOrWhiteSpace(args.column);
        bool hasAxis = !string.IsNullOrWhiteSpace(args.axis);

        int given = (hasRow ? 1 : 0) + (hasColumn ? 1 : 0) + (hasAxis ? 1 : 0);
        if (given != 1) {
            result["error"] = "Give exactly one of 'row', 'column', or 'axis'.";
            return;
        }

        int rowMax = mgr.RowCount - 1;
        int colMax = mgr.ColCount - 1;

        if (hasAxis) {
            string axis = args.axis.Trim().ToLowerInvariant();
            bool columns = axis == "columns" || axis == "column";
            if (!columns && axis != "rows" && axis != "row") {
                result["error"] = "'axis' must be 'rows' or 'columns'.";
                return;
            }

            int count = columns ? colMax + 1 : rowMax + 1;
            var lines = new List<object>();
            for (int v = 0; v < count; v++) lines.Add(LineEntry(data, columns, v, rowMax, colMax));
            result["axis"] = columns ? "columns" : "rows";
            result["lines"] = lines;
            NoteRefreshed(columns);
            return;
        }

        bool isColumn = hasColumn;
        int max = isColumn ? colMax : rowMax;
        if (!TryResolveLine(isColumn ? args.column : args.row, isColumn, 0, max, result, out int vis)) return;

        result["axis"] = isColumn ? "column" : "row";
        foreach (var kv in LineEntry(data, isColumn, vis, rowMax, colMax))
            result[kv.Key] = kv.Value;
    }

    private static Dictionary<string, object> LineEntry(DataSource data, bool column, int visLine,
        int rowMax, int colMax) {
        SheetStats.Summary s = column
            ? SheetStats.Over(data, 0, rowMax, visLine, visLine)
            : SheetStats.Over(data, visLine, visLine, 0, colMax);

        var entry = new Dictionary<string, object> {
            { "title", data.TitleAt(column, visLine) },
            { "position", visLine + 1 }
        };

        if (!s.valid) {
            entry["note"] = "No values in this line.";
            return entry;
        }

        entry["count"] = s.count;
        entry["minimum"] = Round(s.min);
        entry["maximum"] = Round(s.max);
        entry["average"] = Round(s.mean);
        entry["sum"] = Round(s.sum);

        entry["minAt"] = data.TitleAt(!column, column ? s.minVisRow : s.minVisCol);
        entry["maxAt"] = data.TitleAt(!column, column ? s.maxVisRow : s.maxVisCol);
        return entry;
    }
}
