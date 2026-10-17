using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;

namespace DutyNotes;

/// <summary>The Help tab: the chat commands, and a macro to copy for each one worth putting on a hotbar.</summary>
internal sealed class HelpTab
{
    private static readonly Vector4 Heading = new(1f, 0.85f, 0.45f, 1f);

    private string copied = string.Empty;
    private double copiedUntil;

    public void Draw()
    {
        if (!ImGui.BeginChild("##help-scroll", new Vector2(0, 0))) { ImGui.EndChild(); return; }

        ImGui.TextWrapped("Everything here can be typed in chat, or put in a macro so a hotbar key does it. " +
                          "The copy icon next to a suggested macro puts its text on the clipboard.");

        ImGui.Spacing();
        ImGui.TextColored(Heading, "Making a macro");
        ImGui.Separator();
        ImGui.TextWrapped("1. Open the game's Macros window from the main menu and pick an empty macro.");
        ImGui.TextWrapped("2. Give it the suggested name, then paste the macro text into the first line.");
        ImGui.TextWrapped("3. Click the icon box next to the name and pick the suggested icon (shown with each macro below).");
        ImGui.TextWrapped("4. Drag the macro from that window onto a hotbar slot, and give the slot a key.");
        ImGui.TextDisabled("A macro line is just the command. Add /echo lines under it if you want a message in chat when it runs.");

        ImGui.Spacing();
        ImGui.TextColored(Heading, "Commands");
        ImGui.Separator();

        for (var i = 0; i < HelpCommands.All.Count; i++)
        {
            var command = HelpCommands.All[i];
            ImGui.PushID(i);

            ImGui.Spacing();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(command.Command);

            ImGui.TextWrapped(command.What);

            if (command.Macro is { } macro)
            {
                ImGui.Indent();

                // The icon to pick for the macro, drawn from the game's own icon. The macro text can't set it.
                var iconSize = new Vector2(32 * ImGuiHelpers.GlobalScale);
                if (command.MacroIcon != 0)
                {
                    var wrap = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(command.MacroIcon)).GetWrapOrEmpty();
                    ImGui.Image(wrap.Handle, iconSize);
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("The suggested macro icon: pick it in the macro's Icons window");
                    ImGui.SameLine();
                }

                ImGui.BeginGroup();
                ImGui.TextDisabled($"Suggested macro, named \"{command.MacroName}\"");
                ImGui.SameLine();
                CopyButton(macro, $"Copy the macro text:\n{macro}");
                if (command.Tip is not null) ImGui.TextDisabled(command.Tip);
                ImGui.EndGroup();

                ImGui.Unindent();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.PopID();
        }

        ImGui.EndChild();
    }

    /// <summary>A copy icon that copies the text, and says so next to it for a couple of seconds. The tooltip says what it copies.</summary>
    private void CopyButton(string text, string tooltip)
    {
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Copy))
        {
            ImGui.SetClipboardText(text);
            copied = text;
            copiedUntil = ImGui.GetTime() + 2;
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(tooltip);

        if (copied == text && ImGui.GetTime() < copiedUntil)
        {
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.TextColored(new Vector4(0.55f, 0.9f, 0.55f, 1f), "Copied");
        }
    }
}
