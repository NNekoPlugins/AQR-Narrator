using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Reflection;

namespace AqrNarrator
{
    public sealed class AqrNarrator : IDalamudPlugin
    {
        #region
        [PluginService] internal static IPluginLog PluginLog { get; private set; } = null!;
        [PluginService] internal static IFramework Framework { get; private set; } = null!;
        [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] internal static IWindowSystem WindowSystem { get; private set; } = null!;
        [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
        #endregion

        private readonly NarratorWindow _window;

        private object? _eventWindow;
        private FieldInfo? _fieldText;
        private FieldInfo? _fieldName;
        private PropertyInfo? _propIsOpen;
        //private ICallGateSubscriber<string, object>? _chat2AddLine;

        public static bool AqrReady { get; private set; } = false;

        private bool _firstLineCompleted = false;
        private string _lastTargetText = "";
        private bool _wasOpenLastFrame = false;

        private readonly string _logPath = Path.Combine(AqrNarrator.PluginInterface.ConfigDirectory.FullName, "narrator_log.txt");
        private readonly string _sessionPath = Path.Combine(PluginInterface.ConfigDirectory.FullName, "session_log.txt");


        public AqrNarrator(IDalamudPluginInterface pluginInterface, IWindowSystem windowSystem)
        {
            PluginLog.Information("[AqrNarrator] Constructor fired.");

            _window = new NarratorWindow();
            windowSystem.AddWindow(_window);
            _window.IsOpen = true;
            PluginInterface.UiBuilder.Draw += DrawUI;
            Framework.Update += OnFrameworkUpdate;
            //_chat2AddLine = PluginInterface.GetIpcSubscriber<string, object>("ChatTwo.AddLine");

            CommandManager.AddHandler("/aqrwin", new CommandInfo((_, _) =>
            {
                _window.IsOpen = !_window.IsOpen;
                PluginLog.Information("[AqrNarrator] Window toggled. Now: " + _window.IsOpen);
            })
            {
                HelpMessage = "Toggle AQR Narrator window"
            });

            CommandManager.AddHandler("/aqrnewquest", new CommandInfo((_, _) =>
            {
                ClearSession();
            })
            {
                HelpMessage = "Clear narrator session for a new quest"
            });

            CommandManager.AddHandler("/hidechat", new CommandInfo((_, _) =>
            {
                SetChatLogVisible(false);
            })
            {
                HelpMessage = "Hide the vanilla chat log window"
            });

            CommandManager.AddHandler("/showchat", new CommandInfo((_, _) =>
            {
                SetChatLogVisible(true);
            })
            {
                HelpMessage = "Show the vanilla chat log window"
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

            if (_eventWindow == null)
                return;

            var isOpen = (bool)(_propIsOpen?.GetValue(_eventWindow) ?? false);
            var current = _fieldText?.GetValue(_eventWindow) as string ?? "";
            var name = _fieldName?.GetValue(_eventWindow) as string ?? "Unknown";

            // AQR's full line being typed
            var targetField = _eventWindow.GetType().GetField("_targetText", BindingFlags.Instance | BindingFlags.NonPublic);

            var target = targetField?.GetValue(_eventWindow) as string ?? "";

            //
            // CASE 1: Window just opened
            //
            if (isOpen && !_wasOpenLastFrame)
            {
                // Detect new quest start
                if (target.StartsWith("QuestStart:")) // or whatever AQR exposes
                    ClearSession();

                _wasOpenLastFrame = true;
                _lastTargetText = target;
                _firstLineCompleted = false;
                return;
            }

            //
            // CASE 2: Window is open
            //
            if (isOpen)
            {
                // Detect when the first line finishes typing
                if (!_firstLineCompleted && current == target)
                {
                    _firstLineCompleted = true;
                }

                // Player clicked → target changed → finalize previous line
                if (target != _lastTargetText)
                {
                    // Finalize previous line ONLY if it finished typing
                    if (_firstLineCompleted && !string.IsNullOrWhiteSpace(_lastTargetText))
                    {
                        PluginLog.Information("[AqrNarrator] Finalizing (advance): \"" + _lastTargetText + "\"");
                        PrintNarration(name, _lastTargetText);
                    }

                    // Shift to new line
                    _lastTargetText = target;
                    _firstLineCompleted = false;
                }

                return;
            }

            //
            // CASE 3: Window just closed → finalize last line
            //
            if (!isOpen && _wasOpenLastFrame)
            {
                _wasOpenLastFrame = false;

                // Only finalize if typing finished
                if (_firstLineCompleted && !string.IsNullOrWhiteSpace(target))
                {
                    PluginLog.Information("[AqrNarrator] Finalizing on close: \"" + target + "\"");
                    PrintNarration(name, target);
                }

                _lastTargetText = "";
                _firstLineCompleted = false;
                return;
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

        private void LogToFile(string line)
        {
            try
            {
                File.AppendAllText(_logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {line}\n");
            }
            catch { }
        }

        private void LogSession(string line)
        {
            File.AppendAllText(_sessionPath, line + "\n");
        }

        private void ClearSession()
        {
            _window.ClearNarratorWindow();
            _lastTargetText = "";
            _firstLineCompleted = false;
            _wasOpenLastFrame = false;
            File.WriteAllText(_sessionPath, "");

            PluginLog.Information("[AqrNarrator] Session cleared (new quest or reset).");
        }

        public static void DrawUI()
        {
            WindowSystem.Draw();
        }

        private void PrintNarration(string npc, string line)
        {
            var formatted = $"◆ {npc}: {line}";

            // Send to narrator window
            _window.AddLine(formatted);

            // Send to Chat2
            //_chat2AddLine?.InvokeFunc(formatted);

            // Log to file
            LogToFile(formatted);
        }


        private unsafe void SetChatLogVisible(bool visible)
        {
            HideAddon("ChatLog", visible);

            // Hide all chat panels (0–3)
            for (var i = 0; i < 4; i++)
            {
                HideAddon($"ChatLogPanel_{i}", visible);
            }
        }

        private unsafe void HideAddon(string name, bool visible)
        {
            var addon = GameGui.GetAddonByName(name, 1);
            if (addon == null || addon.Address == IntPtr.Zero)
                return; // silently ignore missing addons

            var atk = (AtkUnitBase*)addon.Address;
            atk->IsVisible = visible;

            if (atk->RootNode != null)
                atk->RootNode->ToggleVisibility(visible);

            PluginLog.Information($"[AqrNarrator] {name} visibility set to: {visible}");
        }
    }
}
