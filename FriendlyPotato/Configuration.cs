using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace FriendlyPotato;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public bool NearbyEnabled { get; set; } = true;
    public bool NearbyDead { get; set; } = true;
    public bool NearbyOffWorld { get; set; } = true;
    public bool NearbyFriends { get; set; } = true;

    // the below exist just to make saving less cumbersome
    public void Save()
    {
        FriendlyPotato.PluginInterface.SavePluginConfig(this);
    }
}
