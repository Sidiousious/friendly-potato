using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Network;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FriendlyPotato.Windows;
using Lumina.Excel.Sheets;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace FriendlyPotato;

// ReSharper disable once ClassNeverInstantiated.Global - instantiated by Dalamud
public sealed partial class FriendlyPotato : IDalamudPlugin
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
    public const uint FateOffset = 0x10000000;
    public const uint TreasureOffset = 0x20000000;
    private const uint RenderFlagHidden = 2048;
    private readonly uint[] aRanks;
    private readonly uint[] bRanks;

    private readonly Payload deadIcon = new IconPayload(BitmapFontIcon.Disconnecting);
    private readonly Payload doomedIcon = new IconPayload(BitmapFontIcon.OrangeDiamond);
    private readonly Payload dtrSeparator = new TextPayload("  ");
    private readonly Payload friendIcon = new IconPayload(BitmapFontIcon.Returner);

    private readonly ByteColor highlightColor = new() { R = 240, G = 0, B = 0, A = 155 };

    private readonly uint[] interestingFates =
    [
        // Each unique position has its own fate id
        1862, // Drink
        1871, // Snek
        1922, // Mica
        831,  // Cerf's up
        877,  // Prey online
        878,  // Prey online
        879,  // Prey online
        1431, // Archie 1/2
        1432, // Archie 2/2
        196,  // Odin central
        197,  // Odin central
        198,  // Odin east
        199,  // Odin east
        200,  // Odin east
        201,  // Odin south
        202,  // Odin south
        203,  // Odin south
        204,  // Odin south
        205,  // Odin north
        206,  // Odin north
        207,  // Odin north
        1106, // Foxy Lady
        1107, // Foxy Lady
        1108, // Foxy Lady
        1855, // Chi
        505,  // Behemoth 1/2
        506,  // Behemoth 2/2
        1103, // Ixion
        1464, // Formi
        1763, // Dave
        902,  // Coeurlregina 1/3
        903,  // Coeurlregina 2/3
        904,  // Coeurlregina 2/3
        905,  // Coeurlregina 3/3
        906,  // Coeurlregina 3/3
        907,  // Coeurlregina 3/3
        1259  // Tribute
    ];

    private readonly Payload offWorldIcon = new IconPayload(BitmapFontIcon.CrossWorld);
    private readonly Payload playerIcon = new IconPayload(BitmapFontIcon.AnyClass);

    private readonly PlayerInformation playerInformation = new();
    private readonly uint[] sRanks;
    private readonly ByteColor unknownHighlightColor = new() { R = 240, G = 240, B = 50, A = 155 };

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

        RuntimeData = new RuntimeDataManager(PluginInterface, PluginLog);

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
        bRanks = hunts.BRanks;

        GameNetwork.NetworkMessage += HandleGameNetworkMessage;

        ClientState.TerritoryChanged += TerritoryChanged;
    }

    public static RuntimeDataManager RuntimeData { get; private set; } = null!;

    public static ConcurrentDictionary<uint, ObjectLocation> ObjectLocations { get; private set; } = [];
    public static ImmutableList<uint> VisibleHunts { get; private set; } = ImmutableList<uint>.Empty;
    public static ImmutableList<uint> VisibleFates { get; private set; } = ImmutableList<uint>.Empty;
    public static ImmutableList<uint> VisibleTreasure { get; private set; } = ImmutableList<uint>.Empty;

    [PluginService]
    internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService]
    internal static IGameNetwork GameNetwork { get; private set; } = null!;

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

    [PluginService]
    internal static IGameGui GameGui { get; private set; } = null!;

    [PluginService]
    internal static IFateTable FateTable { get; private set; } = null!;

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

        GameNetwork.NetworkMessage -= HandleGameNetworkMessage;

        ClientState.TerritoryChanged -= TerritoryChanged;
    }

    private static void TerritoryChanged(ushort _)
    {
        ObjectLocations.Clear();
    }

    private void HandleGameNetworkMessage(
        nint ptr, ushort code, uint id, uint actorId, NetworkMessageDirection direction)
    {
        if (Configuration.DebugList)
            PluginLog.Debug($"ptr {ptr} - code {code}  - id {id} - actorId {actorId} - direction {direction}");
        if (direction == NetworkMessageDirection.ZoneDown)
        {
            switch (code)
            {
                case (ushort)OpCode.LinkshellDown: // Local linkshell list download
                    Framework.RunOnTick(ProcessLinkshellUsers, TimeSpan.FromMilliseconds(100));
                    break;
                case (ushort)OpCode.CrossworldLinkshellDown: // CWLS list download
                    Framework.RunOnTick(ProcessCrossworldLinkshellUsers, TimeSpan.FromMilliseconds(100));
                    break;
            }
        }
    }

    public static string AssetPath(string assetName)
    {
        return Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, assetName);
    }

    private void Logout(int _, int __)
    {
        if (PlayerListWindow.IsOpen) PlayerListWindow.Toggle();
        Framework.RunOnFrameworkThread(() =>
        {
            VisibleHunts = ImmutableList<uint>.Empty;
            ObjectLocations = [];
        });
    }

    private void FrameworkOnUpdateEvent(IFramework framework)
    {
        EnsureIsOnFramework();
        if (!ClientState.IsLoggedIn || ClientState.LocalPlayer == null) return;
        UpdatePlayerList();
        UpdateDtrBar();

        if (!LocatorWindow.IsOpen) LocatorWindow.Toggle();

        UpdateVisibleHunts();
        UpdateVisibleFates();
        UpdateVisibleTreasure();
        HighlightLinkshellUsers();
        HighlightCrossworldLinkshellUsers();
    }

    private static bool IsTreasureHuntArea()
    {
        return ClientState.MapId == 967; // Occult Crescent
    }

    private static unsafe bool IsVisible(IGameObject gameObject)
    {
        var csObject = (GameObject*)gameObject.Address;
        return (csObject->RenderFlags & RenderFlagHidden) == 0;
    }

    private static bool IsTreasureCoffer(IGameObject o)
    {
        switch (o.ObjectKind)
        {
            case ObjectKind.Treasure:
            case ObjectKind.EventObj
                when o.DataId is 2010139 /* Name "Destination" carrot */ or 2014695 /* Survey Point */
                         or 2014743 /* Pot coffer */:
                return IsVisible(o);
            default:
                return false;
        }
    }

    private static string TreasureName(IGameObject o)
    {
        return o.DataId switch
        {
            2010139 => "Carrot",
            2014695 => "Survey Point",
            _ => o.Name.TextValue
        };
    }

    private void UpdateVisibleTreasure()
    {
        EnsureIsOnFramework();

        List<uint> visible = [];
        if (IsTreasureHuntArea())
        {
            foreach (var obj in ObjectTable.Where(IsTreasureCoffer))
            {
                if (!VisibleTreasure.Contains(obj.DataId) && Configuration.TreasureSoundEnabled)
                    UIGlobals.PlayChatSoundEffect(6);

                var objLoc = new ObjectLocation
                {
                    Angle = (float)CameraAngles.AngleToTarget(obj.Position, CameraAngles.OwnAimAngle()),
                    Distance = DistanceToTarget(obj.Position),
                    Position = new Vector2(obj.Position.X, obj.Position.Z),
                    Name = TreasureName(obj),
                    Type = ObjectLocation.Variant.Treasure
                };
                ObjectLocations[obj.DataId + TreasureOffset] = objLoc;
                visible.Add(obj.DataId);
            }
        }

        VisibleTreasure = visible.ToImmutableList();
    }

    private void UpdateVisibleFates()
    {
        EnsureIsOnFramework();

        List<uint> visible = [];
        // Process interesting fates that have a position set
        foreach (var fate in FateTable.Where(f => !f.Position.Equals(Vector3.Zero) &&
                                                  interestingFates.Contains(f.FateId)))
        {
            var pos = new Vector2(fate.Position.X, fate.Position.Z);

            if (!VisibleFates.Contains(fate.FateId))
            {
                if (Configuration.FateSoundEnabled) UIGlobals.PlayChatSoundEffect(3);

                if (Configuration.FateChatEnabled)
                    SendChatFlag(pos, GetInstance(), $"A FATE catches your eye... {fate.Name.TextValue}", SeColorWhite);
            }

            var objLoc = new ObjectLocation
            {
                Angle = (float)CameraAngles.AngleToTarget(fate.Position, CameraAngles.OwnAimAngle()),
                Distance = DistanceToTarget(fate.Position),
                Position = pos,
                Name = fate.Name.TextValue,
                Type = ObjectLocation.Variant.Fate,
                Health = 100f - fate.Progress,
                Duration = fate.TimeRemaining
            };
            ObjectLocations[fate.FateId + FateOffset] = objLoc;
            visible.Add(fate.FateId);
        }

        VisibleFates = visible.ToImmutableList();
    }

    private void UpdateVisibleHunts()
    {
        EnsureIsOnFramework();

        List<uint> visible = [];
        foreach (var mob in ObjectTable.Skip(1).OfType<IBattleNpc>())
        {
            var pos = new Vector2(mob.Position.X, mob.Position.Z);
            var previouslyVisible = VisibleHunts.Contains(mob.DataId);

            var instance = GetInstance();

            ObjectLocation.Variant variant;
            if (sRanks.Contains(mob.DataId))
            {
                variant = ObjectLocation.Variant.SRank;

                if (!previouslyVisible)
                {
                    if (Configuration.SChatLocatorEnabled)
                    {
                        SendChatFlag(pos, instance, $"You sense the presence of a powerful mark... {mob.Name}",
                                     SeColorWineRed);
                    }

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
                        SendChatFlag(pos, instance, $"A-rank detected... {mob.Name}", 1);

                    if (Configuration.ARankSoundEnabled)
                        UIGlobals.PlayChatSoundEffect(2);
                }
            }
            else if (bRanks.Contains(mob.DataId))
                variant = ObjectLocation.Variant.BRank;
            else
            {
                // Not interested
                continue;
            }

            string? targetPlayer = null;
            if (ObjectLocations.TryGetValue(mob.DataId, out var previousLocation))
            {
                // Preserving previous target
                if (previousLocation.Target is not null)
                    targetPlayer = previousLocation.Target;

                // Enter combat ping
                if (!previousLocation.Status.HasFlag(StatusFlags.InCombat) &&
                    mob.StatusFlags.HasFlag(StatusFlags.InCombat))
                {
                    if (Configuration.PingOnPull)
                        UIGlobals.PlayChatSoundEffect(10);
                }
            }
            // Found in combat ping
            else if (mob.StatusFlags.HasFlag(StatusFlags.InCombat))
            {
                if (Configuration.PingOnPull)
                    UIGlobals.PlayChatSoundEffect(10);
            }

            if (targetPlayer is null && mob.TargetObject is IPlayerCharacter target)
                targetPlayer = $"{target.Name.TextValue}@{HomeWorldName(target.HomeWorld.RowId)}";

            var objLoc = new ObjectLocation
            {
                Angle = (float)CameraAngles.AngleToTarget(mob.Position, CameraAngles.OwnAimAngle()),
                Distance = DistanceToTarget(mob.Position),
                Position = pos,
                Name = mob.Name.TextValue,
                Type = variant,
                Health = 100f * mob.CurrentHp / mob.MaxHp,
                Target = targetPlayer,
                Status = mob.StatusFlags
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
                    Angle = (float)CameraAngles.AngleToTarget(mob.Position, CameraAngles.OwnAimAngle()),
                    Distance = DistanceToTarget(mob.Position),
                    Position = pos,
                    Name = mob.Name.TextValue,
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
                                    .Where(p => !string.IsNullOrEmpty(p.Name.TextValue))
                                    .Select(p => new PlayerCharacterDetails
                                    {
                                        Character = p
                                    }).ToImmutableList();
        foreach (var player in playerInformation.Players)
        {
            var nameKey =
                $"{player.Character.Name.TextValue}@{player.Character.HomeWorld.Value.Name.ToDalamudString().TextValue}";
            RuntimeData.MarkSeen(nameKey, false);
            playerInformation.SeenHistory[nameKey] = DateTime.Now;
        }

        playerInformation.ClearOld();

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

    public static double DistanceToTarget(Vector3 pos)
    {
        return Vector2.Distance(
            new Vector2(ClientState.LocalPlayer!.Position.X,
                        ClientState.LocalPlayer!.Position.Z),
            new Vector2(pos.X, pos.Z));
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

    private static void SendChatFlag(Vector2 position, int? instance, string text, ushort colorKey = 0)
    {
        var flagCoords = PositionToFlag(position);
        var mapLink = SeString.CreateMapLinkWithInstance(ClientState.TerritoryType, ClientState.MapId, instance,
                                                         flagCoords.X, flagCoords.Y);
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
        PluginLog.Debug(
            $"Current Position: {posFlat} - {PositionToFlag(posFlat)} - {ClientState.TerritoryType} - {ClientState.MapId}");
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


        if (target != null)
        {
            PluginLog.Debug(
                $"Name: {target.Name} - DataId: {target.DataId} - {target.EntityId} - Kind: {target.ObjectKind}, {target.SubKind}");
        }

        PluginLog.Debug($"Visible Hunts: {string.Join(", ", VisibleHunts)}");
        PluginLog.Debug($"Visible Fates: {string.Join(", ", VisibleFates)}");

        PluginLog.Debug($"Config dir: {PluginInterface.GetPluginConfigDirectory()}");


        // Time remaining = seconds left
        foreach (var fate in FateTable)
            PluginLog.Debug(
                $"{fate.FateId} - {fate.Name.TextValue} - {fate.TimeRemaining} - {fate.Progress} - {fate.HandInCount}");

        Framework.RunOnFrameworkThread(() =>
        {
            foreach (var o in ObjectTable)
                if (DistanceToTarget(o.Position) < 10)
                {
                    PluginLog.Debug(
                        $"{o.Name} - {o.Position} - {o.EntityId} - {o.DataId} - {o.ObjectKind} - {o.SubKind} - {DistanceToTarget(o.Position)}y");
                    unsafe
                    {
                        var csObj = (GameObject*)o.Address;
                        PluginLog.Debug($"Render flags: {csObj->RenderFlags}");
                    }
                }
        });

        unsafe
        {
            PluginLog.Debug($"Currently in instance: {UIState.Instance()->PublicInstance.InstanceId}");
        }
    }

    private static (uint[] SRanks, uint[] ARanks, uint[] BRanks) NotoriousMonsters()
    {
        const byte sRank = 3;
        const byte aRank = 2;
        const byte bRank = 1;
        return (Ranks(sRank), Ranks(aRank), Ranks(bRank));

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

    private static unsafe void ProcessCrossworldLinkshellUsers()
    {
        if (InfoProxyCrossWorldLinkshell.Instance() == null ||
            AgentCrossWorldLinkshell.Instance() == null ||
            InfoProxyCrossWorldLinkshellMember.Instance() == null)
        {
            PluginLog.Debug("Called to log cwls users but an instance was null");
            return;
        }

        foreach (var c in InfoProxyCrossWorldLinkshellMember.Instance()->CharDataSpan)
        {
            var charName = CharacterFullName(c);
            PluginLog.Debug($"Found cw linkshell user: {charName}");
            Framework.Run(() =>
            {
                if (c.State != InfoProxyCommonList.CharacterData.OnlineStatus.Offline) RuntimeData.MarkSeen(charName);
            });
        }
    }

    private static unsafe string? FindCurrentCrossworldLinkshellUser(string who)
    {
        if (InfoProxyCrossWorldLinkshellMember.Instance() == null)
        {
            PluginLog.Debug("Called to log cwls users but an instance was null");
            return null;
        }

        foreach (var c in InfoProxyCrossWorldLinkshellMember.Instance()->CharDataSpan)
            if (c.NameString == who)
                return CharacterFullName(c);

        return null;
    }

    private static unsafe void ProcessLinkshellUsers()
    {
        if (InfoProxyLinkshell.Instance() == null || InfoProxyLinkshellMember.Instance() == null ||
            AgentLinkshell.Instance() == null)
        {
            PluginLog.Debug("Called to log linkshell users but an instance was null");
            return;
        }

        foreach (var c in InfoProxyLinkshellMember.Instance()->CharDataSpan)
        {
            var charName = CharacterFullName(c);
            PluginLog.Debug($"Found linkshell user: {charName}");
            Framework.Run(() =>
            {
                if (c.State != InfoProxyCommonList.CharacterData.OnlineStatus.Offline) RuntimeData.MarkSeen(charName);
            });
        }
    }

    private unsafe void HighlightCrossworldLinkshellUsers()
    {
        if (!Configuration.HighlightInactive) return;

        var lsAddonPtr = GameGui.GetAddonByName("CrossWorldLinkshell");
        if (lsAddonPtr == nint.Zero) return;

        var lsAddon = (AtkUnitBase*)lsAddonPtr;
        var componentList = lsAddon->GetComponentListById(33);
        if (componentList == null) return;

        foreach (nint i in Enumerable.Range(0, componentList->ListLength))
        {
            var renderer = componentList->ItemRendererList[i].AtkComponentListItemRenderer;
            if (renderer == null) continue;

            var text = (AtkTextNode*)renderer->GetTextNodeById(5);
            if (text == null) continue;

            var name = SeString.Parse(text->GetText().Value).TextValue;
            if (name.StartsWith('(')) continue;
            var runtimeDataName = FindCurrentCrossworldLinkshellUser(NameIconStripper().Replace(name, ""));
            if (runtimeDataName == null) continue;

            var lastSeen =
                RuntimeData.LastSeen(runtimeDataName);
            double days = -1;
            if (lastSeen != null) days = (DateTime.Now - lastSeen.Value).TotalDays;

            if (days < 0)
            {
                text->TextColor = unknownHighlightColor;
                text->SetText($"{name}");
            }
            else if (days >= Configuration.InactivityThreshold)
            {
                text->TextColor = highlightColor;
                text->SetText($"({days:F0}d) {name}");
            }
        }
    }

    private unsafe void HighlightLinkshellUsers()
    {
        if (!Configuration.HighlightInactive) return;

        var lsAddonPtr = GameGui.GetAddonByName("LinkShell");
        if (lsAddonPtr == nint.Zero) return;

        var lsAddon = (AtkUnitBase*)lsAddonPtr;
        var componentList = lsAddon->GetComponentListById(22);
        if (componentList == null) return;

        foreach (nint i in Enumerable.Range(0, componentList->ListLength))
        {
            var renderer = componentList->ItemRendererList[i].AtkComponentListItemRenderer;
            if (renderer == null) continue;

            var text = (AtkTextNode*)renderer->GetTextNodeById(5);
            if (text == null) continue;

            var name = SeString.Parse(text->GetText().Value).TextValue;
            if (name.StartsWith('(')) continue;
            var compareName = NameIconStripper().Replace(name, "");

            var lastSeen =
                RuntimeData.LastSeen(
                    $"{compareName}@{ClientState.LocalPlayer?.HomeWorld.Value.Name.ToDalamudString().TextValue}");
            double days = -1;
            if (lastSeen != null) days = (DateTime.Now - lastSeen.Value).TotalDays;

            if (days < 0)
            {
                text->TextColor = unknownHighlightColor;
                text->SetText($"{name}");
            }
            else if (days >= Configuration.InactivityThreshold)
            {
                text->TextColor = highlightColor;
                text->SetText($"({days:F0}d) {name}");
            }
        }
    }

    private static unsafe int? GetInstance()
    {
        int? instance = null;
        try
        {
            instance = Convert.ToInt32(UIState.Instance()->PublicInstance.InstanceId);
            if (instance.Value == 0) instance = null;
        }
        catch (Exception ex)
        {
            PluginLog.Error(ex.ToString());
        }

        return instance;
    }

    private static string CharacterFullName(InfoProxyCommonList.CharacterData c)
    {
        return $"{c.NameString}@{HomeWorldName(c.HomeWorld)}";
    }

    private static string HomeWorldName(uint id)
    {
        return DataManager.GetExcelSheet<World>().First(w => w.RowId == id).Name.ToDalamudString().TextValue;
    }

    [GeneratedRegex(@"^[^A-Z']")]
    private static partial Regex NameIconStripper();

    private enum OpCode
    {
        LinkshellDown = 814,
        CrossworldLinkshellDown = 116
    }
}
