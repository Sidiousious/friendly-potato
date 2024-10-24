using System;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using ImGuiNET;

namespace FriendlyPotato.Windows;

public class DtrClickWindow : Window, IDisposable
{
    private readonly PlayerInformation playerInformation;
    private readonly Configuration configuration;
    private int selectedIdx = -1;
    private Vector2 mousePosition;

    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public DtrClickWindow(FriendlyPotato plugin, PlayerInformation playerInformation) : base("Friendly Potato Player List###FriendlyPotatoDtrList")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoTitleBar;
        this.playerInformation = playerInformation;
        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void OnOpen() => mousePosition = ImGui.GetMousePos();

    public override void PreDraw()
    {
        var dynamicHeight = (ImGui.GetTextLineHeightWithSpacing() * (playerInformation.Dead + 4)) - ImGui.GetFrameHeight() - ImGui.GetStyle().WindowPadding.Y + ImGuiHelpers.GlobalScale;
        SizeCondition = ImGuiCond.Always;
        Size = new Vector2(300f, Math.Min(700f, dynamicHeight));
        Position = new Vector2(mousePosition.X - 100, mousePosition.Y + 20);
    }

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
        
        ImGui.Text(string.Join(" & ", typeHeaders) + " Players:");
        for (var i = 0; i < playerInformation.Players.Count; i++)
        {
            var player = playerInformation.Players[i];
            
            if (!player.IsKind(PlayerCharacterKind.Dead) && !player.IsKind(PlayerCharacterKind.Doomed))
            {
                continue;
            }

            var raiseText = "";
            var color = new Vector4(1f, 0.6f, 0.6f, 1f);
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
            
            var text = $"{player.Character.Name}  {player.Character.HomeWorld.GameData?.Name ?? "Unknown"}{raiseText}";
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
