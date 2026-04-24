using Dalamud.Game;
using Dalamud.Game.Gui;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;
using System.Reflection;

namespace NNekoPlugins.AqrNarrator;

public sealed class AqrNarratorPlugin : IDalamudPlugin
{
    public string Name => "AQR Narrator";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;

    private object? aqrService;
    private EventInfo? aqrEvent;
    private string? lastText;
    private bool enabled = true;

    public AqrNarratorPlugin()
    {
        TryHookAqr();
        Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        UnhookAqrEvent();
    }

    // -----------------------------
    // AQR HOOKING
    // -----------------------------
    private void TryHookAqr()
    {
        var aqr = PluginInterface.GetPlugin("A Quest Reborn");
        if (aqr == null)
            return;

        aqrService = aqr.GetType().GetProperty("Service")?.GetValue(aqr);
        if (aqrService == null)
            return;

        // Try event-based hook
        aqrEvent = aqrService.GetType().GetEvent("OnDialogueAdvanced");
        if (aqrEvent != null)
        {
            var handler = (Action<string>)OnAqrDialogue;
            aqrEvent.AddEventHandler(aqrService, handler);
        }
    }

    private void UnhookAqrEvent()
    {
        if (aqrService == null || aqrEvent == null)
            return;

        var handler = (Action<string>)OnAqrDialogue;
        aqrEvent.RemoveEventHandler(aqrService, handler);
    }

    // -----------------------------
    // EVENT-BASED PATH
    // -----------------------------
    private void OnAqrDialogue(string text)
    {
        if (!enabled)
            return;

        PrintToEcho(Sanitize(text));
    }

    // -----------------------------
    // REFLECTION POLLING PATH
    // -----------------------------
    private void OnFrameworkUpdate(IFramework _)
    {
        if (!enabled || aqrService == null || aqrEvent != null)
            return;

        var field = aqrService.GetType().GetField("CurrentDialogue",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (field == null)
            return;

        var text = field.GetValue(aqrService) as string;
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (text != lastText)
        {
            lastText = text;
            PrintToEcho(Sanitize(text));
        }
    }

    // -----------------------------
    // UTILITIES
    // -----------------------------
    private static string Sanitize(string text)
    {
        return text
            .Replace("\r", "")
            .Replace("\n", " ")
            .Trim();
    }

    private void PrintToEcho(string text)
    {
        ChatGui.Print($"[AQR] {text}");
    }
}
