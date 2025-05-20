using System;
using System.Numerics;
using Dalamud.Interface.Utility.Raii;
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
        Size = new Vector2(360, 360);
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
        if (ImGui.CollapsingHeader("General Settings", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Spacing();

            var showTotalsInList = configuration.ListCountsEnabled;
            if (ImGui.Checkbox("Show counters in player list", ref showTotalsInList))
            {
                configuration.ListCountsEnabled = showTotalsInList;
                configuration.Save();
            }

            var nearbyEnabled = configuration.DtrEnabled;
            if (ImGui.Checkbox("Show counters in status bar", ref nearbyEnabled))
            {
                configuration.DtrEnabled = nearbyEnabled;
                // can save immediately on change, if you don't want to provide a "Save and Close" button
                configuration.Save();
            }

            var weesUtOnly = configuration.WeesInUtOnly;
            if (ImGui.Checkbox("Limit Wee Ea shown to UT only", ref weesUtOnly))
            {
                configuration.WeesInUtOnly = weesUtOnly;
                configuration.Save();
            }

            ImGui.Spacing();
        }

        if (ImGui.CollapsingHeader("Player List Settings"))
        {
            ImGui.Spacing();

            var nearbyTotal = configuration.ListTotalEnabled;
            if (ImGui.Checkbox("Show total nearby players", ref nearbyTotal))
            {
                configuration.ListTotalEnabled = nearbyTotal;
                configuration.Save();
            }

            var nearbyDead = configuration.ListDeadEnabled;
            if (ImGui.Checkbox("Show nearby dead players", ref nearbyDead))
            {
                configuration.ListDeadEnabled = nearbyDead;
                configuration.Save();
            }

            var nearbyOffWorld = configuration.ListOffWorldEnabled;
            if (ImGui.Checkbox("Show nearby off-world players", ref nearbyOffWorld))
            {
                configuration.ListOffWorldEnabled = nearbyOffWorld;
                configuration.Save();
            }

            var nearbyFriends = configuration.ListFriendsEnabled;
            if (ImGui.Checkbox("Show nearby friends", ref nearbyFriends))
            {
                configuration.ListFriendsEnabled = nearbyFriends;
                configuration.Save();
            }

            var nearbyWees = configuration.ListWeesEnabled;
            if (ImGui.Checkbox("Show nearby Wee Eas", ref nearbyWees))
            {
                configuration.ListWeesEnabled = nearbyWees;
                configuration.Save();
            }

            var nearbyDooms = configuration.ListDoomEnabled;
            if (ImGui.Checkbox("Show players hit with Doom", ref nearbyDooms))
            {
                configuration.ListDoomEnabled = nearbyDooms;
                configuration.Save();
            }

            ImGui.Spacing();
        }

        if (ImGui.CollapsingHeader("Server Status Bar (DTR) Settings"))
        {
            ImGui.Spacing();

            var nearbyTotal = configuration.DtrTotalEnabled;
            if (ImGui.Checkbox("Show total nearby players", ref nearbyTotal))
            {
                configuration.DtrTotalEnabled = nearbyTotal;
                configuration.Save();
            }

            var nearbyDead = configuration.DtrDeadEnabled;
            if (ImGui.Checkbox("Show nearby dead players", ref nearbyDead))
            {
                configuration.DtrDeadEnabled = nearbyDead;
                configuration.Save();
            }

            var nearbyOffWorld = configuration.DtrOffWorldEnabled;
            if (ImGui.Checkbox("Show nearby off-world players", ref nearbyOffWorld))
            {
                configuration.DtrOffWorldEnabled = nearbyOffWorld;
                configuration.Save();
            }

            var nearbyFriends = configuration.DtrFriendsEnabled;
            if (ImGui.Checkbox("Show nearby friends", ref nearbyFriends))
            {
                configuration.DtrFriendsEnabled = nearbyFriends;
                configuration.Save();
            }

            var nearbyWees = configuration.DtrWeesEnabled;
            if (ImGui.Checkbox("Show nearby Wee Eas", ref nearbyWees))
            {
                configuration.DtrWeesEnabled = nearbyWees;
                configuration.Save();
            }

            var nearbyDooms = configuration.DtrDoomEnabled;
            if (ImGui.Checkbox("Show players hit with Doom", ref nearbyDooms))
            {
                configuration.DtrDoomEnabled = nearbyDooms;
                configuration.Save();
            }

            ImGui.Spacing();
        }

        if (ImGui.CollapsingHeader("Hunt Locator Settings"))
        {
            ImGui.Spacing();
            var huntLocator = configuration.ShowHuntLocator;
            if (ImGui.Checkbox("Show hunt locator", ref huntLocator))
            {
                configuration.ShowHuntLocator = huntLocator;
                configuration.Save();
            }

            ImGui.Spacing();
            var locatorBackground = configuration.HuntLocatorBackgroundEnabled;
            if (ImGui.Checkbox("Show locator background", ref locatorBackground))
            {
                configuration.HuntLocatorBackgroundEnabled = locatorBackground;
                configuration.Save();
            }

            ImGui.Spacing();
            var pingOnPull = configuration.PingOnPull;
            if (ImGui.Checkbox("Play notification sound on pull", ref pingOnPull))
            {
                configuration.PingOnPull = pingOnPull;
                configuration.Save();
            }

            ImGui.Spacing();
            var chatLocator = configuration.ChatLocatorEnabled;
            if (ImGui.Checkbox("Show S rank flags in chat", ref chatLocator))
            {
                configuration.ChatLocatorEnabled = chatLocator;
                configuration.Save();
            }

            ImGui.Spacing();
            var sSound = configuration.SRankSoundEnabled;
            if (ImGui.Checkbox("Play notification sound on S spawn", ref sSound))
            {
                configuration.SRankSoundEnabled = sSound;
                configuration.Save();
            }

            ImGui.Spacing();
            var aLocator = configuration.ChatLocatorARanksEnabled;
            if (ImGui.Checkbox("Show A rank flags in chat", ref aLocator))
            {
                configuration.ChatLocatorARanksEnabled = aLocator;
                configuration.Save();
            }

            ImGui.Spacing();
            var aSound = configuration.ARankSoundEnabled;
            if (ImGui.Checkbox("Play notification sound on A spawn", ref aSound))
            {
                configuration.ARankSoundEnabled = aSound;
                configuration.Save();
            }

            ImGui.Spacing();

            using (ImRaii.ItemWidth(100f))
            {
                var offsetX = configuration.LocatorOffsetX;
                if (ImGui.InputFloat("Locator X offset", ref offsetX))
                {
                    offsetX = offsetX switch
                    {
                        < -70 => -70,
                        > 70 => 70,
                        _ => offsetX
                    };

                    configuration.LocatorOffsetX = offsetX;
                    configuration.Save();
                }

                var offsetY = configuration.LocatorOffsetY;
                if (ImGui.InputFloat("Locator Y offset", ref offsetY))
                {
                    offsetY = offsetY switch
                    {
                        < -70 => -70,
                        > 70 => 70,
                        _ => offsetY
                    };

                    configuration.LocatorOffsetY = offsetY;
                    configuration.Save();
                }
            }

            ImGui.Spacing();
        }

        if (ImGui.CollapsingHeader("Linkshell Activity Tracker Settings"))
        {
            ImGui.Spacing();
            var highlightEnabled = configuration.HighlightInactive;
            if (ImGui.Checkbox("Highlight potentially inactive users", ref highlightEnabled))
            {
                configuration.HighlightInactive = highlightEnabled;
                configuration.Save();
            }

            ImGui.Spacing();

            var inactivityThreshold = configuration.InactivityThreshold;
            using (ImRaii.ItemWidth(100f))
            {
                if (ImGui.InputInt("Days unseen before highlight", ref inactivityThreshold))
                {
                    configuration.InactivityThreshold = inactivityThreshold;
                    configuration.Save();
                }
            }

            ImGui.Spacing();
        }

        if (ImGui.CollapsingHeader("Debug Settings"))
        {
#if DEBUG
            ImGui.Spacing();
            var debugList = configuration.DebugList;
            if (ImGui.Checkbox("Show debug list", ref debugList))
            {
                configuration.DebugList = debugList;
                configuration.Save();
            }
#endif
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
