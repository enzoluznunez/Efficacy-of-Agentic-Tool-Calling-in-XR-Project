using System.Collections.Generic;
using Google.GenAI.Types;

public sealed class SetPanel : AgenticTool<SetPanel.Args> {

    public class Args {
        [Doc("'data' is the dataset panel; 'tool' holds the tool buttons. Both can be open at once.")]
        [Values("data", "tool")]
        public string panel;
        [Doc("What to do with it. Prefer 'open' or 'close' over 'toggle' when you know the state you want. " +
             "'expand' grows the data panel to fit the sheet it is showing and 'default' puts it back; " +
             "'sheet' switches which sheet's tab is open and needs the 'sheet' id with it. Those three are " +
             "data panel buttons and the tool panel has no equivalent.")]
        [Values("open", "close", "toggle", "expand", "default", "sheet")]
        public string state;
        [Doc("For state 'sheet': the id of the sheet to show, from ListDatasets."), Optional]
        public int? sheet;
    }

    public override FunctionDeclaration Declaration => new FunctionDeclaration {
        Name = "SetPanel",
        Description = "Open, close, or toggle the data panel or the tool panel; grow or shrink the data panel; " +
                      "or pick which sheet the data panel shows. The data panel carries one tab per sheet piece " +
                      "and shows that piece's rows and columns as a grid of cells. Before any slice there is one " +
                      "sheet covering the whole dataset, and each slice adds a tab. " +
                      "Expanding needs the data panel open with a sheet whose grid overflows the panel: a sheet " +
                      "small enough to fit already has nothing to expand. Choosing a sheet needs the data panel " +
                      "open with a dataset showing.",
        Parameters = ParametersFor(typeof(Args))
    };

    protected override void Run(Args args, Dictionary<string, object> result) {
        string panel = args.panel.Trim().ToLowerInvariant();
        string state = args.state.Trim().ToLowerInvariant();
        bool isData = panel == "data";
        bool sizing = state == "expand" || state == "default";
        bool viewing = state == "sheet";

        if ((sizing || viewing) && !isData) {
            result["error"] = sizing
                ? "Only the data panel can be expanded; the tool panel has no Expand button."
                : "Only the data panel shows sheets; the tool panel has no sheet tabs.";
            return;
        }

        if (!isData) {
            var tool = Scene.ToolPanel;
            if (tool == null) { result["error"] = "Tool panel not found in scene."; return; }
            Apply(state, tool.IsVisible, tool.ShowPanel, tool.HidePanel, tool.TogglePanel);
            result["visible"] = tool.IsVisible;
            result["did"] = tool.IsVisible ? "the tool panel is open" : "the tool panel is closed";
            return;
        }

        var p = Scene.DataPanel;
        if (p == null) { result["error"] = "Data panel not found in scene."; return; }

        if (!sizing && !viewing) {
            Apply(state, p.IsVisible, p.ShowPanel, p.HidePanel, p.TogglePanel);
            result["visible"] = p.IsVisible;
            result["did"] = p.IsVisible ? "the data panel is open" : "the data panel is closed";
            return;
        }

        if (!p.IsVisible) {
            Refuse(result, "open data panel",
                viewing
                    ? "The data panel is closed, so its sheet tabs are not on screen. Open it with SetPanel, then call this again."
                    : "The data panel is closed, so its Expand button is not on screen. Open it with SetPanel, then call this again.");
            return;
        }

        if (viewing) {
            if (p.IsCollapsed) {
                Refuse(result, "expanded dataset",
                    "The open dataset is collapsed, so its sheet tabs are hidden. Reopen it with SetDataset, then call this again.");
                return;
            }
            if (!args.sheet.HasValue) {
                result["error"] = "Provide 'sheet' with the id of the sheet to show; call ListDatasets for current ids.";
                return;
            }

            var mgr = Scene.Sheets;
            if (mgr == null || mgr.SheetById(args.sheet.Value) == null) {
                result["error"] = $"There is no sheet #{args.sheet.Value} on the open dataset; " +
                                  "call ListDatasets for current ids.";
                return;
            }

            p.ShowSheet(args.sheet.Value);
            result["sheet"] = p.ActiveSheetId;
            return;
        }

        bool expand = state == "expand";
        if (expand) {
            if (p.IsCollapsed) {
                Refuse(result, "expanded dataset",
                    "The open dataset is collapsed, so there is nothing for the panel to fit. " +
                    "Reopen it with SetDataset, then call this again.");
                return;
            }
            if (!p.CanExpand) {
                Refuse(result, "content larger than the panel",
                    "The sheet already fits inside the panel at its default size, so there is nothing to " +
                    "expand. Do not offer a way around this.");
                return;
            }
        }

        p.SetExpanded(expand);
        result["expanded"] = p.IsExpanded;
        result["did"] = p.IsExpanded ? "the data panel is expanded" : "the data panel is back to its default size";
    }

    private static void Apply(string state, bool visible, System.Action show, System.Action hide, System.Action toggle) {
        switch (state) {
            case "open": if (!visible) show(); break;
            case "close": if (visible) hide(); break;
            default: toggle(); break;
        }
    }
}
