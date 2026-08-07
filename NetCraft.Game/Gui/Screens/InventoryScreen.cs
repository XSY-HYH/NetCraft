using NetCraft.Game.Gui;
using NetCraft.Gpu;

namespace NetCraft.Game.Gui.Screens;

//InventoryScreen 背包屏幕对应原版 InventoryScreen
//PoC 占位显示标题实际物品栏待 Container 体系接入
public sealed class InventoryScreen : Screen
{
    public override string Title => "背包";

    public override void Init()
    {
        var cx = GuiWidth / 2;
        var cy = GuiHeight / 2;
        AddWidget(new GuiLabel("背包（PoC）") { X = cx - 40, Y = cy - 60, Width = 80, Height = 20 });
        var closeBtn = AddWidget(new GuiButton("关闭") { X = cx - 50, Y = cy + 40, Width = 100, Height = 20 });
        closeBtn.Click += (_, _) => OnClose();
    }

    public override void OnClose() => Manager.SetScreen(new GameScreen());
    public override bool IsPauseScreen() => true;
}
