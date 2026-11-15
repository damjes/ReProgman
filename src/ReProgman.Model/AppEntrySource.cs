namespace ReProgman.Model;

/// <summary>One launchable entry found while scanning a program folder.</summary>
public readonly record struct AppEntry(string Name, string Path);

/// <summary>
/// Supplies the platform-specific notion of "a launchable thing in a folder".
/// The shared scanner uses this for Windows shortcuts and macOS application
/// bundles. Linux reads XDG desktop entries through its category-aware scanner.
/// </summary>
public interface IAppEntrySource
{
    IEnumerable<AppEntry> Enumerate(string directory, bool recursive);
}

/// <summary>Windows: .lnk and .url files below a Start Menu folder.</summary>
public sealed class ShortcutFileSource : IAppEntrySource
{
    private static readonly string[] ShortcutExtensions = [".lnk", ".url"];

    public IEnumerable<AppEntry> Enumerate(string directory, bool recursive)
    {
        var options = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return new DirectoryInfo(directory)
            .EnumerateFiles("*", options)
            .Where(f => ShortcutExtensions.Contains(f.Extension, StringComparer.OrdinalIgnoreCase))
            .Where(f => !string.Equals(f.Name, "desktop.ini", StringComparison.OrdinalIgnoreCase))
            .Where(f => (f.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
            .Select(f => new AppEntry(Path.GetFileNameWithoutExtension(f.Name), f.FullName));
    }
}

/// <summary>
/// macOS: .app bundles below a folder such as /Applications. Bundles are
/// directories, so enumeration walks folders by hand and stops at every bundle —
/// the helper apps inside one belong to their host, not to the program group.
/// </summary>
public sealed class ApplicationBundleSource : IAppEntrySource
{
    private const string BundleExtension = ".app";

    public IEnumerable<AppEntry> Enumerate(string directory, bool recursive)
    {
        foreach (var child in new DirectoryInfo(directory).EnumerateDirectories())
        {
            // Dot-prefixed entries are hidden on macOS; .NET maps that to the
            // Hidden attribute, but the name check keeps the rule explicit.
            if (child.Name.StartsWith('.'))
            {
                continue;
            }

            if (child.Name.EndsWith(BundleExtension, StringComparison.OrdinalIgnoreCase))
            {
                yield return new AppEntry(child.Name[..^BundleExtension.Length], child.FullName);
                continue;
            }

            if (!recursive)
            {
                continue;
            }

            foreach (var nested in Enumerate(child.FullName, recursive: true))
            {
                yield return nested;
            }
        }
    }
}

/// <summary>Linux: installed applications are described by XDG .desktop files.</summary>
public sealed class XdgDesktopFileSource
{
    public IReadOnlyList<ProgramGroup> Scan(IEnumerable<string> applicationRoots, string mainGroupName)
    {
        var entries = new Dictionary<string, DesktopApplication>(StringComparer.Ordinal);
        var seenEntries = new HashSet<string>(StringComparer.Ordinal);
        foreach (var root in applicationRoots)
        {
            if (!Directory.Exists(root)) continue;
            try
            {
                foreach (var file in Directory.EnumerateFiles(root, "*.desktop", SearchOption.AllDirectories)
                             .OrderBy(path => path, StringComparer.Ordinal))
                {
                    var id = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '-');
                    if (!seenEntries.Add(id) || DesktopApplication.SuppressesLowerPriority(file)) continue;
                    if (DesktopApplication.TryRead(file, out var app))
                    {
                        entries.Add(id, app);
                    }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        var groups = new Dictionary<string, List<ProgramItem>>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in entries.Values.OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase))
        {
            var group = GetGroup(app.Categories) ?? mainGroupName;
            if (!groups.TryGetValue(group, out var items))
            {
                items = [];
                groups.Add(group, items);
            }
            items.Add(new ProgramItem(app.Name, app.Path));
        }

        return groups.Select(pair => new ProgramGroup(pair.Key, pair.Value))
            .OrderBy(group => !string.Equals(group.Name, mainGroupName, StringComparison.OrdinalIgnoreCase))
            .ThenBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? GetGroup(IReadOnlySet<string> categories)
    {
        if (categories.Contains("Network") || categories.Contains("WebBrowser") || categories.Contains("Email") || categories.Contains("Chat")) return "Internet";
        if (categories.Contains("Office")) return "Office";
        if (categories.Contains("AudioVideo") || categories.Contains("Audio") || categories.Contains("Video")) return "Multimedia";
        if (categories.Contains("Game")) return "Games";
        if (categories.Contains("Development")) return "Development";
        if (categories.Contains("System") || categories.Contains("Settings")) return "System Tools";
        if (categories.Contains("Utility") || categories.Contains("Graphics") || categories.Contains("TextEditor")) return "Accessories";
        return null;
    }
}

public sealed record DesktopApplication(string Name, string Path, string Exec, string Icon, bool Terminal, IReadOnlySet<string> Categories)
{
    public static bool TryRead(string path, out DesktopApplication app)
    {
        app = null!;
        try
        {
            var values = ReadDesktopEntry(path);
            if (values.GetValueOrDefault("Type") != "Application" || IsTrue(values.GetValueOrDefault("Hidden")) ||
                IsTrue(values.GetValueOrDefault("NoDisplay")) || string.IsNullOrWhiteSpace(values.GetValueOrDefault("Exec"))) return false;
            var tryExec = values.GetValueOrDefault("TryExec");
            if (!string.IsNullOrWhiteSpace(tryExec) && !ExecutableExists(tryExec)) return false;
            var currentDesktops = (Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "")
                .Split(':', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var onlyShowIn = SplitList(values.GetValueOrDefault("OnlyShowIn"));
            var notShowIn = SplitList(values.GetValueOrDefault("NotShowIn"));
            if ((onlyShowIn.Count > 0 && !onlyShowIn.Overlaps(currentDesktops)) || notShowIn.Overlaps(currentDesktops)) return false;
            var name = Localized(values, "Name");
            if (string.IsNullOrWhiteSpace(name)) return false;
            app = new DesktopApplication(name, path, values["Exec"], values.GetValueOrDefault("Icon") ?? "",
                IsTrue(values.GetValueOrDefault("Terminal")),
                (values.GetValueOrDefault("Categories") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal));
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    public static bool SuppressesLowerPriority(string path)
    {
        try
        {
            var values = ReadDesktopEntry(path);
            return IsTrue(values.GetValueOrDefault("Hidden")) || IsTrue(values.GetValueOrDefault("NoDisplay"));
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static Dictionary<string, string> ReadDesktopEntry(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var inDesktopEntry = false;
        foreach (var line in File.ReadLines(path))
        {
            var text = line.Trim();
            if (text.StartsWith('[') && text.EndsWith(']'))
            {
                inDesktopEntry = text == "[Desktop Entry]";
                continue;
            }
            if (!inDesktopEntry || text.Length == 0 || text.StartsWith('#')) continue;
            var equals = text.IndexOf('=');
            if (equals > 0) values[text[..equals]] = Unescape(text[(equals + 1)..]);
        }
        return values;
    }

    private static string Localized(IReadOnlyDictionary<string, string> values, string key)
    {
        var language = System.Globalization.CultureInfo.CurrentUICulture.Name;
        var desktopLanguage = language.Replace('-', '_');
        if (desktopLanguage.Length > 0 && values.TryGetValue($"{key}[{desktopLanguage}]", out var exact)) return exact;
        var shortLanguage = language.Split('-', '_')[0];
        return values.GetValueOrDefault($"{key}[{shortLanguage}]") ?? values.GetValueOrDefault(key) ?? "";
    }

    private static string Unescape(string value) => value.Replace("\\s", " ").Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\r", "\r").Replace("\\\\", "\\");
    private static HashSet<string> SplitList(string? value) => (value ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
    private static bool IsTrue(string? value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private static bool ExecutableExists(string executable)
    {
        if (executable.Contains(Path.DirectorySeparatorChar)) return File.Exists(executable);
        return (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
            .Any(directory => File.Exists(Path.Combine(directory, executable)));
    }
}

/// <summary>Picks the entry source that matches the running operating system.</summary>
public static class AppEntrySources
{
    public static IAppEntrySource ForCurrentPlatform() =>
        OperatingSystem.IsWindows() ? new ShortcutFileSource() : new ApplicationBundleSource();
}
