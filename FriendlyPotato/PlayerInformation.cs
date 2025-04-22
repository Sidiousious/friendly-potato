using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
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
    public int Raised { get; set; }
    public ImmutableList<PlayerCharacterDetails> Players { get; set; } = [];
    public ConcurrentDictionary<string, DateTime> SeenHistory { get; } = new();

    public void ClearOld()
    {
        if (Players.Count < 30)
        {
            SeenHistory.Clear();
            return;
        }

        var old = SeenHistory.Keys.ToImmutableArray();
        foreach (var k in old)
            if (SeenHistory.TryGetValue(k, out var date) && DateTime.Now - date > TimeSpan.FromSeconds(30))
                SeenHistory.TryRemove(k, out _);
    }
}

public enum PlayerCharacterKind
{
    None = 0x0,
    Friend = 0x1,
    Dead = 0x2,
    OffWorlder = 0x4,
    Doomed = 0x8
}

public class PlayerCharacterDetails
{
    public IPlayerCharacter Character { get; init; } = null!;
    public ushort Kind { get; set; }
    public bool Raised { get; set; }
    public bool Doomed { get; set; }

    public string JobAbbreviation => Character.ClassJob.ValueNullable?.Abbreviation.ToString() ?? string.Empty;

    public void AddKind(PlayerCharacterKind kind)
    {
        Kind |= (ushort)kind;
    }

    public bool IsKind(PlayerCharacterKind kind)
    {
        return ((ushort)kind & Kind) != 0;
    }

    public override string ToString()
    {
        return $"Character: {Character.Name}, Kind: {Kind}";
    }
}
