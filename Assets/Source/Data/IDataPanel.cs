public interface IDataPanel
{
    bool IsVisible { get; }
    bool IsCollapsed { get; }
    bool IsExpanded { get; }
    bool CanExpand { get; }
    int ActiveSheetId { get; }

    void ShowPanel();
    void HidePanel();
    void TogglePanel();
    void SetExpanded(bool on);

    void ShowDataset(int index);
    void CollapseData();
    void ShowSheet(int sheetId);

    void Rebind(DataSource source);
}
