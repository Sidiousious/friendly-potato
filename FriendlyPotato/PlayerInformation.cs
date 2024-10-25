using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;

namespace FriendlyPotato;

public class PlayerInformation
{
    public int Friends { get; set; }
    public int Dead { get; set; }
    public int OffWorlders { get; set; }
    public int Total => Players.Count;
    public int Doomed { get; set; }
    public int Wees { get; set; }
    public ImmutableList<PlayerCharacterDetails> Players { get; set; } = [];
}

public enum PlayerCharacterKind
{
    None = 0x0,
    Friend = 0x1,
    Dead = 0x2,
    OffWorlder = 0x4,
    Doomed = 0x8,
}

public class PlayerCharacterDetails
{
    public IPlayerCharacter Character { get; init; } = null!;
    public ushort Kind { get; set; }
    public bool Raised { get; set; }
    public bool Doomed { get; set; }
    
    public void AddKind(PlayerCharacterKind kind) => Kind |= (ushort)kind;
    public bool IsKind(PlayerCharacterKind kind) => ((ushort)kind & Kind) != 0;

    public string JobAbbreviation => Character.ClassJob.GameData?.Abbreviation.ToString() ?? string.Empty;

    public override string ToString()
    {
        return $"Character: {Character.Name}, Kind: {Kind}";
    }
}
