using System;
using Dalamud.Configuration;

namespace FriendlyPotato;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public bool NearbyEnabled { get; set; } = true;
    public bool NearbyTotal { get; set; } = true;
    public bool NearbyDead { get; set; } = true;
    public bool NearbyOffWorld { get; set; } = true;
    public bool NearbyFriends { get; set; } = true;
    public bool NearbyWees { get; set; } = false;
    public bool WeesInUtOnly { get; set; } = false;
    public bool NearbyDoomed { get; set; } = false;
    public bool DebugStatuses { get; set; } = false;
    public bool ShowTotalsInList { get; set; } = true;
    internal bool ShowWees => NearbyWees && (!WeesInUtOnly || FriendlyPotato.ClientState.MapId == 699);
    public int Version { get; set; } = 0;

    // the below exist just to make saving less cumbersome
    public void Save()
    {
        FriendlyPotato.PluginInterface.SavePluginConfig(this);
    }
}
