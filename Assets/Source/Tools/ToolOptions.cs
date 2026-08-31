using System;
using System.Collections.Generic;
using UnityEngine.Events;

public abstract class ToolOptions : Tool
{
    protected static readonly string[] Axes = { "columns", "rows" };

    private int _selected = -1;
    private ButtonList _group;

    protected int Selected => _selected;
    protected bool HasOption => _selected >= 0;

    public abstract IReadOnlyList<string> Options { get; }
    public abstract string OptionNoun { get; }

    protected abstract ButtonList BuildOptions();
    protected virtual void OnOptionChanged() { }

    public string CurrentOptionName =>
        _selected >= 0 && _selected < Options.Count ? Options[_selected] : "none";

    public bool TryGetAxis(out bool columns)
    {
        columns = false;
        if (!HasOption) return false;

        string name = CurrentOptionName;
        if (name == Axes[0]) { columns = true; return true; }
        return name == Axes[1];
    }

    protected ButtonList BuildToggleRow()
    {
        if (toolPanelUI == null) return null;

        IReadOnlyList<string> options = Options;
        string prefix = Kind.ToString();
        var buttons = new (string, string, UnityAction)[options.Count];

        for (int i = 0; i < options.Count; i++)
        {
            string option = options[i];
            string label = char.ToUpperInvariant(option[0]) + option.Substring(1);
            buttons[i] = (prefix + label, label, () => SetOption(option));
        }

        return toolPanelUI.AddToggleRow(Kind, prefix + "Options", buttons);
    }

    protected override void BuildPanelContent()
    {
        _group = BuildOptions();
        ApplyVisual();
    }

    protected override void ClearToolState() => ClearOption(false);

    public bool SetOption(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;

        string target = name.Trim();
        if (string.Equals(target, "none", StringComparison.OrdinalIgnoreCase)) { ClearOption(); return true; }

        IReadOnlyList<string> options = Options;
        for (int i = 0; i < options.Count; i++)
        {
            if (!string.Equals(options[i], target, StringComparison.OrdinalIgnoreCase)) continue;
            Select(i);
            return true;
        }
        return false;
    }

    protected void Select(int index)
    {
        if (index < 0 || index >= Options.Count || _selected == index) return;
        _selected = index;
        ApplyVisual();
        OnOptionChanged();
        StateChannel.RecordState("option", $"the {OptionNoun} is {CurrentOptionName}");
    }

    protected void ClearOption(bool announce = true)
    {
        if (_selected < 0) return;
        _selected = -1;
        ApplyVisual();
        OnOptionChanged();
        string what = $"no {OptionNoun} is chosen";
        if (announce) StateChannel.RecordState("option", what);
        else StateChannel.SetState("option", what);
    }

    private void ApplyVisual() => _group?.SetSelected(_selected);
}
