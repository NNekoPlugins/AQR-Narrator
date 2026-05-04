using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System.Numerics;

namespace AqrNarrator;

public class NarratorWindow : Window
{
    private readonly List<string> _lines = [];

    public NarratorWindow()
        : base("AQR Narrator", ImGuiWindowFlags.None)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(575, 100),
            MaximumSize = new Vector2(575, float.MaxValue)
        };

        Size = SizeConstraints.Value.MinimumSize;
        SizeCondition = ImGuiCond.FirstUseEver;

        Position = new Vector2(25, 900);
        PositionCondition = ImGuiCond.FirstUseEver;

        AllowBackgroundBlur = false;
        AllowClickthrough = true;
        AllowPinning = true;
        ShowCloseButton = true;
        RespectCloseHotkey = false;
    }

    public void ClearNarratorWindow()
    {
        _lines.Clear();
    }

    public void AddLine(string line)
        => _lines.Add(line);

    public override void Draw()
    {
        var ready = AqrNarrator.AqrReady == true;
        ImGui.TextColored(ready ? new Vector4(0, 1, 0, 1) : new Vector4(1, 1, 0, 1), ready ? "AQR: READY" : "AQR: NOT RESOLVED");
        if (ImGui.Button("Clear Log"))
        {
            ClearNarratorWindow();
        }

        ImGui.BeginChild("scroll", new Vector2(0, 0), false, ImGuiWindowFlags.AlwaysVerticalScrollbar);
        BgAlpha = 0.75f;
        foreach (var line in _lines)
            ImGui.TextWrapped(line);

        if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 5)
            ImGui.SetScrollHereY(1.0f);

        ImGui.EndChild();
    }
}
