using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FriendlyPotato.Windows;
using System;
using System.Linq;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;

using Dalamud.Game.Text.SeStringHandling.Payloads;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;

namespace FriendlyPotato;

public sealed class FriendlyPotato : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IDtrBar DtrBar { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog PluginLog { get; private set; } = null!;
    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;

    private const string CommandName = "/friendlypotato";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("FriendlyPotato");
    public readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version!;
    private ConfigWindow ConfigWindow { get; init; }
    private DtrClickWindow PlayerListWindow { get; init; }
    private IDtrBarEntry NearbyDtrBarEntry { get; set; }

    private readonly PlayerInformation playerInformation = new();

    public FriendlyPotato()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ConfigWindow = new ConfigWindow(this);
        PlayerListWindow = new DtrClickWindow(this, playerInformation);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(PlayerListWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open settings for FriendlyPotato"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi += ToggleConfigUI;

        NearbyDtrBarEntry = DtrBar.Get("FriendlyPotatoNearby");
        NearbyDtrBarEntry.OnClick += () =>
        {
            if (KeyState[VirtualKey.CONTROL])
            {
                ToggleConfigUI();
                return;
            }
            PlayerListWindow.Toggle();
        };

        Framework.Update += FrameworkOnUpdateEvent;
    }

    public void Dispose()
    {
        Framework.Update -= FrameworkOnUpdateEvent;

        WindowSystem.RemoveAllWindows();

        NearbyDtrBarEntry.Remove();
        ConfigWindow.Dispose();
        PlayerListWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void FrameworkOnUpdateEvent(IFramework framework)
    {
        if (!ClientState.IsLoggedIn)
        {
            return;
        }
        UpdatePlayerList();
        UpdateDtrBar();
    }

    private void UpdatePlayerList()
    {
        EnsureIsOnFramework();
        playerInformation.Players = ObjectTable
                                    .Where(player => player != ClientState.LocalPlayer && player is IPlayerCharacter)
                                    .Cast<IPlayerCharacter>().Select(p => new PlayerCharacterDetails
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
        var wees = 0;
        var doomed = 0;

        foreach (var player in playerInformation.Players)
        {
            var minion = player.Character.CurrentMinion;
            if (minion?.Id == weeEaId)
            {
                wees++;
            }

            if (player.Character.IsDead)
            {
                player.AddKind(PlayerCharacterKind.Dead);
                dead++;
            }

            if (player.Character.HomeWorld != ClientState.LocalPlayer!.CurrentWorld)
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
    }

    private readonly Payload dtrSeparator = new TextPayload("  ");
    private readonly Payload doomedIcon = new IconPayload(BitmapFontIcon.OrangeDiamond);
    private readonly Payload weesIcon = new IconPayload(BitmapFontIcon.Meteor);
    private readonly Payload offWorldIcon = new IconPayload(BitmapFontIcon.CrossWorld);
    private readonly Payload deadIcon = new IconPayload(BitmapFontIcon.Disconnecting);
    private readonly Payload friendIcon = new IconPayload(BitmapFontIcon.Returner);
    private const string VisiblePlayers = "Visible Players: ";
    private const string Friends = "Friends: ";
    private const string Dead = "Dead: ";
    private const string OffWorlders = "Off-Worlders: ";
    private const string Wees = "Wees: ";
    private const string Doomed = "Doomed: ";

    private void UpdateDtrBar()
    {
        // const string dtrSeparator = "  ";
        const char tooltipSeparator = '\n';

        if (!Configuration.NearbyEnabled)
        {
            NearbyDtrBarEntry.Shown = false;
            return;
        }
        NearbyDtrBarEntry.Shown = true;
        EnsureIsOnFramework();

        var tooltip = new StringBuilder(VisiblePlayers).Append(playerInformation.Total);
        List<Payload> payloads = [new IconPayload(BitmapFontIcon.AnyClass), new TextPayload(playerInformation.Total.ToString())];
        if (Configuration.NearbyFriends)
        {
            tooltip.Append(tooltipSeparator).Append(Friends).Append(playerInformation.Friends);
            payloads.Add(dtrSeparator);
            payloads.Add(friendIcon);
            payloads.Add(new TextPayload(playerInformation.Friends.ToString()));
        }
        
        if (Configuration.NearbyDead)
        {
            tooltip.Append(tooltipSeparator).Append(Dead).Append(playerInformation.Dead);
            payloads.Add(dtrSeparator);
            payloads.Add(deadIcon);
            payloads.Add(new TextPayload(playerInformation.Dead.ToString()));
        }
        
        if (Configuration.NearbyOffWorld)
        {
            tooltip.Append(tooltipSeparator).Append(OffWorlders).Append(playerInformation.OffWorlders);
            payloads.Add(dtrSeparator);
            payloads.Add(offWorldIcon);
            payloads.Add(new TextPayload(playerInformation.OffWorlders.ToString()));
        }

        if (Configuration.NearbyWees)
        {
            tooltip.Append(tooltipSeparator).Append(Wees).Append(playerInformation.Wees);
            payloads.Add(dtrSeparator);
            payloads.Add(weesIcon);
            payloads.Add(new TextPayload(playerInformation.Wees.ToString()));
        }

        if (Configuration.NearbyDoomed)
        {
            tooltip.Append(tooltipSeparator).Append(Doomed).Append(playerInformation.Doomed);
            payloads.Add(dtrSeparator);
            payloads.Add(doomedIcon);
            payloads.Add(new TextPayload(playerInformation.Doomed.ToString()));
        }

        NearbyDtrBarEntry.Text = new SeString(payloads);
        NearbyDtrBarEntry.Tooltip = new SeString(new TextPayload(tooltip.ToString()));
    }

    private static void EnsureIsOnFramework()
    {
        if (!Framework.IsInFrameworkUpdateThread) throw new InvalidOperationException("This method must be called from the framework update thread.");
    }

    private void OnCommand(string command, string args)
    {
        ToggleConfigUI();
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
}
