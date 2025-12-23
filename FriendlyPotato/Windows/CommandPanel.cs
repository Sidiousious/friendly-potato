using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace FriendlyPotato.Windows;

internal class CommandPanel : Window, IDisposable
{
    private const uint CustomNodeId = 0x500000;
    public readonly Configuration config;

    private int CurrentTimestamp => (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    private IPluginLog Log => FriendlyPotato.PluginLog;
    private IGameGui GameGui => FriendlyPotato.GameGui;
    private IFramework Framework => FriendlyPotato.Framework;
    private IAddonLifecycle AL => FriendlyPotato.AddonLifecycle;

    public CommandPanel(Configuration config) : base("FriendlyPotatoCommandPanel")
    {
        this.config = config;

        Flags = ImGuiWindowFlags.NoBackground
            | ImGuiWindowFlags.NoDecoration
            | ImGuiWindowFlags.NoFocusOnAppearing
            | ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoResize;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;

        AL.RegisterListener(AddonEvent.PostOpen, "QuickPanel", PanelDrawing);
        AL.RegisterListener(AddonEvent.PreClose, "QuickPanel", PanelClosing);
        AL.RegisterListener(AddonEvent.PostClose, "QuickPanel", PanelClosed);
    }

    public void Dispose()
    {
        AL.UnregisterListener(AddonEvent.PostOpen, "QuickPanel", PanelClosing);
        AL.UnregisterListener(AddonEvent.PreDraw, "QuickPanel", PanelDrawing);
        AL.UnregisterListener(AddonEvent.PostClose, "QuickPanel", PanelClosed);
    }

    private unsafe void PanelDrawing(AddonEvent type, AddonArgs args)
    {
        if (!this.IsOpen)
            this.Toggle();
    }

    private void PanelClosed(AddonEvent type, AddonArgs args)
    {
        if (this.IsOpen)
            this.Toggle();
    }

    private unsafe void PanelClosing(AddonEvent type, AddonArgs args)
    {
        if (!config.RestoreCommandPanel)
            return;

        var qp = AgentQuickPanel.Instance();
        Log.Debug($"[CommandPanel] {type} - {args} - {qp->IsAddonReady()} - {qp->IsAddonShown()} - {qp->ActivePanel}");
        Framework.RunOnTick(() =>
        {
            OpenPanel(qp->ActivePanel);
        }, TimeSpan.FromMilliseconds(50));
    }

    private unsafe void OpenPanel(uint page)
    {
        Log.Debug($"Opening command panel on page {page + 1}");
        var qp = AgentQuickPanel.Instance();
        if (qp->IsAddonShown())
        {
            Log.Warning("[CommandPanel] OpenPanel called while addon is already shown");
            return;
        }

        qp->OpenPanel(page, false, false);
        if (!this.IsOpen)
            this.Toggle();
    }

    private Vector2? CommandPanelPosition()
    {
        var addonPtr = GameGui.GetAddonByName("QuickPanel");
        if (addonPtr == nint.Zero)
            return null;

        return addonPtr.Position;
    }

    public override void PreDraw()
    {
        base.PreDraw();

        var addonPtr = GameGui.GetAddonByName("QuickPanel");
        if (addonPtr == nint.Zero)
            return;
        Position = addonPtr.Position;
    }

    public override void Draw()
    {
        var restoreCommandPanel = config.RestoreCommandPanel;
        if (ImGui.Checkbox("Locked", ref restoreCommandPanel))
        {
            config.RestoreCommandPanel = restoreCommandPanel;
            config.Save();
        }
    }

    public override void PostDraw()
    {
        ImGui.GetStyle().ScaleAllSizes(1f);
    }
}
