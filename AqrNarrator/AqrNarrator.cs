using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Reflection;

namespace AqrNarrator
{
    public sealed class AqrNarrator : IDalamudPlugin
    {
        [PluginService] internal static IPluginLog PluginLog { get; private set; } = null!;
        [PluginService] internal static IFramework Framework { get; private set; } = null!;
        [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

        public static WindowSystem WindowSystem = new("AqrNarrator");
        private NarratorWindow _window;

        private object? _eventWindow;
        private FieldInfo? _fieldText;
        private FieldInfo? _fieldName;
        private PropertyInfo? _propIsOpen;

        public static bool AqrReady { get; private set; } = false;


        private string _lastSeenText = "";
        private int _stableFrames = 0;
        private const int FramesRequiredForStable = 10; // ~0.16s at 60fps

        public AqrNarrator()
        {
            PluginLog.Information("[AqrNarrator] Constructor fired.");

            _window = new NarratorWindow();
            WindowSystem.AddWindow(_window);
            _window.IsOpen = true;
            PluginInterface.UiBuilder.Draw += DrawUI;
            Framework.Update += OnFrameworkUpdate;

            CommandManager.AddHandler("/aqrwin", new CommandInfo((_, _) =>
            {
                _window.IsOpen = !_window.IsOpen;
                PluginLog.Information("[AqrNarrator] Window toggled. Now: " + _window.IsOpen);
            })
            {
                HelpMessage = "Toggle AQR Narrator window"
            });
        }


        public void Dispose()
        {
            CommandManager.RemoveHandler("/aqrwin");
            Framework.Update -= OnFrameworkUpdate;
            PluginInterface.UiBuilder.Draw -= DrawUI;
            WindowSystem.RemoveAllWindows();
        }

        private void OnFrameworkUpdate(IFramework _)
        {
            if (!EnsureAqrResolved())
                return;

            string raw = _fieldText?.GetValue(_eventWindow) as string ?? "";
            string text = raw.Trim();
            string name = _fieldName?.GetValue(_eventWindow) as string ?? "Unknown";
            bool isOpen = (bool)(_propIsOpen?.GetValue(_eventWindow) ?? false);

            bool hasText = isOpen && !string.IsNullOrWhiteSpace(text);

            // If no text, reset stability and return
            if (!hasText)
            {
                _lastSeenText = "";
                _stableFrames = 0;
                return;
            }

            // If text changed, reset stability timer
            if (text != _lastSeenText)
            {
                _lastSeenText = text;
                _stableFrames = 0;
                return;
            }

            // Text is unchanged this frame → increment stability
            _stableFrames++;

            // If text has been stable long enough → finalize
            if (_stableFrames == FramesRequiredForStable)
            {
                PluginLog.Information("[AqrNarrator] Finalizing stable line: \"" + text + "\"");
                PrintNarration(name, text);
            }
        }


        private bool EnsureAqrResolved()
        {
            if (_eventWindow != null)
                return true;

            try
            {
                PluginLog.Information("[AqrNarrator] Resolving AQR…");

                // Find the AQR assembly
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "AQuestReborn");

                if (asm == null)
                {
                    PluginLog.Warning("[AqrNarrator] AQuestReborn assembly not found.");
                    return false;
                }

                // Find the type that has a public EventWindow property
                var pluginType = asm.GetTypes()
                    .FirstOrDefault(t => t.GetProperty("EventWindow",
                        BindingFlags.Public | BindingFlags.Instance) != null);

                if (pluginType == null)
                {
                    PluginLog.Warning("[AqrNarrator] Could not find type with EventWindow.");
                    return false;
                }

                // Find the instance of that type by scanning all objects in memory
                var instance = asm.GetTypes()
                    .Where(t => pluginType.IsAssignableFrom(t))
                    .SelectMany(t => t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    .Where(f => pluginType.IsAssignableFrom(f.FieldType))
                    .Select(f => f.GetValue(null))
                    .FirstOrDefault();

                if (instance == null)
                {
                    PluginLog.Warning("[AqrNarrator] Could not find AQR plugin instance.");
                    return false;
                }

                // Get EventWindow
                var eventProp = pluginType.GetProperty("EventWindow",
                    BindingFlags.Public | BindingFlags.Instance);

                _eventWindow = eventProp?.GetValue(instance);

                if (_eventWindow == null)
                {
                    PluginLog.Warning("[AqrNarrator] EventWindow is null.");
                    AqrReady = false;
                    return false;
                }

                PluginLog.Information("[AqrNarrator] EventWindow resolved successfully.");
                AqrReady = true;

                // Resolve fields
                var ewType = _eventWindow.GetType();

                _fieldText = ewType.GetField("_currentText", BindingFlags.Instance | BindingFlags.NonPublic);
                _fieldName = ewType.GetField("_currentName", BindingFlags.Instance | BindingFlags.NonPublic);
                _propIsOpen = ewType.GetProperty("IsOpen", BindingFlags.Instance | BindingFlags.Public);

                return true;
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, "[AqrNarrator] Reflection failed.");
                return false;
            }
        }

        public void DrawUI()
        {
            WindowSystem.Draw();
        }

        private void PrintNarration(string npc, string line)
        {
            PluginLog.Information($"[AqrNarrator] Printing line: NPC=\"{npc}\" Text=\"{line}\"");
            _window.AddLine($"◆ {npc}: {line}");
        }
    }
}
