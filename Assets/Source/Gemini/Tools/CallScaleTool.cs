using System.Collections.Generic;
using Google.GenAI.Types;
using UnityEngine;
using Type = Google.GenAI.Types.Type;

public sealed class CallScaleTool : AgenticTool<CallScaleTool.Args> {

    private const float DefaultPercent = 50f;
    private const float MinPercent = 1f;
    private const float MaxPercent = 100f;
    private const float MinScale = 0.01f;
    private const float MaxScale = 2f;

    public class Args {
        [Doc("'enlarge' makes the piece bigger, 'shrink' makes it smaller.")]
        [Values("enlarge", "shrink")]
        public string direction;
        [Doc("How much to resize, as a percentage of the current size. Shrink by 42 leaves it at 58 percent; enlarge by 42 leaves it at 142 percent. Enlarge by 100 doubles it.")]
        [Limits(1, 100), DefaultsTo(50), Optional]
        public float? percent;
        [Doc("Target piece."), Optional]
        public int? sheet;
        [Doc("Resize several pieces the same way in one go: their sheet ids. Use this instead of calling repeatedly."), Optional]
        public int[] sheets;
    }

    protected override bool EditsAreOutcome => true;

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "CallScaleTool",
        Description = "Resize a sheet piece, as if the user grabbed it with both hands and moved them apart to enlarge " +
                      "or together to shrink. A piece never goes above twice its built size, and never shrinks below a " +
                      "hundredth of it, the same limits the user's hands have, so 'scale' in the result is the " +
                      "size it actually reached.",
        Parameters = ParametersFor(typeof(Args))
    };

    protected override void Run(Args args, Dictionary<string, object> result) {
        if (!EnsureToolSelected(ToolType.Scale, result)) return;

        string direction = args.direction.Trim().ToLowerInvariant();
        bool enlarge = direction == "enlarge";

        float percent = Mathf.Clamp(args.percent ?? DefaultPercent, MinPercent, MaxPercent);
        float mult = enlarge ? 1f + percent / 100f : 1f - percent / 100f;

        System.Action<Transform> resize = t => {
            float v = Mathf.Clamp(t.localScale.x * mult, MinScale, MaxScale);
            t.localScale = new Vector3(v, v, v);
        };

        if (args.sheets != null && args.sheets.Length > 0) {
            var all = Scene.Sheets;
            if (ForEachPiece(args.sheets, result, "resize", (piece, step) => {
                    ApplyPieceTransform(all, piece, resize);
                    return true;
                })) {
                result["resized"] = direction;
                result["percent"] = System.Math.Round(percent, 1);
            }
            return;
        }

        if (!TryResolvePiece(args.sheet, result, "resize", out var mgr, out var sheet, out int pieceId)) return;

        var pre = ApplyPieceTransform(mgr, sheet, resize);

        float before = pre.scale.x;
        mgr.GetCommittedPose(sheet, out _, out _, out Vector3 nowScale);
        float after = nowScale.x;

        result["resized"] = direction;
        result["percent"] = System.Math.Round(percent, 1);
        result["scaleBefore"] = System.Math.Round(before, 3);
        result["scale"] = System.Math.Round(after, 3);
        result["sheet"] = pieceId;
        if (Mathf.Abs(after - before * mult) > 1e-3f)
            result["note"] = "The piece reached its size limit and changed less than asked.";
    }
}
