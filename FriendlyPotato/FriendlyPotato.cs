using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
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
using FFXIVClientStructs.FFXIV.Client.UI;
using FriendlyPotato.Windows;
using Lumina.Excel.Sheets;

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
    private const ushort SeColorWineRed = 14;
    private const ushort SeColorWhite = 64;
    private readonly uint[] aRanks;

    private readonly Payload deadIcon = new IconPayload(BitmapFontIcon.Disconnecting);
    private readonly Payload doomedIcon = new IconPayload(BitmapFontIcon.OrangeDiamond);
    private readonly Payload dtrSeparator = new TextPayload("  ");
    private readonly Payload friendIcon = new IconPayload(BitmapFontIcon.Returner);
    private readonly Payload offWorldIcon = new IconPayload(BitmapFontIcon.CrossWorld);
    private readonly Payload playerIcon = new IconPayload(BitmapFontIcon.AnyClass);

    private readonly PlayerInformation playerInformation = new();

    private readonly uint[] sRanks;

    public readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version!;
    private readonly Payload weesIcon = new IconPayload(BitmapFontIcon.Meteor);

    public readonly WindowSystem WindowSystem = new("FriendlyPotato");

    public FriendlyPotato()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ConfigWindow = new ConfigWindow(this);
        PlayerListWindow = new PlayerListWindow(this, playerInformation);
        LocatorWindow = new LocatorWindow(this);

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

        var hunts = NotoriousMonsters();
        sRanks = hunts.SRanks;
        aRanks = hunts.ARanks;
    }

    public static ConcurrentDictionary<uint, ObjectLocation> ObjectLocations { get; private set; } = [];
    public static ImmutableList<uint> VisibleHunts { get; private set; } = ImmutableList<uint>.Empty;

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

    [PluginService]
    internal static IDataManager DataManager { get; private set; } = null!;

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

    public static string AssetPath(string assetName)
    {
        return Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, assetName);
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

        UpdateVisibleHunts();
    }

    private void UpdateVisibleHunts()
    {
        EnsureIsOnFramework();

        List<uint> visible = [];
        foreach (var mob in ObjectTable.Skip(1).OfType<IBattleNpc>())
        {
            var pos = new Vector2(mob.Position.X, mob.Position.Z);
            var previouslyVisible = VisibleHunts.Contains(mob.DataId);

            ObjectLocation.Variant variant;
            if (sRanks.Contains(mob.DataId))
            {
                variant = ObjectLocation.Variant.SRank;

                if (!previouslyVisible)
                {
                    if (Configuration.ChatLocatorEnabled)
                        SendChatFlag(pos, $"You sense the presence of a powerful mark... {mob.Name}", SeColorWineRed);

                    if (Configuration.SRankSoundEnabled)
                        UIGlobals.PlayChatSoundEffect(2);
                }
            }
            else if (aRanks.Contains(mob.DataId))
            {
                variant = ObjectLocation.Variant.ARank;

                if (!previouslyVisible)
                {
                    if (Configuration.ChatLocatorARanksEnabled)
                        SendChatFlag(pos, $"A-rank detected... {mob.Name}", 1);

                    if (Configuration.ARankSoundEnabled)
                        UIGlobals.PlayChatSoundEffect(2);
                }
            }
            else
            {
                // Not interested
                continue;
            }

            var objLoc = new ObjectLocation
            {
                Angle = (float)CameraAngles.AngleToTarget(mob, CameraAngles.OwnAimAngle()),
                Distance = DistanceToTarget(mob),
                Position = pos,
                Name = mob.Name.ToString(),
                Type = variant
            };
            ObjectLocations[mob.DataId] = objLoc;
            visible.Add(mob.DataId);
        }
#if DEBUG
        if (Configuration.DebugList)
        {
            var target = ClientState.LocalPlayer!.TargetObject;
            if (target is IBattleNpc mob)
            {
                var pos = new Vector2(mob.Position.X, mob.Position.Z);

                var objLoc = new ObjectLocation
                {
                    Angle = (float)CameraAngles.AngleToTarget(mob, CameraAngles.OwnAimAngle()),
                    Distance = DistanceToTarget(mob),
                    Position = pos,
                    Name = mob.Name.ToString(),
                    Type = ObjectLocation.Variant.SRank
                };
                visible.Add(mob.DataId);
                ObjectLocations[mob.DataId] = objLoc;
            }
        }
#endif

        VisibleHunts = visible.ToImmutableList();
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
        if (ClientState.MapId is >= 211 and <= 216) scale = 95f;

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
        message.AddUiForeground(SeColorWhite);
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
        var target = ClientState.LocalPlayer!.TargetObject;
        if (target is IBattleNpc npc)
        {
            PluginLog.Debug(
                $"Name: {npc.Name} - DataId: {npc.DataId} - is A rank? {aRanks.Contains(npc.DataId)} - is S rank? {sRanks.Contains(npc.DataId)}");
        }

        if (target is IBattleChara targetCharacter)
        {
            PluginLog.Debug($"Statuses {targetCharacter.StatusList.Length}");
            PluginLog.Debug(
                $"{string.Join(", ", targetCharacter.StatusList.Select(x => $"{x.StatusId} remaining {x.RemainingTime}"))}");
        }

        PluginLog.Debug($"Visible Hunts: {string.Join(", ", VisibleHunts)}");
    }

    private (uint[] SRanks, uint[] ARanks) NotoriousMonsters()
    {
        const byte sRank = 3;
        const byte aRank = 2;
        return (Ranks(sRank), Ranks(aRank));

        uint[] Ranks(byte typeOfRank)
        {
            return DataManager.GetExcelSheet<NotoriousMonster>().Where(m => m.Rank == typeOfRank)
                              .Select(m => m.BNpcBase.ValueNullable?.RowId).Where(m => m.HasValue).Select(m => m!.Value)
                              .ToArray();
        }
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
