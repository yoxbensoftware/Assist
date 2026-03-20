namespace Assist.Services;

internal static class IconHelper
{
    public static Bitmap GetBitmap(string name)
    {
        var icon = name.ToLowerInvariant() switch
        {
            "app" or "assist" or "main"          => SystemIcons.Shield,
            "passwords" or "lock" or "shield"    => SystemIcons.Shield,
            "wiggle" or "info"                   => SystemIcons.Information,
            "system" or "sysinfo"                => SystemIcons.Information,
            "news" or "feed" or "application"    => SystemIcons.Application,
            "dns" or "network" or "warning"      => SystemIcons.Warning,
            _                                    => SystemIcons.Application,
        };
        return icon.ToBitmap();
    }

    public static Icon AppIcon => SystemIcons.Shield;
}
