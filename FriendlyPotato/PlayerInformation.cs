using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.SubKinds;

namespace FriendlyPotato;

public class PlayerInformation
{
    public int Friends { get; set; }
    public int Dead { get; set; }
    public int OffWorlders { get; set; }
    public int Total { get; set; }
    public List<IPlayerCharacter> Players { get; set; } = [];
}
