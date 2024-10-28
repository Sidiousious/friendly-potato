using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using ImGuiNET;

namespace FriendlyPotato.Windows;

public class DtrClickWindow : Window, IDisposable
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
    public DtrClickWindow(FriendlyPotato plugin, PlayerInformation playerInformation) : base(
        "Friendly Potato Player List###FriendlyPotatoDtrList")
    {
        Flags = ImGuiWindowFlags.NoTitleBar;
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

        if (configuration.ShowTotalsInList)
        {
            if (configuration.NearbyTotal)
            {
                InlineIcon(FontAwesomeIcon.User, "Total players nearby");
                ImGui.Text(playerInformation.Total.ToString());
                ImGui.SameLine();
            }

            if (configuration.NearbyFriends)
            {
                InlineIcon(FontAwesomeIcon.UserFriends, "Friends nearby");
                ImGui.Text(playerInformation.Friends.ToString());
                ImGui.SameLine();
            }

            if (configuration.NearbyOffWorld)
            {
                InlineIcon(FontAwesomeIcon.Plane, "Players from other worlds nearby");
                ImGui.Text(playerInformation.OffWorlders.ToString());
                ImGui.SameLine();
            }

            if (configuration.NearbyDead)
            {
                InlineIcon(FontAwesomeIcon.Skull, "Dead players nearby");
                WithTextColor(DeadColor(), () => ImGui.Text(playerInformation.Dead.ToString()));
                ImGui.SameLine();
            }

            if (configuration.NearbyDoomed)
            {
                InlineIcon(FontAwesomeIcon.SkullCrossbones, "Doomed players nearby");
                WithTextColor(DoomedColor(), () => ImGui.Text(playerInformation.Doomed.ToString()));
                ImGui.SameLine();
            }

            if (configuration.ShowWees)
            {
                InlineIcon(FontAwesomeIcon.SmileBeam, "Wee Ea minions nearby");
                WithTextColor(WeesColor(), () => ImGui.Text(playerInformation.Wees.ToString()));
                ImGui.SameLine();
            }

            ImGui.NewLine();
            ImGui.Spacing();
        }

        List<string> typeHeaders = [];
        if (configuration.NearbyDead) typeHeaders.Add("Dead");

        if (configuration.NearbyDoomed) typeHeaders.Add("Doomed");

        if (typeHeaders.Count == 0)
        {
            ImGui.TextWrapped("This window lists dead/doomed players, when those settings are enabled.");
            return;
        }

        ImGui.Text(string.Join(" & ", typeHeaders) + " Players:");

        var cameraAimAngle = AimAngle(AimVector2());

        var drawnPlayers = playerInformation.Players
                                            .Where(p => p.IsKind(PlayerCharacterKind.Dead) ||
                                                        p.IsKind(PlayerCharacterKind.Doomed))
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
                color.W *= 0.8f;
                raiseText += " (untargetable)";
                selectableFlags = ImGuiSelectableFlags.Disabled;
            }

            var angleToTarget = AngleToTarget(player.Character, cameraAimAngle);
            var icon = DirectionArrow(angleToTarget);

            var text =
                $"[{player.JobAbbreviation}] {player.Character.Name}  {player.Character.HomeWorld.GameData?.Name ?? "Unknown"}{raiseText}";
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
        } finally
        {
            ImGui.PopStyleColor(1);
        }
    }

    private static double AngleToTarget(IPlayerCharacter character, double aimAngle)
    {
        var dirVector3 = FriendlyPotato.ClientState.LocalPlayer!.Position - character.Position;
        var dirVector = Vector2.Normalize(new Vector2(dirVector3.X, dirVector3.Z));
        var dirAngle = AimAngle(dirVector);
        var angularDifference = dirAngle - aimAngle;
        switch (angularDifference)
        {
            case > 180:
                angularDifference -= 360;
                break;
            case < -180:
                angularDifference += 360;
                break;
        }

        return angularDifference;
    }

    private static double AimAngle(Vector2 aimVector)
    {
        return Math.Atan2(aimVector.Y, aimVector.X) * 180f / Math.PI;
    }

    private static unsafe Vector2 AimVector2()
    {
        var camera = CameraManager.Instance()->CurrentCamera;
        var threeDAim =
            new Vector3(camera->RenderCamera->Origin.X, camera->RenderCamera->Origin.Y,
                        camera->RenderCamera->Origin.Z) - FriendlyPotato.ClientState.LocalPlayer!.Position;
        return Vector2.Normalize(new Vector2(threeDAim.X, threeDAim.Z));
    }
}
