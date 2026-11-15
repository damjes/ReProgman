using System.Diagnostics;
using ReProgman.Model;

namespace ReProgman.Services;

/// <summary>Launches XDG application entries without passing their Exec command through a shell.</summary>
public static class XdgDesktopLauncher
{
    public static void Launch(string desktopFile)
    {
        if (!DesktopApplication.TryRead(desktopFile, out var app))
            throw new InvalidOperationException("The desktop entry is missing or cannot be launched.");

        var command = Tokenize(app.Exec);
        if (command.Count == 0) throw new InvalidOperationException("The desktop entry has an empty Exec command.");
        var start = new ProcessStartInfo { FileName = command[0], UseShellExecute = false };
        foreach (var argument in Expand(command.Skip(1), app)) start.ArgumentList.Add(argument);

        if (app.Terminal)
        {
            var terminal = FindTerminal();
            if (terminal is null) throw new InvalidOperationException("This application requires a terminal, but no supported terminal emulator was found.");
            var wrapped = new ProcessStartInfo { FileName = terminal.Value.Command, UseShellExecute = false };
            foreach (var argument in terminal.Value.Prefix) wrapped.ArgumentList.Add(argument);
            wrapped.ArgumentList.Add(start.FileName);
            foreach (var argument in start.ArgumentList) wrapped.ArgumentList.Add(argument);
            start = wrapped;
        }

        Process.Start(start)?.Dispose();
    }

    private static List<string> Tokenize(string command)
    {
        var result = new List<string>();
        var token = new System.Text.StringBuilder();
        var quoted = false;
        var escaped = false;
        var started = false;
        foreach (var c in command)
        {
            if (escaped)
            {
                token.Append(c);
                escaped = false;
                started = true;
            }
            else if (c == '\\')
            {
                escaped = true;
                started = true;
            }
            else if (c == '"')
            {
                quoted = !quoted;
                started = true;
            }
            else if (char.IsWhiteSpace(c) && !quoted)
            {
                if (started)
                {
                    result.Add(token.ToString());
                    token.Clear();
                    started = false;
                }
            }
            else
            {
                token.Append(c);
                started = true;
            }
        }
        if (escaped || quoted) throw new FormatException("The desktop entry contains an invalid Exec command.");
        if (started) result.Add(token.ToString());
        return result;
    }

    private static IEnumerable<string> Expand(IEnumerable<string> arguments, DesktopApplication app)
    {
        foreach (var argument in arguments)
        {
            if (argument == "%i")
            {
                if (app.Icon.Length > 0)
                {
                    yield return "--icon";
                    yield return app.Icon;
                }
                continue;
            }
            // ReProgman launches an application without a document. Document and
            // URL placeholders therefore have no value and their entire argument is omitted.
            if (argument.Contains("%f", StringComparison.Ordinal) || argument.Contains("%F", StringComparison.Ordinal) ||
                argument.Contains("%u", StringComparison.Ordinal) || argument.Contains("%U", StringComparison.Ordinal)) continue;

            var expanded = argument.Replace("%c", app.Name, StringComparison.Ordinal)
                .Replace("%k", app.Path, StringComparison.Ordinal)
                .Replace("%%", "%", StringComparison.Ordinal);
            // Deprecated desktop-entry field codes are ignored, as required by the spec.
            expanded = expanded.Replace("%d", "", StringComparison.Ordinal).Replace("%D", "", StringComparison.Ordinal)
                .Replace("%n", "", StringComparison.Ordinal).Replace("%N", "", StringComparison.Ordinal)
                .Replace("%v", "", StringComparison.Ordinal).Replace("%m", "", StringComparison.Ordinal);
            yield return expanded;
        }
    }

    private static (string Command, string[] Prefix)? FindTerminal()
    {
        (string Command, string[] Prefix)[] choices =
        [
            ("x-terminal-emulator", ["-e"]),
            ("konsole", ["-e"]),
            ("gnome-terminal", ["--"]),
            ("xfce4-terminal", ["--disable-server", "--execute"]),
            ("xterm", ["-e"]),
        ];
        foreach (var choice in choices)
        {
            if (ExecutableExists(choice.Command)) return choice;
        }
        return null;
    }

    private static bool ExecutableExists(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        return path.Split(Path.PathSeparator).Any(directory => File.Exists(Path.Combine(directory, name)));
    }
}
