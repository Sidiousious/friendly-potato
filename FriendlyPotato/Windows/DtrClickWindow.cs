using System;
using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace FriendlyPotato.Windows;

public class DtrClickWindow : Window, IDisposable
{
    private readonly PlayerInformation playerInformation;
    private int selectedIdx = -1;
    private Vector2 mousePosition;

    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public DtrClickWindow(FriendlyPotato plugin, PlayerInformation playerInformation) : base("Friendly Potato Player List###FriendlyPotatoDtrList")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoTitleBar;
        this.playerInformation = playerInformation;
    }

    public void Dispose() { }

    public override void OnOpen() => mousePosition = ImGui.GetMousePos();

    public override void PreDraw()
    {
        var dynamicHeight = (ImGui.GetTextLineHeightWithSpacing() * playerInformation.Players.Count) - ImGui.GetFrameHeight() - ImGui.GetStyle().WindowPadding.Y + ImGuiHelpers.GlobalScale;
        SizeCondition = ImGuiCond.Always;
        Size = new Vector2(200f, Math.Min(700f, dynamicHeight));
        Position = new Vector2(mousePosition.X - 100, mousePosition.Y + 20);
    }

    public override void Draw()
    {
        for (var i = 0; i < playerInformation.Players.Count; i++)
        {
            var player = playerInformation.Players[i];
            var text = $"{player.Name}  {player.HomeWorld.GameData?.Name ?? "Unknown"}";
            if (ImGui.Selectable(text, selectedIdx == i))
            {
                selectedIdx = i;
                FriendlyPotato.TargetManager.FocusTarget = player;
            }
        }
    }
}
