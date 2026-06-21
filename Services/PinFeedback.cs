using System.Media;

namespace InScreenApp.Services;

internal static class PinFeedback
{
    public static void PlayPinSound() => SystemSounds.Asterisk.Play();

    public static void PlayUnpinSound() => SystemSounds.Exclamation.Play();
}
