namespace NotchWin.Models
{
    /// <summary>
    /// Visual style options for the interactive notch pet companion.
    /// Features two pure-vector premium mascots: Pastel Mochi & Astro Mascot.
    /// </summary>
    public enum PetStyle
    {
        // Nilai numerik dipertahankan (1, 2, 3) agar settings.json lama tetap valid;
        // nilai 0 (gaya maskot lama yang dihapus) dinormalisasi ke PastelMochi saat load.
        PastelMochi = 1,    // Pastel Mochi (Lavender Periwinkle, MascotControl.xaml)
        AstroMascot = 2,    // Astro Mascot (White space suit, glowing visor, AstroMascotControl.xaml)
        EggMascot = 3       // Egg Mascot (White egg with waving arm, EggMascotControl.xaml)
    }
}
