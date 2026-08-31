using UnityEngine;
using UnityEngine.UI;

public class ScrollList
{
    public enum ContentSizing { Fit, Manual }

    public const float BarThickness = Style.SmallPadding;
    public const float BarInset = Style.SmallPadding;
    public const float BarClearance = BarThickness + BarInset;

    public RectTransform Viewport { get; private set; }
    public RectTransform Content { get; private set; }
    public ScrollRect Scroll { get; private set; }

    public ScrollList(RectTransform parent, string name, ContentSizing sizing)
    {
        GameObject viewportObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        Viewport = viewportObj.GetComponent<RectTransform>();
        Viewport.SetParent(parent, false);

        Image viewportFill = viewportObj.GetComponent<Image>();
        viewportFill.color = Color.clear;
        viewportFill.raycastTarget = true;

        LayoutElement viewportLayout = viewportObj.AddComponent<LayoutElement>();
        viewportLayout.flexibleHeight = 1f;

        GameObject contentObj = new GameObject("Content", typeof(RectTransform));
        Content = contentObj.GetComponent<RectTransform>();
        Content.SetParent(Viewport, false);
        Content.anchorMin = Content.anchorMax = new Vector2(0f, 1f);
        Content.pivot = new Vector2(0f, 1f);
        Content.anchoredPosition = Vector2.zero;

        if (sizing == ContentSizing.Fit)
        {
            ContentSizeFitter fitter = contentObj.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        ScrollRect scroll = viewportObj.AddComponent<ScrollRect>();
        Scroll = scroll;
        scroll.content = Content;
        scroll.viewport = Viewport;
        scroll.horizontal = true;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = Style.ScrollSensitivity;

        scroll.verticalScrollbar = Bar(scroll, true);
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        scroll.horizontalScrollbar = Bar(scroll, false);
        scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }

    public void Clear() => UILayout.Clear(Content);

    public void SetVisible(bool on) => Viewport.gameObject.SetActive(on);

    private static Scrollbar Bar(ScrollRect scroll, bool vertical)
    {
        float radius = BarThickness * 0.5f;

        GameObject barObj = new GameObject(vertical ? "VScrollbar" : "HScrollbar");
        barObj.transform.SetParent(scroll.viewport, false);
        RectTransform barRT = barObj.AddComponent<RectTransform>();

        if (vertical)
        {
            barRT.anchorMin = new Vector2(1f, 0f);
            barRT.anchorMax = new Vector2(1f, 1f);
            barRT.pivot = new Vector2(1f, 1f);
            barRT.sizeDelta = new Vector2(BarThickness, -BarClearance);
            barRT.anchoredPosition = new Vector2(-BarInset, 0f);
        }
        else
        {
            barRT.anchorMin = new Vector2(0f, 0f);
            barRT.anchorMax = new Vector2(1f, 0f);
            barRT.pivot = new Vector2(0f, 0f);
            barRT.sizeDelta = new Vector2(-BarClearance, BarThickness);
            barRT.anchoredPosition = new Vector2(0f, BarInset);
        }

        Image track = barObj.AddComponent<Image>();
        RoundedSprite.Apply(track, radius);
        track.color = Style.Alpha(Style.Black, Style.SurfaceTint);

        GameObject slidingArea = new GameObject("SlidingArea");
        slidingArea.transform.SetParent(barObj.transform, false);
        RectTransform slidingRT = slidingArea.AddComponent<RectTransform>();
        slidingRT.anchorMin = Vector2.zero;
        slidingRT.anchorMax = Vector2.one;
        slidingRT.offsetMin = Vector2.zero;
        slidingRT.offsetMax = Vector2.zero;

        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(slidingArea.transform, false);
        RectTransform handleRT = handle.AddComponent<RectTransform>();
        handleRT.sizeDelta = Vector2.zero;

        Image handleImg = handle.AddComponent<Image>();
        RoundedSprite.Apply(handleImg, radius);
        handleImg.color = Style.Alpha(Style.Black, 0.5f);

        Scrollbar bar = barObj.AddComponent<Scrollbar>();
        bar.transition = Selectable.Transition.None;
        bar.direction = vertical ? Scrollbar.Direction.BottomToTop : Scrollbar.Direction.LeftToRight;
        bar.handleRect = handleRT;
        bar.targetGraphic = handleImg;
        return bar;
    }
}
