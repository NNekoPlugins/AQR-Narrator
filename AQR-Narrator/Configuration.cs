using Dalamud.Configuration;
using Dalamud.Plugin;

namespace NNekoPlugins.AqrNarrator;

public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool Enabled = true;

    public void Save()
        => AqrNarratorPlugin.PluginInterface.SavePluginConfig(this);
}
