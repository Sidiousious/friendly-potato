using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;

namespace FriendlyPotato.Windows;

public class PlayerListWindow : Window, IDisposable
{
    private readonly Vector4 colorBlue = new(0.6f, 0.6f, 1f, 1f);
    private readonly Vector4 colorGreen = new(0.6f, 1f, 0.6f, 1f);

    private readonly Vector4 colorRed = new(1f, 0.6f, 0.6f, 1f);
    private readonly Vector4 colorWhite = new(1f, 1f, 1f, 1f);
    private readonly Configuration configuration;

    private readonly string[] healers = ["AST", "WHM", "SCH", "SGE"];
    private readonly PlayerInformation playerInformation;

    private readonly Action toggleConfigUi;

    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public PlayerListWindow(FriendlyPotato plugin, PlayerInformation playerInformation) : base(
        "Friendly Potato Player List###FriendlyPotatoDtrList")
    {
        SizeCondition = ImGuiCond.FirstUseEver;
        Size = new Vector2(300f, 300f);
        this.playerInformation = playerInformation;
        configuration = plugin.Configuration;
        toggleConfigUi = plugin.ToggleConfigUi;
    }

    public void Dispose() { }

    private Vector4 DeadColor()
    {
        if (playerInformation.Dead > 0 && playerInformation.Raised != playerInformation.Dead) return colorRed;

        return colorWhite;
    }

    private Vector4 DoomedColor()
    {
        return playerInformation.Doomed > 0 ? colorRed : colorWhite;
    }

    private Vector4 WeesColor()
    {
        return playerInformation.Wees >= 10 ? colorGreen : colorWhite;
    }

    public override void Draw()
    {
        InlineButtonIcon(FontAwesomeIcon.Cog, toggleConfigUi, "Open configuration");

        if (configuration.ListCountsEnabled)
        {
            if (configuration.ListTotalEnabled)
            {
                InlineIcon(FontAwesomeIcon.User, "Total players nearby");
                var historical = $" (~{playerInformation.SeenHistory.Count})";
                ImGui.Text($"{playerInformation.Total}{(playerInformation.Total > 40 ? historical : "")}");
                ImGui.SameLine();
            }

            if (configuration.ListFriendsEnabled)
            {
                InlineIcon(FontAwesomeIcon.UserFriends, "Friends nearby");
                ImGui.Text(playerInformation.Friends.ToString());
                ImGui.SameLine();
            }

            if (configuration.ListOffWorldEnabled)
            {
                InlineIcon(FontAwesomeIcon.Plane, "Players from other worlds nearby");
                ImGui.Text(playerInformation.OffWorlders.ToString());
                ImGui.SameLine();
            }

            if (configuration.ListDeadEnabled)
            {
                InlineIcon(FontAwesomeIcon.Skull, "Dead players nearby");
                WithTextColor(DeadColor(), () => ImGui.Text(playerInformation.Dead.ToString()));
                ImGui.SameLine();
            }

            if (configuration.ListDoomEnabled)
            {
                InlineIcon(FontAwesomeIcon.SkullCrossbones, "Doomed players nearby");
                WithTextColor(DoomedColor(), () => ImGui.Text(playerInformation.Doomed.ToString()));
                ImGui.SameLine();
            }

            if (configuration.ListShowWees)
            {
                InlineIcon(FontAwesomeIcon.SmileBeam, "Wee Ea minions nearby");
                WithTextColor(WeesColor(), () => ImGui.Text(playerInformation.Wees.ToString()));
                ImGui.SameLine();
            }

            ImGui.NewLine();
            ImGui.Spacing();
        }

        List<string> typeHeaders = [];
        if (configuration.ListDeadEnabled) typeHeaders.Add("Dead");

        if (configuration.ListDoomEnabled) typeHeaders.Add("Doomed");

        if (typeHeaders.Count == 0)
        {
            ImGui.TextWrapped("This window lists dead/doomed players, when those settings are enabled.");
            return;
        }

        ImGui.Text(string.Join(" & ", typeHeaders) + " Players:");

        var cameraAimVector = CameraAngles.OwnAimVector2();

        if (cameraAimVector == Vector2.Zero)
        {
            // No camera exists, assume loading screen and disable unnecessary listing
            ImGui.TextWrapped("<Disabled while player has no camera>");
            return;
        }

        var cameraAimAngle = CameraAngles.AimAngle(cameraAimVector);

        var drawnPlayers = playerInformation.Players
                                            .Where(p =>
                                            {
#if DEBUG
                                                if (configuration.DebugList) return true;
#endif
                                                return p.IsKind(PlayerCharacterKind.Dead) ||
                                                       p.IsKind(PlayerCharacterKind.Doomed);
                                            })
                                            .ToList();
        drawnPlayers.Sort((a, b) =>
        {
            // Push targetable to the top
            if (a.Character.IsTargetable && !b.Character.IsTargetable) return -1;

            if (!a.Character.IsTargetable && b.Character.IsTargetable) return 1;

            // Push raised to the bottom
            if (a.Raised && !b.Raised) return 1;

            if (!a.Raised && b.Raised) return -1;

            // Push doomed to the bottom
            if (a.Doomed && !b.Doomed) return 1;

            if (!a.Doomed && b.Doomed) return -1;

            // Push healers to the top
            var aIsHealer = healers.Contains(a.JobAbbreviation);
            var bIsHealer = healers.Contains(b.JobAbbreviation);
            if (aIsHealer && !bIsHealer) return -1;

            if (!aIsHealer && bIsHealer) return 1;

            // Push RDMs to the top
            if (a.JobAbbreviation == "RDM" && b.JobAbbreviation != "RDM") return -1;

            if (a.JobAbbreviation != "RDM" && b.JobAbbreviation == "RDM") return 1;

            // Push SMNs to the top
            if (a.JobAbbreviation == "SMN" && b.JobAbbreviation != "SMN") return -1;

            if (a.JobAbbreviation != "SMN" && b.JobAbbreviation == "SMN") return 1;

            // Fallback alphabetical for consistent sort
            return string.Compare(a.Character.Name.ToString(), b.Character.Name.ToString(), StringComparison.Ordinal);
        });

        foreach (var player in drawnPlayers)
        {
            var color = new Vector4(1f, 1f, 1f, 1f);
            var raiseText = "";
            if (player.IsKind(PlayerCharacterKind.Dead))
            {
                raiseText = " (dead)";
                color = new Vector4(1f, 0.6f, 0.6f, 1f);
            }

            if (player.Raised)
            {
                raiseText = " (raised)";
                color = new Vector4(0.6f, 1f, 1f, 1f);
            }

            if (player.Doomed)
            {
                raiseText = " (doomed)";
                color = new Vector4(1f, 0.6f, 1f, 1f);
            }

            var selectableFlags = ImGuiSelectableFlags.None;
            if (!player.Character.IsTargetable)
            {
                color.W *= 0.5f;
                raiseText += " (untargetable)";
                selectableFlags = ImGuiSelectableFlags.Disabled;
            }

            var angleToTarget = CameraAngles.AngleToTarget(player.Character.Position, cameraAimAngle);
            var icon = DirectionArrow(angleToTarget);
            var distance = FriendlyPotato.DistanceToTarget(player.Character.Position);

            if (distance > 31.0)
            {
                color.W *= 0.7f;
                raiseText += " (FAR)";
            }

            var text =
                $"[{player.JobAbbreviation}] {player.Character.Name}  {player.Character.HomeWorld.ValueNullable?.Name.ExtractText() ?? "Unknown"}{raiseText}";
            WithTextColor(color, () =>
            {
                InlineIcon(icon);
                if (ImGui.Selectable(
                        text, FriendlyPotato.TargetManager.Target?.GameObjectId == player.Character.GameObjectId,
                        selectableFlags))
                {
                    if (FriendlyPotato.KeyState[VirtualKey.CONTROL])
                        FriendlyPotato.TargetManager.FocusTarget = player.Character;
                    else
                        FriendlyPotato.TargetManager.Target = player.Character;
                }
#if DEBUG
                if (configuration.DebugList)
                {
                    ImGui.SameLine();
                    ImGui.Text($" ({distance:F1}u)");
                }
#endif
            });
        }
    }

    private static FontAwesomeIcon DirectionArrow(double angle)
    {
        return angle switch
        {
            > -45 and < 45 => FontAwesomeIcon.ArrowCircleUp,      // up arrow
            <= -135 or >= 135 => FontAwesomeIcon.ArrowCircleDown, // back arrow
            <= -45 and > -135 => FontAwesomeIcon.ArrowCircleLeft, // left arrow
            >= 45 and < 135 => FontAwesomeIcon.ArrowCircleRight,  // right arrow
            _ => FontAwesomeIcon.Question
        };
    }

    private static void InlineIcon(FontAwesomeIcon icon, string tooltip = "")
    {
        using (FriendlyPotato.PluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
        {
            ImGui.Text(icon.ToIconString());
            ImGui.SameLine();
        }

        if (tooltip != "" && ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text(tooltip);
            ImGui.EndTooltip();
        }
    }

    private static void InlineButtonIcon(FontAwesomeIcon icon, Action action, string tooltip = "")
    {
        using (FriendlyPotato.PluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
        {
            if (ImGui.Button(icon.ToIconString())) action();
            ImGui.SameLine();
        }

        if (tooltip != "" && ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text(tooltip);
            ImGui.EndTooltip();
        }
    }

    private static void WithTextColor(Vector4 color, Action action)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        try
        {
            action();
        }
        finally
        {
            ImGui.PopStyleColor(1);
        }
    }
}
