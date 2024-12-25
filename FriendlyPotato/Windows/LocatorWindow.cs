using System;
using System.Numerics;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace FriendlyPotato.Windows;

public class LocatorWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public LocatorWindow(FriendlyPotato plugin) : base(
        "Friendly Potato Hunt Locator###FriendlyPotatoHuntLocator")
    {
        Flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoBackground;
        SizeCondition = ImGuiCond.Always;
        Size = new Vector2(300f, 600f);
        PositionCondition = ImGuiCond.Always;
        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        base.PreDraw();

        var screenPos = ImGuiHelpers.MainViewport.Pos;
        var screenSize = ImGuiHelpers.MainViewport.Size;
        PositionCondition = ImGuiCond.Always;
        Position = screenPos + new Vector2(screenSize.X / 2, screenSize.Y / 2) + new Vector2(
                       screenSize.X / 100 * configuration.LocatorOffsetX / 2,
                       screenSize.Y / 100 * configuration.LocatorOffsetY / 2);
    }

    public override void Draw()
    {
        if (!configuration.ShowHuntLocator) return;

        foreach (var huntId in FriendlyPotato.VisibleHunts)
        {
            if (!FriendlyPotato.ObjectLocations.TryGetValue(huntId, out var sRank)) continue;

            if (sRank.Distance < 0f) return;

            var arrowImagePath = sRank.Type switch
            {
                ObjectLocation.Variant.ARank => FriendlyPotato.AssetPath("purplearrow.png"),
                ObjectLocation.Variant.BRank => FriendlyPotato.AssetPath("bluearrow.png"),
                _ => FriendlyPotato.AssetPath("arrow.png")
            };

            var texture = FriendlyPotato.TextureProvider
                                        .GetFromFile(arrowImagePath)
                                        .GetWrapOrDefault();
            if (texture == null)
            {
                FriendlyPotato.PluginLog.Warning("Could not find texture `{0}`", arrowImagePath);
                return;
            }

            var flag = FriendlyPotato.PositionToFlag(sRank.Position);
            ImGui.Text($"{sRank.Name}");
            ImGui.Text($"(x {flag.X} , y {flag.Y}) {sRank.Distance:F1}y");
            DrawImageRotated(texture, sRank.Angle);
            ImGui.Spacing();
        }
    }

    private void DrawImageRotated(IDalamudTextureWrap texture, float angle)
    {
        var drawList = ImGui.GetWindowDrawList();

        var rotation = angle * MathF.PI / 180f; // Replace with your desired rotation angle

        // Get the image size and center position
        var imageSize = new Vector2(texture.Width, texture.Height);
        var center = ImGui.GetCursorScreenPos() + (imageSize / 2);

        // Calculate cosine and sine of the rotation angle
        var cos = MathF.Cos(rotation);
        var sin = MathF.Sin(rotation);

        // Define half-size for convenience
        var halfSize = imageSize / 2;

        // Calculate rotated vertex positions
        var vertices = new Vector2[4];
        vertices[0] = center + new Vector2((-halfSize.X * cos) - (-halfSize.Y * sin),
                                           (-halfSize.X * sin) + (-halfSize.Y * cos));
        vertices[1] = center + new Vector2((halfSize.X * cos) - (-halfSize.Y * sin),
                                           (halfSize.X * sin) + (-halfSize.Y * cos));
        vertices[2] = center + new Vector2((halfSize.X * cos) - (halfSize.Y * sin),
                                           (halfSize.X * sin) + (halfSize.Y * cos));
        vertices[3] = center + new Vector2((-halfSize.X * cos) - (halfSize.Y * sin),
                                           (-halfSize.X * sin) + (halfSize.Y * cos));

        drawList.AddImageQuad(texture.ImGuiHandle, vertices[0], vertices[1], vertices[2], vertices[3]);

        ImGui.Dummy(new Vector2(texture.Width, texture.Height));
    }
}
