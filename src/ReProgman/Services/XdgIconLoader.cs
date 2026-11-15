using Avalonia.Media.Imaging;
using ReProgman.Model;

namespace ReProgman.Services;

/// <summary>Loads bitmap icons named by XDG desktop entries from common icon themes.</summary>
public static class XdgIconLoader
{
    private static readonly string[] Themes = ["hicolor", "Adwaita", "breeze", "breeze-dark", "Yaru"];
    private static readonly string[] Sizes = ["32x32", "48x48", "24x24", "22x22", "16x16", "64x64", "128x128", "256x256", "scalable", "symbolic"];

    public static Bitmap? GetLargeIcon(string desktopFile)
    {
        if (!DesktopApplication.TryRead(desktopFile, out var app) || string.IsNullOrWhiteSpace(app.Icon)) return null;
        var icon = app.Icon;
        var roots = new[] { XdgPaths.DataHome }.Concat(
            (Environment.GetEnvironmentVariable("XDG_DATA_DIRS") ?? "/usr/local/share:/usr/share")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)).Distinct(StringComparer.Ordinal);
        foreach (var root in roots)
        {
            if (Path.IsPathRooted(icon))
            {
                var direct = LoadBitmap(icon);
                if (direct is not null) return direct;
                foreach (var extension in new[] { ".png", ".xpm" })
                {
                    direct = LoadBitmap(icon + extension);
                    if (direct is not null) return direct;
                }
                continue;
            }

            foreach (var theme in Themes)
            foreach (var size in Sizes)
            {
                foreach (var extension in new[] { ".png", ".xpm" })
                {
                    var themed = LoadBitmap(Path.Combine(root, "icons", theme, size, "apps", icon + extension));
                    if (themed is not null) return themed;
                }
            }

            foreach (var extension in new[] { ".png", ".xpm" })
            {
                var pixmap = LoadBitmap(Path.Combine(root, "pixmaps", icon + extension));
                if (pixmap is not null) return pixmap;
            }
        }
        return null;
    }

    private static Bitmap? LoadBitmap(string path)
    {
        if (!File.Exists(path) || Path.GetExtension(path).Equals(".xpm", StringComparison.OrdinalIgnoreCase)) return null;
        try { return new Bitmap(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { return null; }
    }
}
