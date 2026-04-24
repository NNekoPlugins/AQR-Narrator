using Dalamud.Interface.Windowing;

namespace NNekoPlugins.AqrNarrator;

public class PluginUI : Window
{
    private readonly Configuration config;

    public PluginUI(Configuration config)
        : base("AQR Narrator")
    {
        this.config = config;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new System.Numerics.Vector2(300, 100),
            MaximumSize = new System.Numerics.Vector2(600, 300)
        };
    }

    public override void Draw()
    {
        if (ImGui.Checkbox("Enable narration", ref config.Enabled))
            config.Save();

        ImGui.TextWrapped("Forwards A Quest Reborn dialog into /echo.");
    }
}
