using System;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace FriendlyPotato.Windows;

public class DtrClickWindow : Window, IDisposable
{
    private readonly PlayerInformation playerInformation;
    private readonly Configuration configuration;

    private readonly string[] healers = ["AST", "WHM", "SCH", "SGE"];

    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public DtrClickWindow(FriendlyPotato plugin, PlayerInformation playerInformation) : base("Friendly Potato Player List###FriendlyPotatoDtrList")
    {
        Flags = ImGuiWindowFlags.NoTitleBar;
        SizeCondition = ImGuiCond.FirstUseEver;
        Size = new Vector2(300f, 300f);
        this.playerInformation = playerInformation;
        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        System.Collections.Generic.List<string> typeHeaders = [];
        if (configuration.NearbyDead)
        {
            typeHeaders.Add("Dead");
        }

        if (configuration.NearbyDoomed)
        {
            typeHeaders.Add("Doomed");
        }

        if (typeHeaders.Count == 0)
        {
            ImGui.TextWrapped("This window lists dead/doomed players, when those settings are enabled.");
            return;
        }

        var cameraAimAngle = AimAngle(AimVector2());

        var drawnPlayers = playerInformation.Players
                                            .Where(p => p.IsKind(PlayerCharacterKind.Dead) || p.IsKind(PlayerCharacterKind.Doomed))
                                            .ToList();
        drawnPlayers.Sort((a, b) =>
        {
            var aIsHealer = healers.Contains(a.JobAbbreviation);
            var bIsHealer = healers.Contains(b.JobAbbreviation);
            if (aIsHealer && !bIsHealer)
            {
                return -1;
            }

            if (!aIsHealer && bIsHealer)
            {
                return 1;
            }

            if (a.JobAbbreviation == "RDM" && b.JobAbbreviation != "RDM")
            {
                return -1;
            }

            if (a.JobAbbreviation != "RDM" && b.JobAbbreviation == "RDM")
            {
                return 1;
            }

            if (a.JobAbbreviation == "SMN" && b.JobAbbreviation != "SMN")
            {
                return -1;
            }

            if (a.JobAbbreviation != "SMN" && b.JobAbbreviation == "SMN")
            {
                return 1;
            }

            if (a.Character.IsTargetable && !b.Character.IsTargetable)
            {
                return -1;
            }

            if (!a.Character.IsTargetable && b.Character.IsTargetable)
            {
                return 1;
            }

            return string.Compare(a.Character.Name.ToString(), b.Character.Name.ToString(), StringComparison.Ordinal);
        });

        ImGui.Text(string.Join(" & ", typeHeaders) + " Players:");
        for (var i = 0; i < drawnPlayers.Count; i++)
        {
            var player = drawnPlayers[i];
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

            var text = $"[{player.JobAbbreviation}] {player.Character.Name}  {player.Character.HomeWorld.GameData?.Name ?? "Unknown"}{raiseText}";
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            using (FriendlyPotato.PluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
            {
                ImGui.Text(icon.ToIconString());
                ImGui.SameLine();
            }
            if (ImGui.Selectable(text, FriendlyPotato.TargetManager.Target?.GameObjectId == player.Character.GameObjectId, selectableFlags))
            {
                if (FriendlyPotato.KeyState[VirtualKey.CONTROL])
                {
                    FriendlyPotato.TargetManager.FocusTarget = player.Character;
                }
                else
                {
                    FriendlyPotato.TargetManager.Target = player.Character;
                }
            }

            ImGui.PopStyleColor(1);
        }
    }

    private static FontAwesomeIcon DirectionArrow(double angle)
    {
        return angle switch
        {
            > -45 and < 45 => FontAwesomeIcon.ArrowCircleUp,     // up arrow
            <= -135 or >= 135 => FontAwesomeIcon.ArrowCircleDown,  // back arrow
            <= -45 and > -135 => FontAwesomeIcon.ArrowCircleLeft,  // left arrow
            >= 45 and < 135 => FontAwesomeIcon.ArrowCircleRight,    // right arrow
            _ => FontAwesomeIcon.Question
        };
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

    private static double AimAngle(Vector2 aimVector) => Math.Atan2(aimVector.Y, aimVector.X) * 180f / Math.PI;

    private static unsafe Vector2 AimVector2()
    {
        var camera = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.CameraManager.Instance()->CurrentCamera;
        var threeDAim = new Vector3(camera->RenderCamera->Origin.X, camera->RenderCamera->Origin.Y, camera->RenderCamera->Origin.Z) - FriendlyPotato.ClientState.LocalPlayer!.Position;
        return Vector2.Normalize(new Vector2(threeDAim.X, threeDAim.Z));
    }
}
