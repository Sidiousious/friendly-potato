using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace FriendlyPotato;

internal class CommandPanel(Configuration config)
{
    public readonly Configuration config = config;

    private int CurrentTimestamp => (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    private IPluginLog Log => FriendlyPotato.PluginLog;

    private volatile bool pendingCommandPanelOpen = false;
    private volatile int commandPanelLastOpen = 0;
    private volatile uint lastPage = 0;

    public void ZoneInit()
    {
        var ts = CurrentTimestamp;
        Log.Debug($"ZoneInit - {ts - commandPanelLastOpen}");
        if (config.RestoreCommandPanel && ts - commandPanelLastOpen < 4)
        {
            Log.Debug("Marking command panel to be opened");
            pendingCommandPanelOpen = true;
        }
    }

    public unsafe void Update()
    {
        if (!config.RestoreCommandPanel)
        {
            pendingCommandPanelOpen = false;
            return;
        }

        FriendlyPotato.EnsureIsOnFramework();

        var qp = AgentQuickPanel.Instance();
        if (qp->IsAddonReady() && qp->IsAddonShown())
        {
            commandPanelLastOpen = CurrentTimestamp;
            lastPage = qp->ActivePanel;
        }
        else if (pendingCommandPanelOpen)
        {
            Log.Debug($"Opening command panel on page {lastPage + 1}");
            pendingCommandPanelOpen = false;
            qp->OpenPanel(lastPage, false, false);
        }
    }
}
