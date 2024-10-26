using System;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace FriendlyPotato.Windows;

public class DtrClickWindow : Window, IDisposable
{
    private readonly PlayerInformation playerInformation;
    private readonly Configuration configuration;
    private int selectedIdx = -1;
            
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

        var drawnPlayers = playerInformation.Players
#if !DEBUG
                                            .Where(p => p.IsKind(PlayerCharacterKind.Dead) || p.IsKind(PlayerCharacterKind.Doomed))
#endif
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
            
            var text = $"[{player.JobAbbreviation}] {player.Character.Name}  {player.Character.HomeWorld.GameData?.Name ?? "Unknown"}{raiseText}";
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            if (ImGui.Selectable(text, selectedIdx == i))
            {
                selectedIdx = i;
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
}
