namespace NotchWin.Widgets
{
    public interface INotchWidget
    {
        WidgetType Type { get; }
        string Name { get; }
        int Priority { get; }
        bool IsActive { get; }
    }
}
