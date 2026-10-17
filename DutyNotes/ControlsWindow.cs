using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace DutyNotes;

/// <summary>
/// The Reset / Previous / Next buttons, in their own small window directly under the overlay. A separate
/// window (rather than part of the overlay) keeps them clickable when the overlay is locked and click-through.
/// </summary>
public sealed class ControlsWindow : Window
{
    private const float Gap = 4f;

    private readonly Plugin plugin;
    private readonly OverlayWindow overlay;

    public ControlsWindow(Plugin plugin, OverlayWindow overlay) : base("Duty Notes Controls###mitsheet-controls")
    {
        this.plugin = plugin;
        this.overlay = overlay;

        Flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove
                | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse
                | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoBackground
                | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoSavedSettings;
        PositionCondition = ImGuiCond.Always;

        // Dalamud only draws a window while IsOpen is set. This one has no title bar to close it with;
        // whether it is actually shown is decided by DrawConditions below.
        IsOpen = true;
    }

    /// <summary>
    /// Only while the overlay is up and the buttons are switched on in the settings. Not in full-encounter mode:
    /// every phase is showing there, so there is no "current phase" to move.
    /// </summary>
    public override bool DrawConditions()
        => plugin.Config.ShowControls && !plugin.Config.ShowFullEncounter && overlay.IsOpen && overlay.HasBounds;

    public override void PreDraw()
    {
        // Left edge of the overlay, just under its bottom edge. Follows it when it moves or resizes.
        Position = new Vector2(overlay.Bounds.Position.X, overlay.Bounds.Position.Y + overlay.Bounds.Size.Y + Gap * ImGuiHelpers.GlobalScale);
    }

    public override void Draw()
    {
        if (ImGui.Button("Reset")) plugin.StepPhase(PhaseStep.Reset);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Back to the first phase  (/dutynotes reset)");

        // Previous and Next are greyed out when there is nowhere further to go, i.e. the step would not move.
        ImGui.SameLine();
        StepButton("Previous", PhaseStep.Previous, "Previous phase  (/dutynotes prev)");

        ImGui.SameLine();
        StepButton("Next", PhaseStep.Next, "Next phase  (/dutynotes next)");

    }

    private void StepButton(string label, PhaseStep step, string tooltip)
    {
        var atEnd = plugin.StepTarget(step) is null;

        ImGui.BeginDisabled(atEnd);
        if (ImGui.Button(label)) plugin.StepPhase(step);
        ImGui.EndDisabled();

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(atEnd ? $"{tooltip}\nAlready at the {(step == PhaseStep.Next ? "last" : "first")} phase" : tooltip);
    }
}