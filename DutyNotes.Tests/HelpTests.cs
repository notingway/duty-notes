using static DutyNotes.Tests.T;

namespace DutyNotes.Tests;

/// <summary>
/// The Help tab is drawn from <see cref="HelpCommands"/>. These checks keep that list honest: well formed, matching what the plugin
/// actually handles, and matching the README, so a new command can't be added in one place and forgotten in the others.
/// </summary>
internal static class HelpTests
{
    public static void Run()
    {
        var all = HelpCommands.All;

        Check("there are commands to show", all.Count > 0);
        Check("every command starts with the plugin's command", all.All(c => c.Command == HelpCommands.Main || c.Command.StartsWith(HelpCommands.Main + " ")));
        Check("commands are listed once each", all.Select(c => c.Command).Distinct().Count() == all.Count);
        Check("every command says what it does", all.All(c => !string.IsNullOrWhiteSpace(c.What)));
        Check("a suggested macro is the command itself, and has a name", all.Where(c => c.MacroName is not null).All(c => c.Macro == c.Command && c.MacroName!.Length > 0));
        Check("macro names are not repeated", all.Where(c => c.MacroName is not null).Select(c => c.MacroName).Distinct().Count() == all.Count(c => c.MacroName is not null));
        Check("the commands worth binding have a macro",
              new[] { "/dutynotes next", "/dutynotes prev", "/dutynotes reset" }.All(name => all.Any(c => c.Command == name && c.Macro is not null)));
        Check("every macro suggests an icon, a different one each", all.Where(c => c.Macro is not null).All(c => c.MacroIcon != 0)
              && all.Where(c => c.Macro is not null).Select(c => c.MacroIcon).Distinct().Count() == all.Count(c => c.Macro is not null));
        Check("a command with no macro has no icon", all.Where(c => c.Macro is null).All(c => c.MacroIcon == 0));
        Check("next is the right arrow, prev the left arrow and reset the loop",
              all.First(c => c.Command == "/dutynotes next").MacroIcon == MacroIcons.RightArrow
              && all.First(c => c.Command == "/dutynotes prev").MacroIcon == MacroIcons.LeftArrow
              && all.First(c => c.Command == "/dutynotes reset").MacroIcon == MacroIcons.Loop);
        Check("only next, prev and reset have a macro",
              all.Where(c => c.Macro is not null).Select(c => c.Command).OrderBy(x => x).SequenceEqual(new[] { "/dutynotes next", "/dutynotes prev", "/dutynotes reset" }));
        Check("commands with no macro have none to copy", all.Where(c => c.MacroName is null).All(c => c.Macro is null));

        // The files next to the tests: found by walking up from the build folder (skipped if the tests run somewhere else).
        var root = FindRoot();
        if (root is null) return;

        var plugin = File.ReadAllText(Path.Combine(root, "DutyNotes", "Plugin.cs"));
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));

        foreach (var command in all.Where(c => c.Command != HelpCommands.Main))
        {
            var word = command.Command[(HelpCommands.Main.Length + 1)..];
            Check($"the plugin handles \"{command.Command}\"", plugin.Contains($"case \"{word}\":"));
        }

        Check("the plugin registers the command", plugin.Contains("\"" + HelpCommands.Main + "\""));
        foreach (var command in all)
            Check($"the README lists \"{command.Command}\"", readme.Contains($"`{command.Command}`"));
    }

    private static string? FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "README.md"))) dir = dir.Parent;
        return dir?.FullName;
    }
}
