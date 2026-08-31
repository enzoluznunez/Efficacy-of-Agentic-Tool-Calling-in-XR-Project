using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

public class ButtonList
{
    public enum Axis { Horizontal, Vertical }
    public enum Sizing { Measured, Equal, Square }

    public class Options
    {
        public Axis axis = Axis.Horizontal;
        public Sizing sizing = Sizing.Measured;
        public RectOffset padding;
        public TextAnchor alignment = TextAnchor.UpperLeft;
        public bool expandCrossAxis = true;
        public float itemHeight;
        public float itemPadding = Style.ButtonTextPad;
        public bool newestFirst;
        public float cellSize;
        public int columns = 1;
        public bool backed;
        public bool scrollable;
    }

    private const float EngageBleed = Style.SmallPadding;

    private readonly RectTransform _root;
    private readonly RectTransform _content;
    private readonly Options _options;
    private readonly List<UIButton.Handle> _items = new List<UIButton.Handle>();

    public float MaxItemExtent { get; private set; }

    public ButtonList(RectTransform root, Options options)
    {
        _root = root;
        _options = options ?? new Options();
        _content = _options.scrollable ? BuildScroll() : _root;

        if (_options.sizing == Sizing.Square) BuildGrid();
        else BuildAxisLayout();
    }

    private GridLayoutGroup _grid;

    private RectTransform BuildScroll()
    {
        bool vertical = _options.axis == Axis.Vertical;

        RectMask2D mask = _root.gameObject.AddComponent<RectMask2D>();
        mask.padding = vertical
            ? new Vector4(-EngageBleed, 0f, -EngageBleed, 0f)
            : new Vector4(0f, -EngageBleed, 0f, -EngageBleed);

        Image catcher = _root.gameObject.AddComponent<Image>();
        catcher.color = Color.clear;
        catcher.raycastTarget = true;

        GameObject contentObj = new GameObject("Content", typeof(RectTransform));
        RectTransform content = contentObj.GetComponent<RectTransform>();
        content.SetParent(_root, false);

        content.anchorMin = vertical ? new Vector2(0f, 1f) : new Vector2(0f, 0f);
        content.anchorMax = vertical ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
        content.pivot = vertical ? new Vector2(0f, 1f) : new Vector2(0f, 0.5f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        ContentSizeFitter fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = vertical
            ? ContentSizeFitter.FitMode.Unconstrained
            : ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = vertical
            ? ContentSizeFitter.FitMode.PreferredSize
            : ContentSizeFitter.FitMode.Unconstrained;

        ScrollRect scroll = _root.gameObject.AddComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = _root;
        scroll.horizontal = !vertical;
        scroll.vertical = vertical;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = Style.ScrollSensitivity;
        scroll.horizontalScrollbar = null;
        scroll.verticalScrollbar = null;

        return content;
    }

    public void SetCellSize(float cell)
    {
        if (_grid == null || cell <= 0f) return;
        _grid.cellSize = new Vector2(cell, cell);
    }

    private void BuildGrid()
    {
        GridLayoutGroup glg = _content.gameObject.AddComponent<GridLayoutGroup>();
        _grid = glg;
        glg.cellSize = new Vector2(_options.cellSize, _options.cellSize);
        glg.spacing = new Vector2(Style.SmallPadding, Style.SmallPadding);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = Mathf.Max(1, _options.columns);
        if (_options.padding != null) glg.padding = _options.padding;
    }

    private void BuildAxisLayout()
    {
        HorizontalOrVerticalLayoutGroup lg = _options.axis == Axis.Vertical
            ? (HorizontalOrVerticalLayoutGroup)_content.gameObject.AddComponent<VerticalLayoutGroup>()
            : _content.gameObject.AddComponent<HorizontalLayoutGroup>();

        lg.spacing = Style.SmallPadding;
        if (_options.padding != null) lg.padding = _options.padding;
        lg.childAlignment = _options.alignment;
        lg.childControlWidth = true;
        lg.childControlHeight = true;

        bool expandAlong = _options.sizing == Sizing.Equal;
        if (_options.axis == Axis.Horizontal)
        {
            lg.childForceExpandWidth = expandAlong;
            lg.childForceExpandHeight = _options.expandCrossAxis;
        }
        else
        {
            lg.childForceExpandHeight = expandAlong;
            lg.childForceExpandWidth = _options.expandCrossAxis;
        }
    }

    public UIButton.Handle Add(string name, string label, UnityAction onClick)
    {
        UIButton.Handle h = UIButton.Create(_content, name, label,
            flexibleWidth: _options.sizing == Sizing.Equal,
            height: _options.itemHeight,
            padLeft: _options.itemPadding, padRight: _options.itemPadding);

        if (_options.backed) UIButton.AddBack(h);
        if (_options.sizing == Sizing.Measured) Measure(h);
        if (onClick != null) h.Button.onClick.AddListener(onClick);
        if (_options.newestFirst) h.Root.transform.SetAsFirstSibling();

        _items.Add(h);
        return h;
    }

    public UIButton.Handle AddSwatch(string name, Color fill, UnityAction onClick)
    {
        UIButton.Handle h = UIButton.CreateSwatch(_content, name, fill);
        if (onClick != null) h.Button.onClick.AddListener(onClick);
        _items.Add(h);
        return h;
    }

    public void Remeasure()
    {
        if (_options.sizing != Sizing.Measured) return;

        MaxItemExtent = 0f;
        for (int i = 0; i < _items.Count; i++)
            Measure(_items[i]);
    }

    private void Measure(UIButton.Handle h)
    {
        LayoutElement le = h.Root.GetComponent<LayoutElement>();
        if (le == null) return;

        UIMeasure.TryTextWidth(h.Text, out float pref);
        float extent = (pref > 0f ? pref : 0f) + _options.itemPadding * 2f;

        le.preferredWidth = extent;
        le.flexibleWidth = 0f;
        if (extent > MaxItemExtent) MaxItemExtent = extent;
    }

    public void Clear()
    {
        UILayout.Clear(_content);
        _items.Clear();
        MaxItemExtent = 0f;
    }

    public void SetSelected(int activeIndex)
    {
        for (int i = 0; i < _items.Count; i++)
            UIButton.SetSelected(_items[i], i == activeIndex);
    }
}
