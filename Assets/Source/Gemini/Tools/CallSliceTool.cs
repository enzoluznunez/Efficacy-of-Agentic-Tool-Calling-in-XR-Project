using System.Collections.Generic;
using Google.GenAI.Types;

public sealed class CallSliceTool : AgenticTool<CallSliceTool.Args> {

    public class Args {
        [Doc("Cut just after this column/row of the target piece: its name, or its 1-based position."), Optional]
        public string after;
        [Doc("Make several cuts in one go: each a name or 1-based position to cut after. Use this instead of calling repeatedly, because every cut renumbers the pieces."), Optional]
        public string[] cuts;
[Doc("Whether this works on columns or rows. Leave it out when the name you gave already says which."), Values("columns", "rows"), Optional]
        public string axis;
        [Doc("Target piece."), Optional]
        public int? sheet;
    }

    protected override bool EditsAreOutcome => true;

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "CallSliceTool",
        Description = "Cut a sheet piece, as if the user touched the cut line. Pass 'axis' when the name you give does " +
                      "not say which. On columns, after=N cuts between column N and N+1; on rows, between row N and " +
                      "N+1. Names work as well as numbers. " +
                      "Use 'cuts' to make several cuts at once and never call this " +
                      "repeatedly for one request: each cut renumbers the pieces, so a second call would be aiming at a " +
                      "layout that no longer exists. " +
                      "The result lists the resulting pieces in order along the axis.",
        Parameters = ParametersFor(typeof(Args))
    };

    protected override void Run(Args args, Dictionary<string, object> result) {
        if (!EnsureToolSelected(ToolType.Slice, result)) return;

        var slice = Scene.Slice;
        if (slice == null) { result["error"] = "Slice tool not found in scene."; return; }

        bool many = args.cuts != null && args.cuts.Length > 0;
        if (many && !string.IsNullOrEmpty(args.after)) {
            result["error"] = "Give either 'after' for one cut or 'cuts' for several, not both.";
            return;
        }
        if (!many && string.IsNullOrEmpty(args.after)) {
            result["error"] = "Give 'after' to cut once, or 'cuts' to make several cuts at once.";
            return;
        }

        string hint = many ? args.cuts[0] : args.after;
        if (!EnsureAxisArmed(slice, "slice", args.axis, hint, result, out bool columns)) return;
        string axis = columns ? "columns" : "rows";

        if (!TryResolvePiece(args.sheet, result, "slice", out var mgr, out var sheet, out int pieceId)) return;

        int lineMin = columns ? sheet.colMin : sheet.rowMin;
        int lineMax = columns ? sheet.colMax : sheet.rowMax;
        int span = lineMax - lineMin + 1;
        if (span < 2) {
            result["error"] = $"That piece has only one {(columns ? "column" : "row")}; it cannot be sliced that way.";
            return;
        }

        var lines = new List<int>();
        if (many) {
            if (!TryResolveLines(args.cuts, columns, lineMin, lineMax, result, out List<int> resolved)) return;
            lines.AddRange(resolved);
        }
        else {
            if (!TryResolveLine(args.after, columns, lineMin, lineMax, result, out int one)) return;
            lines.Add(one);
        }

        string what = columns ? "column" : "row";
        foreach (int line in lines)
            if (line - lineMin + 1 > span - 1) {
                result["error"] = $"Cannot cut after the last {what} of that piece.";
                return;
            }

        lines.Sort();

        var madeAt = new List<object>();
        var pieces = new List<object>();
        CreateSheet target = sheet;

        bool ok = RunGrouped(lines.Count, i => {
            if (target == null) return true;
            if (!slice.CutAt(target, lines[i], out SliceRecord record)) {
                result["error"] = madeAt.Count == 0
                    ? "The cut could not be made there."
                    : $"Made {madeAt.Count} cut(s), then one could not be made; the sheet is part-way through.";
                if (madeAt.Count > 0) result["cutsMade"] = madeAt;
                return false;
            }
            madeAt.Add(lines[i] - lineMin + 1);
            pieces.Add(record.aId);
            target = mgr.SheetById(record.bId);
            return true;
        });
        if (!ok) return;

        if (target != null) pieces.Add(target.sheetId);

        result["sliced"] = axis;
        result["sheet"] = pieceId;
        result["cutsMade"] = madeAt;
        result["sheets"] = pieces;
        result["pieceCount"] = mgr.Sheets.Count;
        result["note"] = $"Pieces run in {axis} order: {string.Join(", ", pieces)}.";
    }
}
