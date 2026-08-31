using TMPro;
using UnityEngine;

public static class Style
{
    private const string BodyFontPath = "Fonts/Inter-Medium SDF";
    private const string TitleFontPath = "Fonts/Inter-Bold SDF";

    private static TMP_FontAsset _bodyFont;
    private static TMP_FontAsset _titleFont;

    public static TMP_FontAsset BodyFont => Load(ref _bodyFont, BodyFontPath);

    public static TMP_FontAsset TitleFont => Load(ref _titleFont, TitleFontPath);

    public static void ApplyBody(TMP_Text label)
    {
        if (label == null) return;
        if (BodyFont != null) label.font = BodyFont;
        label.fontSize = Body;
    }

    public static void ApplyTitle(TMP_Text label)
    {
        if (label == null) return;
        if (TitleFont != null) label.font = TitleFont;
        label.fontSize = Title;
    }

    private static TMP_FontAsset Load(ref TMP_FontAsset cached, string path)
    {
        if (cached != null) return cached;

        cached = Resources.Load<TMP_FontAsset>(path);
        if (cached == null)
            Debug.LogError($"[Style] No font at Resources/{path}; text keeps whatever font TMP defaults to.");
        return cached;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _bodyFont = null;
        _titleFont = null;
    }

    public static readonly Color White = Hex(255, 255, 255);
    public static readonly Color Black = Hex(000, 000, 000);

    public const float SurfaceTint = 0.05f;

    public const float EngageScale = 1.03f;
    public const float EngageSwell = EngageScale - 1f;
    public const float PreviewSwell = 0.10f;

    public const float SmallBorder = 2f;

    public const float Body = 10f;
    public const float Title = 12f;

    public const float WorldScale = 0.001f;
    public const float WorldTextScale = WorldScale * 10f;

    public const float SmallPadding = 6f;
    public const float MediumPadding = 12f;
    public const float ButtonTextPad = 10f;
    public const float PanelInset = MediumPadding;

    public const float ScrollSensitivity = 24f;
    public const float CellRowHeight = 28f;
    public const float HeaderHeight = 30f;
    public const float TitleColumn = 120f;
    public const float ValueColumn = 90f;

    public const float OutRadius = 8f;
    public const float ChipRadius = 4f;
    public const float PanelRadius = 12f;
    public const float PanelInnerRadius = PanelRadius - SmallBorder;

    public static float RadiusFor(float height) => height <= Subbutton.y ? ChipRadius : OutRadius;

    public static float InnerRadius(float radius) => radius - SmallBorder;

    public static readonly Vector2 Subbutton = new Vector2(40f, 20f);
    public static readonly Vector2 Button = new Vector2(50f, 25f);
    public static readonly Vector2 WatchButton = new Vector2(90f, 45f);
    public const float TitleBarButtonWidth = 80f;

    public const int SortPanels = 0;
    public const int SortHandUI = 0;
    public const int SortNotices = 10;

    public static readonly Vector2 Subpanel = new Vector2(400f, 200f);
    public static readonly Vector2 Panel = new Vector2(400f, 400f);
    public static readonly Vector2 DataPanel = new Vector2(Panel.x, Subpanel.y * 1.5f);
    public const float TooltipWidth = 360f;

    public static Color Alpha(Color c, float a)
    {
        c.a = a;
        return c;
    }

    private static Color Hex(byte r, byte g, byte b) => new Color32(r, g, b, 255);
}
