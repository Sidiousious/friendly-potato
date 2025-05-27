using System;
using Dalamud.Configuration;

namespace FriendlyPotato;

[Serializable]
public class Configuration : IPluginConfiguration
{
    // DTR
    public bool DtrEnabled { get; set; } = true;
    public bool DtrTotalEnabled { get; set; } = true;
    public bool DtrDeadEnabled { get; set; } = false;
    public bool DtrOffWorldEnabled { get; set; } = true;
    public bool DtrFriendsEnabled { get; set; } = true;
    public bool DtrWeesEnabled { get; set; } = false;
    public bool DtrDoomEnabled { get; set; } = false;

    // Player list window
    public bool ListCountsEnabled { get; set; } = true;
    public bool ListTotalEnabled { get; set; } = true;
    public bool ListDeadEnabled { get; set; } = true;
    public bool ListOffWorldEnabled { get; set; } = false;
    public bool ListFriendsEnabled { get; set; } = false;
    public bool ListWeesEnabled { get; set; } = true;
    public bool ListDoomEnabled { get; set; } = true;

    // Locator
    public bool ShowHuntLocator { get; set; } = false;
    public bool HuntLocatorBackgroundEnabled { get; set; } = false;
    public bool SChatLocatorEnabled { get; set; } = false;
    public bool SRankSoundEnabled { get; set; } = false;
    public bool ChatLocatorARanksEnabled { get; set; } = false;
    public bool ARankSoundEnabled { get; set; } = false;
    public bool PingOnPull { get; set; } = false;
    public bool FateLocatorEnabled { get; set; } = false;
    public bool FateChatEnabled { get; set; } = false;
    public bool FateSoundEnabled { get; set; } = false;
    public bool TreasureLocatorEnabled { get; set; } = false;
    public bool TreasureSoundEnabled { get; set; } = false;
    public float LocatorOffsetX { get; set; } = -20;
    public float LocatorOffsetY { get; set; } = 15;

    // (CW)LS tracking
    public bool HighlightInactive { get; set; } = false;
    public int InactivityThreshold { get; set; } = 14;

    // Debug
    public bool DebugList { get; set; } = false;
    public bool DebugStatuses { get; set; } = false;

    // Wee Ea
    public bool WeesInUtOnly { get; set; } = true;
    private bool UtLimitOk => !WeesInUtOnly || FriendlyPotato.ClientState.MapId == 699;
    internal bool DtrShowWees => DtrWeesEnabled && UtLimitOk;
    internal bool ListShowWees => ListWeesEnabled && UtLimitOk;

    // Version for migrations
    public int Version { get; set; } = 1;

    // the below exist just to make saving less cumbersome
    public void Save()
    {
        FriendlyPotato.PluginInterface.SavePluginConfig(this);
    }
}
