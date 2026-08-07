using NetCraft.Bootstrap;
using NetCraft.Game;
using NetCraft.Registry;
using NetCraft.Resources;
using NetCraft.Tags;
using BootstrapClass = NetCraft.Bootstrap.Bootstrap;

namespace NetCraft.Test.Modules;

//ReloadableServerResources 测试覆盖 LoadResources→TagsReloadListener→BindAll 端到端链路
//验证空 packs 不抛 Reload 重新触发 listeners 顺序与 TagsReloadListener 注册
//需要 BootStrap 冻结注册表因 BindAll 要求 _frozen=true
internal static class ReloadableServerResourcesTests
{
    public const string Module = "reloadableserverresources";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("LoadResources empty packs no throw", TestLoadResourcesEmptyPacksNoThrow);
        yield return ("LoadResources registers TagsReloadListener", TestRegistersTagsReloadListener);
        yield return ("Reload reinvokes all listeners", TestReloadReinvokesListeners);
        yield return ("SimpleReloadInstance runs listeners in order", TestListenersRunInOrder);
        yield return ("ReloadContext carries index and total", TestReloadContextIndexTotal);
    }

    //TestLoadResourcesEmptyPacksNoThrow 空 ResourceManager 也能调 LoadResources 不抛
    //BindAll 遍历空 _binders 不触发任何 BindTags 安全
    private static bool TestLoadResourcesEmptyPacksNoThrow()
    {
        BootstrapClass.BootStrap();
        var rm = new ResourceManager();
        var ra = BuiltInRegistries.CreateRegistryAccess();
        var rsr = ReloadableServerResources.LoadResources(rm, ra);
        return rsr.Listeners.Count == 1 && rsr.TagManager is not null && rsr.ResourceManager == rm;
    }

    //TestRegistersTagsReloadListener Listeners 第一个且唯一一个为 TagsReloadListener
    //未来追加 RecipeListener/AdvancementListener 时 Count 断言需调整
    private static bool TestRegistersTagsReloadListener()
    {
        BootstrapClass.BootStrap();
        var rm = new ResourceManager();
        var ra = BuiltInRegistries.CreateRegistryAccess();
        var rsr = ReloadableServerResources.LoadResources(rm, ra);
        return rsr.Listeners[0] is TagsReloadListener;
    }

    //TestReloadReinvokesListeners 显式 Reload 重新调用所有 listener 不抛
    //覆盖 /reload 命令重载场景验证 TagsReloadListener.Reset+LoadBuiltinTags+BindAll 可重复执行
    private static bool TestReloadReinvokesListeners()
    {
        BootstrapClass.BootStrap();
        var rm = new ResourceManager();
        var ra = BuiltInRegistries.CreateRegistryAccess();
        var rsr = ReloadableServerResources.LoadResources(rm, ra);
        rsr.Reload();
        rsr.Reload();
        return rsr.Listeners.Count == 1;
    }

    //TestListenersRunInOrder 直接验证 SimpleReloadInstance 按列表顺序调用 listener
    //ReloadableServerResources.LoadResources 内部 listener 列表固定无法注入 mock 故单独测调度器
    private static bool TestListenersRunInOrder()
    {
        var rm = new ResourceManager();
        var order = new List<string>();
        var listeners = new List<PreparableReloadListener>
        {
            new ActionListener("a", order),
            new ActionListener("b", order),
            new ActionListener("c", order),
        };
        SimpleReloadInstance.Run(rm, listeners);
        return order.Count == 3 && order[0] == "a" && order[1] == "b" && order[2] == "c";
    }

    //TestReloadContextIndexTotal 验证 ReloadContext 携带正确序号与总数
    private static bool TestReloadContextIndexTotal()
    {
        var rm = new ResourceManager();
        var captured = new List<ReloadContext>();
        var listeners = new List<PreparableReloadListener>
        {
            new CaptureListener(captured),
            new CaptureListener(captured),
            new CaptureListener(captured),
        };
        SimpleReloadInstance.Run(rm, listeners);
        return captured.Count == 3
            && captured[0].Index == 0 && captured[0].Total == 3
            && captured[1].Index == 1 && captured[1].Total == 3
            && captured[2].Index == 2 && captured[2].Total == 3
            && captured[0].Name == nameof(CaptureListener);
    }

    //ActionListener 记录调用顺序到外部 list 供断言
    private sealed class ActionListener : PreparableReloadListener
    {
        private readonly string _name;
        private readonly List<string> _order;
        public ActionListener(string name, List<string> order) { _name = name; _order = order; }
        public void Reload(ResourceManager rm, ReloadContext ctx) => _order.Add(_name);
    }

    //CaptureListener 捕获 ReloadContext 供序号与总数断言
    private sealed class CaptureListener : PreparableReloadListener
    {
        private readonly List<ReloadContext> _captured;
        public CaptureListener(List<ReloadContext> captured) { _captured = captured; }
        public void Reload(ResourceManager rm, ReloadContext ctx) => _captured.Add(ctx);
    }
}
