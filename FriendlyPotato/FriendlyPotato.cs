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
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace FriendlyPotato;

public sealed class FriendlyPotato : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
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
        UpdatePlayerList();
        UpdateDtrBar();
    }

    private readonly string[] healers = ["AST", "WHM", "SCH", "SGE"];

    private void UpdatePlayerList()
    {
        EnsureIsOnFramework();
        var players = ObjectTable.Where(player => player != ClientState.LocalPlayer && player is IPlayerCharacter).Cast<IPlayerCharacter>().ToList();
        playerInformation.Players = players.Select(p => new PlayerCharacterDetails
        {
            Character = p
        }).ToList();
        playerInformation.Players.Sort((a, b) =>
        {
            var aIsHealer = healers.Contains(a.JobAbbreviation);
            var bIsHealer = healers.Contains(b.JobAbbreviation);
            if (aIsHealer && !bIsHealer)
            {
                return -1;
            }

            if (!aIsHealer && bIsHealer)
            {
                return 1;
            }

            if (a.JobAbbreviation == "RDM" && b.JobAbbreviation != "RDM")
            {
                return -1;
            }

            if (a.JobAbbreviation != "RDM" && b.JobAbbreviation == "RDM")
            {
                return 1;
            }

            if (a.JobAbbreviation == "SMN" && b.JobAbbreviation != "SMN")
            {
                return -1;
            }

            if (a.JobAbbreviation != "SMN" && b.JobAbbreviation == "SMN")
            {
                return 1;
            }

            return string.Compare(a.Character.Name.ToString(), b.Character.Name.ToString(), StringComparison.Ordinal);
        });
        UpdatePlayerTypes();
    }

    private void UpdatePlayerTypes()
    {
        const int actorTablePlayerLength = 200;
        const uint weeEaId = 423;

        var friends = 0;
        var dead = 0;
        var offWorlders = 0;
        var total = 0;
        var wees = 0;
        var doomed = 0;

        if (Configuration.DebugStatuses)
        {
            unsafe
            {
                GameObject* playerObject = GameObjectManager.Instance()->Objects.IndexSorted[0];
                var cPtr = (Character*)playerObject;
                var bPtr = (BattleChara*)cPtr;
                var statuses = bPtr->GetStatusManager()->Status;
                foreach (ref var status in statuses)
                {
                    if (status.RemainingTime > 0)
                    {
                        PluginLog.Debug(
                            $"Player {cPtr->NameString} @ {cPtr->HomeWorld} has status {status.StatusId} - remaining: {status.RemainingTime}");
                    }
                }
            }
        }

        unsafe void CheckPlayer(int i)
        {
            GameObject* gameObject = GameObjectManager.Instance()->Objects.IndexSorted[i];
            var cPtr = (Character*)gameObject;
            if (gameObject == null) return;
            
            // Minions
            if ((ObjectKind)cPtr->GameObject.ObjectKind == ObjectKind.Companion)
            {
                if (cPtr->GameObject.BaseId == weeEaId)
                {
                    wees++;
                }
            }
            
            if (!gameObject->IsCharacter()) return;
            if ((ObjectKind)cPtr->GameObject.ObjectKind != ObjectKind.Player) return;
            
            var details = playerInformation.Players.Find(p =>
                p.Character.Name.ToString() == cPtr->NameString && p.Character.HomeWorld.Id == cPtr->HomeWorld
            );

            if (details == null)
            {
                PluginLog.Warning($"details is null for {cPtr->NameString} @ {cPtr->HomeWorld}");
            }

            if (cPtr->IsFriend)
            {
                friends++;
                details?.AddKind(PlayerCharacterKind.Friend);
            }

            if (cPtr->IsDead())
            {
                dead++;
                details?.AddKind(PlayerCharacterKind.Dead);
            }

            if (cPtr->IsWanderer() || cPtr->IsTraveler() || cPtr->IsVoyager())
            {
                offWorlders++;
                details?.AddKind(PlayerCharacterKind.OffWorlder);
            }

            if (details != null)
            {
                var isRaised = false;
                var isDoomed = false;

                var battleChara = (BattleChara*)cPtr;
                var statuses = battleChara->GetStatusManager()->Status;
                foreach (ref var status in statuses)
                {
                    if (Configuration.DebugStatuses && status.RemainingTime > 0)
                    {
                        PluginLog.Verbose(
                            $"Player {cPtr->NameString} @ {cPtr->HomeWorld} has status {status.StatusId} - remaining: {status.RemainingTime}");
                    }
                    switch (status.StatusId)
                    {
                        case 148 or 1140: // Raised
                            isRaised = true;
                            break;
                        case 1970: // Doom
                            isDoomed = true;
                            break;
                    }
                }

                if (isRaised)
                {
                    details.Raised = true;
                }

                if (isDoomed)
                {
                    details.Doomed = true;
                    details.AddKind(PlayerCharacterKind.Doomed);
                    doomed++;
                }
            }

            total++;
        }

        // 0 is local player and only counting other people here, so start from 1
        for (var i = 1; i < actorTablePlayerLength; i++)
        {
            CheckPlayer(i);
        }
        playerInformation.Friends = friends;
        playerInformation.Dead = dead;
        playerInformation.OffWorlders = offWorlders;
        playerInformation.Total = total;
        playerInformation.Wees = wees;
        playerInformation.Doomed = doomed;
    }

    private void UpdateDtrBar()
    {
        const string dtrSeparator = "  ";
        const char tooltipSeparator = '\n';

        if (!Configuration.NearbyEnabled)
        {
            NearbyDtrBarEntry.Shown = false;
            return;
        }
        NearbyDtrBarEntry.Shown = true;
        EnsureIsOnFramework();

        var tooltip = new StringBuilder("Visible Players: ").Append(playerInformation.Total);
        List<Payload> payloads = [new IconPayload(BitmapFontIcon.AnyClass), new TextPayload(playerInformation.Total.ToString())];
        if (Configuration.NearbyFriends)
        {
            tooltip.Append(tooltipSeparator).Append("Friends: ").Append(playerInformation.Friends);
            payloads.Add(new TextPayload(dtrSeparator));
            payloads.Add(new IconPayload(BitmapFontIcon.Returner));
            payloads.Add(new TextPayload(playerInformation.Friends.ToString()));
        }
        
        if (Configuration.NearbyDead)
        {
            tooltip.Append(tooltipSeparator).Append("Dead: ").Append(playerInformation.Dead);
            payloads.Add(new TextPayload(dtrSeparator));
            payloads.Add(new IconPayload(BitmapFontIcon.Disconnecting));
            payloads.Add(new TextPayload(playerInformation.Dead.ToString()));
        }
        
        if (Configuration.NearbyOffWorld)
        {
            tooltip.Append(tooltipSeparator).Append("Off-Worlders: ").Append(playerInformation.OffWorlders);
            payloads.Add(new TextPayload(dtrSeparator));
            payloads.Add(new IconPayload(BitmapFontIcon.CrossWorld));
            payloads.Add(new TextPayload(playerInformation.OffWorlders.ToString()));
        }

        if (Configuration.NearbyWees)
        {
            tooltip.Append(tooltipSeparator).Append("Wees: ").Append(playerInformation.Wees);
            payloads.Add(new TextPayload(dtrSeparator));
            payloads.Add(new IconPayload(BitmapFontIcon.Meteor));
            payloads.Add(new TextPayload(playerInformation.Wees.ToString()));
        }

        if (Configuration.NearbyDoomed)
        {
            tooltip.Append(tooltipSeparator).Append("Doomed: ").Append(playerInformation.Doomed);
            payloads.Add(new TextPayload(dtrSeparator));
            payloads.Add(new IconPayload(BitmapFontIcon.OrangeDiamond));
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
