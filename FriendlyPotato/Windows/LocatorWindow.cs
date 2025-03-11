using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace FriendlyPotato.Windows;

public sealed class LocatorWindow : Window, IDisposable
{
    private const double HealthTrackInterval = 1.5;
    private readonly Configuration configuration;
    private readonly Dictionary<uint, HealthTrack> healths = new();

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

            var killTimeEstimate = EstimateKillTime(huntId, sRank.Health);

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

            var estimatedTime = killTimeEstimate > 0 ? $"(est. {killTimeEstimate:F1}s)" : "";
            var hp = sRank.Health < 100f ? $"{sRank.Health:F1}%%" : $"{sRank.Health:F0}%%";
            var flag = FriendlyPotato.PositionToFlag(sRank.Position);
            ImGui.Text($"{sRank.Name}");
            ImGui.Text($"(x {flag.X} , y {flag.Y}) {sRank.Distance:F1}y");
            ImGui.Text($"HP {hp} {estimatedTime}");
            DrawImageRotated(texture, sRank.Angle);
            ImGui.Spacing();
        }
    }

    private double EstimateKillTime(uint huntId, float currentHealth)
    {
        if (healths.TryGetValue(huntId, out var health))
        {
            if (DateTime.Now - health.When > TimeSpan.FromSeconds(HealthTrackInterval))
                health.AddHealthDiff(currentHealth);
        }
        else
        {
            health = new HealthTrack
            {
                Health = currentHealth,
                When = DateTime.Now
            };
            healths.Add(huntId, health);
        }

        return health.EstimatedKillTime();
    }

    private static void DrawImageRotated(IDalamudTextureWrap texture, float angle)
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

    private class HealthTrack
    {
        private const int TrackedWindows = 20;
        private readonly float[] healthDifferences = new float[TrackedWindows];
        private readonly TimeSpan[] healthTrackDurations = new TimeSpan[TrackedWindows];
        private int filled;
        public float Health;
        public DateTime When;

        public void AddHealthDiff(float newHealth)
        {
            if (newHealth > Health)
            {
                Health = newHealth;
                When = DateTime.Now;
                filled = 0;
                return;
            }

            for (var i = filled - 1; i > 0; i--)
            {
                healthDifferences[i] = healthDifferences[i - 1];
                healthTrackDurations[i] = healthTrackDurations[i - 1];
            }

            var diff = Health - newHealth;
            var now = DateTime.Now;

            healthDifferences[0] = diff;
            healthTrackDurations[0] = now - When;
            When = now;
            Health = newHealth;
            if (filled < TrackedWindows) filled++;
        }

        public double EstimatedKillTime()
        {
            var healthDifference = 0f;
            var time = TimeSpan.Zero;
            for (var i = 0; i < filled; i++)
            {
                healthDifference += healthDifferences[i];
                time += healthTrackDurations[i];
            }

            if (healthDifference == 0f) return 0;

            var timeToKill = Health / healthDifference * time.TotalMilliseconds / 1000f;
            return timeToKill;
        }
    }
}
