using System.Collections.Generic;

public abstract class AxisTool : ToolOptions
{
    public override IReadOnlyList<string> Options => Axes;
    public override string OptionNoun => "option";

    protected override ButtonList BuildOptions() => BuildToggleRow();

    protected SliceAxis Axis => Selected == 1 ? SliceAxis.Row : SliceAxis.Column;
}
