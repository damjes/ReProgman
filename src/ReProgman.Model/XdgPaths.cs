namespace ReProgman.Model;

/// <summary>Resolves user and system locations defined by the XDG Base Directory specification.</summary>
public static class XdgPaths
{
    public static string ConfigHome => GetDirectory("XDG_CONFIG_HOME", ".config");
    public static string DataHome => GetDirectory("XDG_DATA_HOME", ".local/share");

    public static string[] ApplicationRoots
    {
        get
        {
            var roots = new List<string> { Path.Combine(DataHome, "applications") };
            var dataDirs = Environment.GetEnvironmentVariable("XDG_DATA_DIRS");
            if (string.IsNullOrWhiteSpace(dataDirs)) dataDirs = "/usr/local/share:/usr/share";
            roots.AddRange(dataDirs.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(path => Path.Combine(path, "applications")));
            return roots.Distinct(StringComparer.Ordinal).ToArray();
        }
    }

    private static string GetDirectory(string variable, string fallback)
    {
        var configured = Environment.GetEnvironmentVariable(variable);
        if (!string.IsNullOrWhiteSpace(configured) && Path.IsPathRooted(configured)) return configured;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), fallback);
    }
}
