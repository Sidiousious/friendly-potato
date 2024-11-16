using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FriendlyPotato.Windows;

namespace FriendlyPotato;

// ReSharper disable once ClassNeverInstantiated.Global - instantiated by Dalamud
public sealed class FriendlyPotato : IDalamudPlugin
{
    private const string CommandName = "/friendlypotato";
    private const string DebugCommandName = "/fpotdbg";
    private const string VisiblePlayers = "Visible Players: ";
    private const string Friends = "Friends: ";
    private const string Dead = "Dead: ";
    private const string OffWorlders = "Off-Worlders: ";
    private const string Wees = "Wees: ";
    private const string Doomed = "Doomed: ";

    private readonly Payload deadIcon = new IconPayload(BitmapFontIcon.Disconnecting);
    private readonly Payload doomedIcon = new IconPayload(BitmapFontIcon.OrangeDiamond);
    private readonly Payload dtrSeparator = new TextPayload("  ");
    private readonly Payload friendIcon = new IconPayload(BitmapFontIcon.Returner);
    private readonly Payload offWorldIcon = new IconPayload(BitmapFontIcon.CrossWorld);
    private readonly Payload playerIcon = new IconPayload(BitmapFontIcon.AnyClass);

    private readonly PlayerInformation playerInformation = new();

    private readonly uint[] sRanks =
    [
        // A Realm Reborn
        0xC89, // Laideronnette
        0xC8A, // Wulgaru
        0xC8B, // Mindflayer
        0xC8C, // Theda
        0xC8D, // Zona
        0xC8E, // Brontes
        0xC8F, // Lamp
        0xC90, // TODO: confirm this is nuny
        0xC91, // Minhocao
        0xC92, // Croque
        0xC93, // Croak
        0xC94, // Garlic
        0xC95, // Bonna
        0xC96, // Nandi
        0xC97, // Cherno
        0xC98, // Safat
        0xC99, // TODO: confirm this is agrippa

        // Heavensward
        0x11C7, // KB
        0x11C8, // TODO: confirm this is senmurv
        0x11C9, // PR
        0x11CA, // Gandy
        0x11CB, // BoP
        0x11CC, // Leucrotta

        // Stormblood
        0x1AB1, // Okina
        0x1AB2, // Gamma
        0x1AB3, // TODO: confirm this is Orghana
        0x1AB4, // Udum
        0x1AB5, // BC
        0x1AB6, // SnL

        // Shadowbringers
        0x281E, // Agla
        0x2838, // Ixtab
        0x2852, // Gunitt
        0x2873, // Tarch
        0x288E, // Tyger
        0x298A, // FP

        // Endwalker
        0x360A, // Burf
        0x3670, // Sphat
        0x35BE, // Armstrong
        0x35BD, // Ruminator
        0x35DC, // Ophi
        0x35DB, // Narrow-rift

        // Dawntrail
        0x452A, // Abhorrent
        0x4582, // Ihnu
        0x4233, // Neyo
        0x43DD, // Sans
        0x416D, // Atticus
        0x4397  // Forecaster

        // 17228 - hammerheads NW koza for testing
    ];

    public readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version!;
    private readonly Payload weesIcon = new IconPayload(BitmapFontIcon.Meteor);

    public readonly WindowSystem WindowSystem = new("FriendlyPotato");

    public FriendlyPotato()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        var arrowImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "arrow.png");

        ConfigWindow = new ConfigWindow(this);
        PlayerListWindow = new PlayerListWindow(this, playerInformation);
        LocatorWindow = new LocatorWindow(this, arrowImagePath);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(PlayerListWindow);
        WindowSystem.AddWindow(LocatorWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open settings for FriendlyPotato"
        });

        CommandManager.AddHandler(DebugCommandName, new CommandInfo(OnDebugCommand)
        {
            HelpMessage = "Print some info in debug log"
        });

        PluginInterface.UiBuilder.Draw += DrawUi;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += PlayerListWindow.Toggle;

        ClientState.Logout += Logout;

        NearbyDtrBarEntry = DtrBar.Get("FriendlyPotatoNearby");
        NearbyDtrBarEntry.OnClick += () =>
        {
            if (KeyState[VirtualKey.CONTROL])
            {
                ToggleConfigUi();
                return;
            }

            PlayerListWindow.Toggle();
        };

        Framework.Update += FrameworkOnUpdateEvent;
    }

    public static ObjectLocation SRank { get; set; } = new();

    [PluginService]
    internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService]
    internal static ICommandManager CommandManager { get; private set; } = null!;

    [PluginService]
    internal static IFramework Framework { get; private set; } = null!;

    [PluginService]
    internal static IDtrBar DtrBar { get; private set; } = null!;

    [PluginService]
    internal static IObjectTable ObjectTable { get; private set; } = null!;

    [PluginService]
    internal static IClientState ClientState { get; private set; } = null!;

    [PluginService]
    internal static ITargetManager TargetManager { get; private set; } = null!;

    [PluginService]
    internal static IPluginLog PluginLog { get; private set; } = null!;

    [PluginService]
    internal static IKeyState KeyState { get; private set; } = null!;

    [PluginService]
    internal static ITextureProvider TextureProvider { get; private set; } = null!;

    [PluginService]
    internal static IChatGui ChatGui { get; private set; } = null!;

    public Configuration Configuration { get; init; }
    private ConfigWindow ConfigWindow { get; init; }
    private PlayerListWindow PlayerListWindow { get; init; }
    private LocatorWindow LocatorWindow { get; init; }
    private IDtrBarEntry NearbyDtrBarEntry { get; set; }

    public void Dispose()
    {
        Framework.Update -= FrameworkOnUpdateEvent;
        ClientState.Logout -= Logout;

        WindowSystem.RemoveAllWindows();

        NearbyDtrBarEntry.Remove();
        ConfigWindow.Dispose();
        PlayerListWindow.Dispose();
        LocatorWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(DebugCommandName);
    }

    private void Logout(int _, int __)
    {
        if (PlayerListWindow.IsOpen) PlayerListWindow.Toggle();
    }

    private void FrameworkOnUpdateEvent(IFramework framework)
    {
        EnsureIsOnFramework();
        if (!ClientState.IsLoggedIn || ClientState.LocalPlayer == null) return;
        UpdatePlayerList();
        UpdateDtrBar();

        if (!LocatorWindow.IsOpen) LocatorWindow.Toggle();

        var sRank = ObjectTable.Skip(1).OfType<IBattleNpc>().FirstOrDefault(p => sRanks.Contains(p.DataId));
        if (sRank != null)
        {
            var pos = new Vector2(sRank.Position.X, sRank.Position.Z);
            // Previous tick had none
            if (SRank.Distance < 0) SendChatFlag(pos, $"You sense the presence of a powerful mark... {sRank.Name}", 14);
            SRank = new ObjectLocation
            {
                Angle = (float)CameraAngles.AngleToTarget(sRank, CameraAngles.OwnAimAngle()),
                Distance = DistanceToTarget(sRank),
                Position = pos,
                Name = sRank.Name.ToString()
            };
        }
        else
            SRank = new ObjectLocation();
    }

    private void UpdatePlayerList()
    {
        EnsureIsOnFramework();
        playerInformation.Players = ObjectTable
                                    .Skip(1).OfType<IPlayerCharacter>()
                                    .Where(p => !string.IsNullOrEmpty(p.Name.ToString())).Select(
                                        p => new PlayerCharacterDetails
                                        {
                                            Character = p
                                        }).ToImmutableList();
        UpdatePlayerTypes();
    }

    private static void LogAllPropertiesBeginningWithUnknown<T>(T obj)
    {
        if (obj == null)
        {
            PluginLog.Debug("obj is null");
            return;
        }

        try
        {
            var type = obj.GetType();
            var props = type.GetProperties();
            foreach (var prop in props)
                PluginLog.Debug($"'{prop.Name}' of type {prop.PropertyType.Name}: {GetValueOrDefault(obj, prop)}");
        }
        catch (Exception ex)
        {
            PluginLog.Error(ex.ToString());
        }
    }

    private static object? GetValueOrDefault<T>([DisallowNull] T obj1, PropertyInfo prop)
    {
        try
        {
            return prop.GetValue(obj1);
        }
        catch (TargetInvocationException)
        {
            return "[NO VALUE]";
        }
        catch (Exception ex)
        {
            PluginLog.Error(ex.ToString());
            return "[ERR]";
        }
    }

    private void UpdatePlayerTypes()
    {
        const uint weeEaId = 423;

        var friends = 0;
        var dead = 0;
        var offWorlders = 0;
        // Local player is not included in Players list, so include own wee here
        var wees = ClientState.LocalPlayer!.CurrentMinion?.ValueNullable?.RowId == weeEaId ? 1 : 0;
        var doomed = 0;
        var raised = 0;

        foreach (var player in playerInformation.Players)
        {
            var minion = player.Character.CurrentMinion;
            if (minion?.RowId == weeEaId) wees++;

            if (player.Character.IsDead)
            {
                player.AddKind(PlayerCharacterKind.Dead);
                dead++;
            }

            if (player.Character.HomeWorld.RowId != ClientState.LocalPlayer!.CurrentWorld.RowId)
            {
                player.AddKind(PlayerCharacterKind.OffWorlder);
                offWorlders++;
            }

            if (player.Character.StatusFlags.HasFlag(StatusFlags.Friend))
            {
                player.AddKind(PlayerCharacterKind.Friend);
                friends++;
            }

            // TODO: verify StatusList still works and statusIds have not changed
            foreach (var status in player.Character.StatusList)
            {
                if (Configuration.DebugStatuses && status.RemainingTime > 0)
                {
                    PluginLog.Verbose(
                        $"Player {player.Character.Name} @ {player.Character.HomeWorld} has status {status.StatusId} - remaining: {status.RemainingTime}");
                }

                switch (status.StatusId)
                {
                    case 148 or 1140:
                        player.Raised = true;
                        raised++;
                        break;
                    case 1970:
                        player.Doomed = true;
                        player.AddKind(PlayerCharacterKind.Doomed);
                        doomed++;
                        break;
                }
            }
        }

        playerInformation.Friends = friends;
        playerInformation.Dead = dead;
        playerInformation.OffWorlders = offWorlders;
        playerInformation.Wees = wees;
        playerInformation.Doomed = doomed;
        playerInformation.Raised = raised;
    }

    private void UpdateDtrBar()
    {
        // const string dtrSeparator = "  ";
        const char tooltipSeparator = '\n';

        if (!Configuration.DtrEnabled)
        {
            NearbyDtrBarEntry.Shown = false;
            return;
        }

        NearbyDtrBarEntry.Shown = true;
        EnsureIsOnFramework();

        var tooltip = new StringBuilder("");
        List<Payload> payloads = [];
        if (Configuration.DtrTotalEnabled)
        {
            tooltip.Append(tooltipSeparator).Append(VisiblePlayers).Append(playerInformation.Total);
            payloads.Add(dtrSeparator);
            payloads.Add(playerIcon);
            payloads.Add(new TextPayload(playerInformation.Total.ToString()));
        }

        if (Configuration.DtrFriendsEnabled)
        {
            tooltip.Append(tooltipSeparator).Append(Friends).Append(playerInformation.Friends);
            payloads.Add(dtrSeparator);
            payloads.Add(friendIcon);
            payloads.Add(new TextPayload(playerInformation.Friends.ToString()));
        }

        if (Configuration.DtrOffWorldEnabled)
        {
            tooltip.Append(tooltipSeparator).Append(OffWorlders).Append(playerInformation.OffWorlders);
            payloads.Add(dtrSeparator);
            payloads.Add(offWorldIcon);
            payloads.Add(new TextPayload(playerInformation.OffWorlders.ToString()));
        }

        if (Configuration.DtrDeadEnabled)
        {
            tooltip.Append(tooltipSeparator).Append(Dead).Append(playerInformation.Dead);
            payloads.Add(dtrSeparator);
            payloads.Add(deadIcon);
            payloads.Add(new TextPayload(playerInformation.Dead.ToString()));
        }

        if (Configuration.DtrDoomEnabled)
        {
            tooltip.Append(tooltipSeparator).Append(Doomed).Append(playerInformation.Doomed);
            payloads.Add(dtrSeparator);
            payloads.Add(doomedIcon);
            payloads.Add(new TextPayload(playerInformation.Doomed.ToString()));
        }

        if (Configuration.DtrShowWees)
        {
            tooltip.Append(tooltipSeparator).Append(Wees).Append(playerInformation.Wees);
            payloads.Add(dtrSeparator);
            payloads.Add(weesIcon);
            payloads.Add(new TextPayload(playerInformation.Wees.ToString()));
        }

        NearbyDtrBarEntry.Text = new SeString(payloads.Skip(1).ToList());
        NearbyDtrBarEntry.Tooltip = new SeString(new TextPayload(tooltip.ToString().Trim()));
    }

    public static double DistanceToTarget(IGameObject target)
    {
        return Vector2.Distance(
            new Vector2(ClientState.LocalPlayer!.Position.X,
                        ClientState.LocalPlayer!.Position.Z),
            new Vector2(target.Position.X, target.Position.Z));
    }

    public static Vector2 PositionToFlag(Vector2 position)
    {
        var scale = 100f;
        // HW zones have different scale
        if (ClientState.MapId is >= 397 and <= 402) scale = 95f;

        return new Vector2(ScaleCoord(position.X), ScaleCoord(position.Y));

        float ScaleCoord(float coord)
        {
            return (float)Math.Floor(((2048f / scale) + (coord / 50f) + 1f) * 10) / 10f;
        }
    }

    private static void SendChatFlag(Vector2 position, string text, ushort colorKey = 0)
    {
        var flagCoords = PositionToFlag(position);
        var mapLink = SeString.CreateMapLink(ClientState.TerritoryType, ClientState.MapId, flagCoords.X, flagCoords.Y);
        var message = new SeStringBuilder();
        message.AddUiForeground(colorKey);
        message.AddText(text);
        message.AddUiForegroundOff();
        message.AddText(" - ");
        message.AddUiForeground(64); // White for the flag
        message.Append(mapLink);
        message.AddUiForegroundOff();
        ChatGui.Print(message.BuiltString);
    }

    private static void EnsureIsOnFramework()
    {
        if (!Framework.IsInFrameworkUpdateThread)
            throw new InvalidOperationException("This method must be called from the framework update thread.");
    }

    private void OnCommand(string command, string args)
    {
        PlayerListWindow.Toggle();
    }

    private void OnDebugCommand(string command, string args)
    {
        var pos = ClientState.LocalPlayer!.Position;
        var posFlat = new Vector2(pos.X, pos.Z);
        PluginLog.Debug($"Current Position: {posFlat} - {PositionToFlag(posFlat)}");
        PluginLog.Debug("Current World");
        LogAllPropertiesBeginningWithUnknown(ClientState.LocalPlayer!.CurrentWorld.Value);
        var target = ClientState.LocalPlayer!.TargetObject;
        if (target is IBattleNpc npc) PluginLog.Debug($"Name: {npc.Name} - NameID: {npc.DataId}");

        if (target is IBattleChara targetCharacter)
        {
            PluginLog.Debug($"Statuses {targetCharacter.StatusList.Length}");
            PluginLog.Debug(
                $"{string.Join(", ", targetCharacter.StatusList.Select(x => $"{x.StatusId} remaining {x.RemainingTime}"))}");
        }

        PluginLog.Debug($"SRank: {SRank.Angle} {SRank.Distance}");
    }

    private void DrawUi()
    {
        WindowSystem.Draw();
    }

    public void ToggleConfigUi()
    {
        ConfigWindow.Toggle();
    }
}
