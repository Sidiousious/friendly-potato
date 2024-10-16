using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace FriendlyPotato.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public ConfigWindow(FriendlyPotato plugin) : base("Friendly Potato Configuration###FriendlyPotatoConfig")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(300, 180);
        SizeCondition = ImGuiCond.Always;

        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        Flags &= ~ImGuiWindowFlags.NoMove;
    }

    public override void Draw()
    {
        var nearbyEnabled = configuration.NearbyEnabled;
        if (ImGui.Checkbox("Show nearby player information", ref nearbyEnabled))
        {
            configuration.NearbyEnabled = nearbyEnabled;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            configuration.Save();
        }

        var nearbyDead = configuration.NearbyDead;
        if (ImGui.Checkbox("Show nearby dead players", ref nearbyDead))
        {
            configuration.NearbyDead = nearbyDead;
            configuration.Save();
        }

        var nearbyOffWorld = configuration.NearbyOffWorld;
        if (ImGui.Checkbox("Show nearby off-world players", ref nearbyOffWorld))
        {
            configuration.NearbyOffWorld = nearbyOffWorld;
            configuration.Save();
        }

        var nearbyFriends = configuration.NearbyFriends;
        if (ImGui.Checkbox("Show nearby friends", ref nearbyFriends))
        {
            configuration.NearbyFriends = nearbyFriends;
            configuration.Save();
        }
    }
}
