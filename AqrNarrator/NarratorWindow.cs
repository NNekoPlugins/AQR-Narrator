using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System.Numerics;

namespace AqrNarrator;

public class NarratorWindow : Window
{
    private readonly List<string> _lines = new();

    public NarratorWindow()
        : base("AQR Narrator", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse)
    {
        BgAlpha = 0.75f;
        Size = new Vector2(575, 100);
        Position = new Vector2(25, 900);
        RespectCloseHotkey = false;
    }

    public void AddLine(string line)
        => _lines.Add(line);

    public override void Draw()
    {
        ImGui.BeginChild("scroll", new Vector2(0, 0), false, ImGuiWindowFlags.AlwaysVerticalScrollbar);

        bool ready = AqrNarrator.AqrReady == true;
        ImGui.TextColored(ready ? new Vector4(0, 1, 0, 1) : new Vector4(1, 1, 0, 1), ready ? "AQR: READY" : "AQR: NOT RESOLVED");

        foreach (var line in _lines)
            ImGui.TextWrapped(line);

        if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 5)
            ImGui.SetScrollHereY(1.0f);

        ImGui.EndChild();
    }
}
