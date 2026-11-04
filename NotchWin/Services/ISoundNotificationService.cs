namespace NotchWin.Services
{
    public interface ISoundNotificationService
    {
        int PrayerCallDurationMs { get; }
        void PlayTimerAlarm();
        void PlayWaterReminder();
        void PlayPrayerCall();
        void PlayStartupSound();
    }
}
