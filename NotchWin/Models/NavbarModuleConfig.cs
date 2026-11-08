using System;

namespace NotchWin.Models
{
    /// <summary>
    /// Konfigurasi urutan dan status aktif/nonaktif modul pada notch navbar.
    /// </summary>
    public class NavbarModuleConfig
    {
        public string Id { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;

        public NavbarModuleConfig() { }

        public NavbarModuleConfig(string id, bool isEnabled = true)
        {
            Id = id;
            IsEnabled = isEnabled;
        }
    }
}
