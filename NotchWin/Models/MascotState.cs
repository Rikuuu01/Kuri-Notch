namespace NotchWin.Models
{
    /// <summary>
    /// Keadaan dasar: Idle, Music, Sleep.
    /// Reaksi/peringatan (sementara, bisa menyela keadaan dasar): Happy ... Screenshot.
    /// </summary>
    public enum MascotState
    {
        Idle,
        Music,
        Sleep,

        Happy,
        Notification,
        Alarm,
        Drink,
        Timer,
        Adzan,
        Bluetooth,
        Copied,
        Screenshot,

        Welcome
    }
}
