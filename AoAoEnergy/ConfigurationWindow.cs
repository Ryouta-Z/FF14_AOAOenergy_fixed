using Dalamud.Bindings.ImGui;

namespace AoAoEnergy;

internal sealed class ConfigurationWindow
{
    private readonly Plugin plugin;

    public ConfigurationWindow(Plugin plugin)
    {
        this.plugin = plugin;
    }

    public bool IsOpen { get; set; }

    public void Draw()
    {
        if (!IsOpen)
            return;

        var isOpen = IsOpen;
        if (!ImGui.Begin("AoAoEnergy 设置", ref isOpen))
        {
            IsOpen = isOpen;
            ImGui.End();
            return;
        }
        IsOpen = isOpen;

        ImGui.TextWrapped("调整爆发药 RGB 特效的垂直位置。默认的“原始位置”适合未使用骨骼修复的人物。");

        var rememberPerCharacter = plugin.RememberPerCharacter;
        if (ImGui.Checkbox("按当前角色分别记住", ref rememberPerCharacter))
            plugin.SetRememberPerCharacter(rememberPerCharacter);

        var selectedIndex = plugin.SelectedOffsetIndex;
        if (ImGui.Combo("特效高度", ref selectedIndex, Plugin.OffsetLabels, Plugin.OffsetLabels.Length))
            plugin.SetSelectedOffsetIndex(selectedIndex);

        if (ImGui.Button("预览当前效果"))
            plugin.PreviewSelectedEffect();

        ImGui.TextWrapped("预览不会消耗爆发药。其他角色未单独设置时，会使用全局选择。");
        ImGui.End();
    }
}
