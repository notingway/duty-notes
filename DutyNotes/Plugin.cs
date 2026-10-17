using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace DutyNotes;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/dutynotes";

    // The command from before the rename. Kept (and left out of /xlhelp) so existing macros keep working.

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    internal Configuration Config { get; }
    internal ActionCatalog Catalog { get; }
    internal DutyCatalog Duties { get; }
    internal JobCatalog Jobs { get; }

    private readonly WindowSystem windows = new("DutyNotes");
    private readonly OverlayWindow overlay;
    private readonly ControlsWindow controls;
    private readonly EditorWindow editor;

    public Plugin()
    {
        MigrateSettingsFromMitigationSheet();
        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Config.Migrate();
        Catalog = new ActionCatalog(DataManager);
        Duties = new DutyCatalog(DataManager);
        Jobs = new JobCatalog(DataManager);

        overlay = new OverlayWindow(this);
        controls = new ControlsWindow(this, overlay);
        editor = new EditorWindow(this);
        windows.AddWindow(overlay);
        windows.AddWindow(controls);
        windows.AddWindow(editor);
        overlay.IsOpen = Config.OverlayVisible;

        CommandManager.AddHandler(Command, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle the Duty Notes overlay. '/dutynotes edit' opens the editor, '/dutynotes lock' toggles click-through, " +
                          "'/dutynotes next', '/dutynotes prev' and '/dutynotes reset' move between phases, '/dutynotes full' switches between the whole encounter and the current phase.",
        });

        PluginInterface.UiBuilder.Draw += windows.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleOverlay;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleEditor;
        Framework.Update += OnFrameworkUpdate;
    }

    /// <summary>
    /// This plugin used to be called MitigationSheet and kept its settings in MitigationSheet.json. If there are
    /// no Duty Notes settings yet, copy them over once. The file records the class and assembly names it was
    /// saved with ("MitigationSheet.SheetNode, MitigationSheet"), so those are rewritten to the new ones or the
    /// settings could not be read back. The old file is left where it is, as a backup.
    /// </summary>
    private static void MigrateSettingsFromMitigationSheet()
    {
        try
        {
            var file = PluginInterface.ConfigFile;
            if (file.Exists || file.Directory is null) return;

            var old = new FileInfo(Path.Combine(file.Directory.FullName, "MitigationSheet.json"));
            if (!old.Exists) return;

            var text = Configuration.RenameLegacyTypes(File.ReadAllText(old.FullName));
            File.WriteAllText(file.FullName, text);

            Log.Information("Copied settings from {Old} to {New}", old.FullName, file.FullName);
        }
        catch (Exception ex)
        {
            // Not fatal: the plugin just starts with default settings.
            Log.Error(ex, "Could not copy the settings from MitigationSheet");
        }
    }

    private uint lastTerritory;
    private uint lastJob;

    /// <summary>
    /// Switches the overlay to the encounter matching your duty and job. It only reacts when the zone or
    /// the job changes, so choosing a sheet by hand sticks until the next change.
    /// </summary>
    private void OnFrameworkUpdate(IFramework framework)
    {
        // Dalamud hides plugin windows in cutscenes unless told not to. Follow the setting every frame, so a
        // change in the settings takes effect at once.
        PluginInterface.UiBuilder.DisableCutsceneUiHide = !Config.HideDuringCutscenes;

        var job = ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0;
        if (job == 0) return; // not logged in (yet)

        var territory = ClientState.TerritoryType;
        if (territory == lastTerritory && job == lastJob) return;
        lastTerritory = territory;
        lastJob = job;

        if (!Config.AutoSwitch) return;

        var match = FindEncounter(territory, job);

        if (match is not null && Config.ActiveEncounter?.Id != match.Id)
        {
            // A new encounter starts at its first phase. One already active keeps its place.
            Config.SetActiveEncounter(match);
            Log.Information("Switched to \"{Name}\" for territory {Territory}, job {Job}", match.Name, territory, job);
        }

        // Entering a duty with a sheet for you brings the overlay up if it was hidden. Leaving it (or switching to
        // a job it has no sheet for) puts it away again, but only if we are the ones who opened it.
        switch (Config.OverlayChangeFor(match is not null, Config.OverlayVisible, overlayOpenedByUs))
        {
            case OverlayChange.Show:
                Config.OverlayVisible = true;
                overlay.IsOpen = true;
                overlayOpenedByUs = true;
                break;
            case OverlayChange.Hide:
                Config.OverlayVisible = false;
                overlay.IsOpen = false;
                overlayOpenedByUs = false;
                break;
        }

        Save();
    }

    /// <summary>True while the overlay is up because auto-switch opened it; then leaving the duty may hide it again.</summary>
    private bool overlayOpenedByUs;
    /// <summary>The encounter for this zone, preferring one set to exactly this job over one set to any job.</summary>
    private SheetNode? FindEncounter(uint territory, uint job)
    {
        if (territory == 0) return null;
        var inZone = Config.Roots.Where(r => r.DutyTerritoryId == territory).ToList();
        return inZone.FirstOrDefault(r => r.JobId == job) ?? inZone.FirstOrDefault(r => r.JobId == 0);
    }

    internal void Save() => PluginInterface.SavePluginConfig(Config);

    internal void SetOverlayOpen(bool open)
    {
        overlay.IsOpen = open;
        overlayOpenedByUs = false; // chosen by hand, so it's yours to close
    }

    /// <summary>While the editor is open the overlay shows what is selected in it, instead of following the navigation.</summary>
    internal bool EditorOpen => editor.IsOpen;

    private void ToggleOverlay()
    {
        overlayOpenedByUs = false;
        overlay.IsOpen = !overlay.IsOpen;
        Config.OverlayVisible = overlay.IsOpen;
        Save();
    }

    private void ToggleEditor() => editor.IsOpen = !editor.IsOpen;

    internal void OpenEditor() => editor.IsOpen = true;

    /// <summary>Shows or hides the overlay and remembers the choice (what the "Show overlay" checkbox does).</summary>
    internal void SetOverlayVisible(bool visible)
    {
        overlayOpenedByUs = false;
        Config.OverlayVisible = visible;
        overlay.IsOpen = visible;
        Save();
    }

    /// <summary>
    /// Moves the overlay between the phases of the current encounter. The buttons under the overlay and the
    /// chat commands (usable from macros) both go through here, so they always behave the same.
    /// </summary>
    internal void StepPhase(PhaseStep step)
    {
        if (StepTarget(step) is not { } target) return;

        Config.ActivePhaseId = target.Id;

        // While the editor is open the overlay shows the editor's selection, so move that too. The buttons then
        // work while editing, and the tree shows the phase they landed on.
        if (EditorOpen) Config.SelectedId = target.Id;

        Save();
    }

    /// <summary>The phase a step would go to, or null if it would not move (nowhere to go, or already there).</summary>
    internal SheetNode? StepTarget(PhaseStep step)
    {
        // With the encounter itself selected in the editor, no phase is "current".
        var fromEncounter = EditorOpen && Config.Selected is { } selected && Config.Roots.Contains(selected);

        var target = Config.PhaseTarget(step, fromEncounter);
        var currentId = fromEncounter ? Guid.Empty : Config.CurrentPhase?.Id;
        return target is null || target.Id == currentId ? null : target;
    }

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "edit":
            case "config":
                ToggleEditor();
                break;
            case "lock":
                Config.LockOverlay = !Config.LockOverlay;
                Save();
                break;
            case "next":
                StepPhase(PhaseStep.Next);
                break;
            case "prev":
            case "previous":
                StepPhase(PhaseStep.Previous);
                break;
            case "reset":
                StepPhase(PhaseStep.Reset);
                break;
            case "full":
                Config.ShowFullEncounter = !Config.ShowFullEncounter;
                Save();
                break;
            default:
                ToggleOverlay();
                break;
        }
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        PluginInterface.UiBuilder.Draw -= windows.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleOverlay;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleEditor;
        windows.RemoveAllWindows();
        overlay.Dispose();
        CommandManager.RemoveHandler(Command);
    }
}
