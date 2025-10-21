using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;

namespace FriendlyPotato;

public class ObjectLocation
{
    public enum Variant
    {
        SRank,
        ARank,
        BRank,
        Fate,
        Treasure
    }

    public float Angle { get; init; } = -1000f;
    public double Distance { get; init; } = -1f;
    public Vector2 Position { get; init; } = Vector2.Zero;
    public float Height { get; init; } = 0f;
    public string Name { get; init; } = string.Empty;
    public Variant Type { get; init; } = Variant.SRank;
    public float Health { get; init; } = -1f;
    public string? Target { get; init; } = null;
    public StatusFlags Status { get; init; } = 0;
    public long Duration { get; init; } = -1;
}
