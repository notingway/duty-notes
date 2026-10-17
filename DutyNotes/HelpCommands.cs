namespace DutyNotes;

/// <summary>One chat command, as the Help tab shows it. A macro is suggested for the ones worth putting on a hotbar.</summary>
/// <param name="MacroIcon">
/// The icon to suggest for the macro: a game icon id from the macro Icons window (0 for none). The macro text can't choose it
/// (the game's /micon command only takes the names of actions, items and the like), so the Help tab shows it to pick by hand.
/// </param>
internal sealed record CommandHelp(string Command, string What, string? MacroName = null, string? Tip = null, uint MacroIcon = 0)
{
    /// <summary>The macro text: just the command, which is all a macro line needs.</summary>
    public string? Macro => MacroName is null ? null : Command;
}

/// <summary>
/// Every chat command the plugin handles (<c>Plugin.OnCommand</c>), with what it does and which ones to suggest as macros.
/// The Help tab is drawn from this list, and a test checks it against the README and against what the plugin handles,
/// so adding a command means adding it here too.
/// </summary>
/// <summary>Icons from the game's macro Icons window (the MacroIcon sheet, ids 66xxx), as suggested for the macros below.</summary>
internal static class MacroIcons
{
    public const uint LeftArrow = 66301;
    public const uint RightArrow = 66302;
    public const uint Loop = 66329;
}

internal static class HelpCommands
{
    public const string Main = "/dutynotes";

    public static readonly IReadOnlyList<CommandHelp> All = new[]
    {
        new CommandHelp("/dutynotes", "Show or hide the overlay."),
        new CommandHelp("/dutynotes edit", "Open or close the editor."),
        new CommandHelp("/dutynotes lock", "Lock or unlock the overlay. Locked means click-through: the mouse goes to the game, not the overlay. Ctrl + right-click still opens its menu."),
        new CommandHelp("/dutynotes next", "Go to the next phase. Phases that are switched off are skipped, and it stops at the last one.", "DN Next",
            "Bind this to a key you can reach in a fight. It moves the overlay to the next phase when you are showing one phase at a time (turn off Show full encounter).",
            MacroIcon: MacroIcons.RightArrow),
        new CommandHelp("/dutynotes prev", "Go to the previous phase (also /dutynotes previous). It stops at the first one.", "DN Prev",
            "For when a phase restarts, or you pressed Next too early.",
            MacroIcon: MacroIcons.LeftArrow),
        new CommandHelp("/dutynotes reset", "Go back to the first phase.", "DN Reset",
            "Press it at the start of every pull, so the overlay is on the first phase.",
            MacroIcon: MacroIcons.Loop),
        new CommandHelp("/dutynotes full", "Switch between the whole encounter and just the current phase."),
    };
}
