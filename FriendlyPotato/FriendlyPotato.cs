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
using System.Text;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects;

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

    private const string CommandName = "/friendlypotato";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("FriendlyPotato");
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
        UpdatePlayerTypes();
        UpdatePlayerList();
        UpdateDtrBar();
    }

    private void UpdatePlayerList()
    {
        EnsureIsOnFramework();
        var players = ObjectTable.Where(player => player != ClientState.LocalPlayer && player is IPlayerCharacter).Cast<IPlayerCharacter>().ToList();
        players.Sort((a, b) => string.Compare(a.Name.ToString(), b.Name.ToString(), StringComparison.Ordinal));
        playerInformation.Players = players;
    }

    private void UpdatePlayerTypes()
    {
        const int actorTablePlayerLength = 200;

        var friends = 0;
        var dead = 0;
        var offWorlders = 0;
        var total = -1; // -1 to account for local player

        unsafe void CheckPlayer(int i)
        {
            GameObject* gameObject = GameObjectManager.Instance()->Objects.IndexSorted[i];
            var cPtr = (Character*)gameObject;
            if (gameObject == null || !gameObject->IsCharacter()) return;
            if ((ObjectKind)cPtr->GameObject.ObjectKind != ObjectKind.Player) return;

            if (cPtr->IsFriend) friends++;
            if (cPtr->IsDead()) dead++;
            if (cPtr->IsWanderer() || cPtr->IsTraveler() || cPtr->IsVoyager()) offWorlders++;

            total++;
        }

        for (var i = 0; i < actorTablePlayerLength; i++)
        {
            CheckPlayer(i);
        }
        playerInformation.Friends = friends;
        playerInformation.Dead = dead;
        playerInformation.OffWorlders = offWorlders;
        playerInformation.Total = total;
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
