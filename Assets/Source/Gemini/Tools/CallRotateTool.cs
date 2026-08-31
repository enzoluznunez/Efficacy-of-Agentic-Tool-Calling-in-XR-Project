using System;
using System.Collections.Generic;
using Google.GenAI.Types;
using UnityEngine;

public sealed class CallRotateTool : AgenticTool<CallRotateTool.Args> {

    private const float DefaultDegrees = 90f;
    private const float MaxDegrees = 3600f;

    public class Args {
        [Doc("'by' turns the piece a set amount, 'face' squares it to the user so its columns run left to right, " +
             "'reset' returns it to the orientation it was built with. Defaults to 'by'."),
         Values("by", "face", "reset"), Optional]
        public string mode;
        [Doc("For 'by': which way to turn the piece, seen from above from the user's point of view; " +
             "'right' is clockwise, 'left' is counterclockwise."), Optional]
        public string direction;
        [Doc("For 'by': how far to turn, in degrees. More than a full turn is fine; it is reduced to the equivalent turn, so 730 becomes 10."), Limits(1, 3600), DefaultsTo(90), Optional]
        public float? degrees;
        [Doc("Target piece."), Optional]
        public int? sheet;
        [Doc("Turn several pieces the same way in one go: their sheet ids. Use this instead of calling repeatedly."), Optional]
        public int[] sheets;
    }

    protected override bool EditsAreOutcome => true;

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "CallRotateTool",
        Description = "Turn a sheet piece about the upright axis, as if the user grabbed it with both hands and " +
                      "twisted. Mode 'by' is the default and also needs 'direction'; modes 'face' (square it to the " +
                      "user) and 'reset' (undo any twisting) take neither 'direction' nor 'degrees'. The piece spins in " +
                      "place; its position is unchanged.",
        Parameters = ParametersFor(typeof(Args))
    };

    protected override void Run(Args args, Dictionary<string, object> result) {
        if (!EnsureToolSelected(ToolType.Rotate, result)) return;

        string mode = string.IsNullOrWhiteSpace(args.mode) ? "by" : args.mode.Trim().ToLowerInvariant();

        if (args.sheets != null && args.sheets.Length > 0) {
            var all = Scene.Sheets;
            if (ForEachPiece(args.sheets, result, "rotate", (piece, step) => Apply(all, piece, mode, args, step)))
                result["rotated"] = mode;
            return;
        }

        if (!TryResolvePiece(args.sheet, result, "rotate", out var mgr, out var sheet, out int pieceId)) return;
        result["sheet"] = pieceId;

        Apply(mgr, sheet, mode, args, result);
    }

    private static bool Apply(ManageSheets mgr, CreateSheet sheet, string mode, Args args, Dictionary<string, object> step) {
        switch (mode) {
            case "by": By(mgr, sheet, args, step); break;
            case "face": Face(mgr, sheet, step); break;
            case "reset":
                ApplyPieceTransform(mgr, sheet, t => t.localRotation = Quaternion.identity);
                step["rotated"] = "reset";
                break;
            default: step["error"] = $"Unknown mode '{mode}'. Use 'by', 'face', or 'reset'."; return false;
        }
        return !IsRefusal(step);
    }

    private static void By(ManageSheets mgr, CreateSheet sheet, Args args, Dictionary<string, object> result) {
        string direction = args.direction?.Trim().ToLowerInvariant();
        float sign;
        if (direction == "right") sign = 1f;
        else if (direction == "left") sign = -1f;
        else {
            NeedChoice(result, "direction", new List<string> { "left", "right" },
                "Which way should it turn, left or right? Mode 'face' or 'reset' needs no direction.");
            return;
        }

        float asked = Mathf.Clamp(Math.Abs(args.degrees ?? DefaultDegrees), 0f, MaxDegrees);
        float degrees = asked % 360f;

        if (asked >= 360f) result["asked"] = Math.Round(asked, 1);

        if (degrees < 0.05f) {
            result["degrees"] = 0;
            result["note"] = asked >= 360f
                ? $"{Math.Round(asked, 1)} degrees is a whole number of full turns, so the piece ends where it started."
                : "That is too small a turn to see.";
            return;
        }

        ApplyPieceTransform(mgr, sheet, t =>
            t.rotation = Quaternion.AngleAxis(sign * degrees, Vector3.up) * t.rotation);

        result["rotated"] = direction;
        result["degrees"] = Math.Round(degrees, 1);
    }

    private static void Face(ManageSheets mgr, CreateSheet sheet, Dictionary<string, object> result) {
        Transform cam = CameraRig.MainTransform;
        if (cam == null) { result["error"] = "The user's viewpoint could not be found."; return; }

        Vector3 forward = CameraRig.Flatten(cam.forward, Vector3.zero);
        if (forward == Vector3.zero) { result["error"] = "The user is looking straight up or down."; return; }

        ApplyPieceTransform(mgr, sheet, t => t.rotation = Quaternion.LookRotation(forward, Vector3.up));
        result["rotated"] = "face";
    }
}
