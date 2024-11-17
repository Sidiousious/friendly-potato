using System.Numerics;

namespace FriendlyPotato;

public class ObjectLocation
{
    public enum Variant
    {
        SRank,
        ARank
    }

    public float Angle { get; init; } = -1000f;
    public double Distance { get; init; } = -1f;
    public Vector2 Position { get; init; } = Vector2.Zero;
    public string Name { get; init; } = string.Empty;
    public Variant Type { get; init; } = Variant.SRank;
}
