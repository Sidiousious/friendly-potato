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
    public ConfigWindow(FriendlyPotato plugin) : base(
        $"Friendly Potato ({plugin.Version.ToString()}) Configuration###FriendlyPotatoConfig")
    {
        Size = new Vector2(300, 360);
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
        if (ImGui.CollapsingHeader("Player List Settings", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Spacing();
            var showTotalsInList = configuration.ShowTotalsInList;
            if (ImGui.Checkbox("Show totals in player list", ref showTotalsInList))
            {
                configuration.ShowTotalsInList = showTotalsInList;
                configuration.Save();
            }

            ImGui.Spacing();
        }

        if (ImGui.CollapsingHeader("Server Status Bar (DTR) Settings", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Spacing();
            var nearbyEnabled = configuration.NearbyEnabled;
            if (ImGui.Checkbox("Show nearby player information", ref nearbyEnabled))
            {
                configuration.NearbyEnabled = nearbyEnabled;
                // can save immediately on change, if you don't want to provide a "Save and Close" button
                configuration.Save();
            }

            var nearbyTotal = configuration.NearbyTotal;
            if (ImGui.Checkbox("Show total nearby players", ref nearbyTotal))
            {
                configuration.NearbyTotal = nearbyTotal;
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

            var nearbyWees = configuration.NearbyWees;
            if (ImGui.Checkbox("Show nearby Wee Eas", ref nearbyWees))
            {
                configuration.NearbyWees = nearbyWees;
                configuration.Save();
            }

            var weesUtOnly = configuration.WeesInUtOnly;
            if (ImGui.Checkbox("Limit Wee Ea shown to UT only", ref weesUtOnly))
            {
                configuration.WeesInUtOnly = weesUtOnly;
                configuration.Save();
            }

            var nearbyDooms = configuration.NearbyDoomed;
            if (ImGui.Checkbox("Show players hit with Doom", ref nearbyDooms))
            {
                configuration.NearbyDoomed = nearbyDooms;
                configuration.Save();
            }

            ImGui.Spacing();
        }

        if (ImGui.CollapsingHeader("Debug Settings"))
        {
            ImGui.Spacing();
            var debugStatuses = configuration.DebugStatuses;
            if (ImGui.Checkbox("Log debug statuses", ref debugStatuses))
            {
                configuration.DebugStatuses = debugStatuses;
                configuration.Save();
            }

            ImGui.TextWrapped(
                "Player statuses logged as debug, other characters as verbose.");
            ImGui.TextWrapped("THIS WILL FLOOD YOUR LOG.");

            ImGui.Spacing();
        }
    }
}
