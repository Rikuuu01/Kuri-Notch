namespace NotchWin.Services
{
    public enum AutostartMode
    {
        Disabled,
        LogonTask,
        RunKey
    }

    public interface IAutostartService
    {
        AutostartMode GetAutostartMode();
        AutostartMode SetAutostart(bool enable);
    }
}
