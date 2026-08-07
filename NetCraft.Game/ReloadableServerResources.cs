using NetCraft.Bootstrap;
using NetCraft.Logging;
using NetCraft.Registry;
using NetCraft.Resources;
using NetCraft.Tags;

namespace NetCraft.Game;

//ReloadableServerResources 服务端可重载资源集合对应原版同名类精简版
//持有 ResourceManager 与已注册 listener 列表 LoadResources 一次性构建并触发首次重载
//不含 Recipes/Advancements/Functions 等业务监听器留给后续阶段
//原版异步 CompletableFuture 调度在此简化为同步 SimpleReloadInstance.Run
public sealed class ReloadableServerResources
{
    //ResourceManager 资源包聚合入口 listeners 共享同一实例
    public ResourceManager ResourceManager { get; }
    //TagManager 标签管理器供 BindAll 入口暴露给启动序列
    public TagManager TagManager { get; }
    //Listeners 已注册的重载监听器按注册顺序执行
    private readonly List<PreparableReloadListener> _listeners = new();
    public IReadOnlyList<PreparableReloadListener> Listeners => _listeners;

    private ReloadableServerResources(ResourceManager rm, TagManager tm)
    {
        ResourceManager = rm;
        TagManager = tm;
    }

    //LoadResources 构建实例注册内置 listener 并触发首次重载
    //registryAccess 由 BuiltInRegistries.CreateRegistryAccess 提供用于 TagManager.BindAll
    //必须在 BootstrapClass.BootStrap 之后调用因 BindAll 要求 Registry 已 Freeze
    public static ReloadableServerResources LoadResources(ResourceManager rm, RegistryAccess registryAccess)
    {
        Log.Debug($"ReloadableServerResources.LoadResources 入口");
        var tm = new TagManager();
        var rsr = new ReloadableServerResources(rm, tm);
        rsr._listeners.Add(new TagsReloadListener(tm, registryAccess));
        //未来此处追加 RecipeListener/AdvancementListener/FunctionListener
        rsr.Reload();
        Log.Debug($"ReloadableServerResources.LoadResources 出口");
        return rsr;
    }

    //Reload 触发所有 listener 按顺序重载同步执行
    //用于运行时 /reload 命令或 Pack 增删后手动触发
    public void Reload()
    {
        Log.Debug($"ReloadableServerResources.Reload 入口 listenerCount={_listeners.Count}");
        SimpleReloadInstance.Run(ResourceManager, _listeners);
        Log.Debug($"ReloadableServerResources.Reload 出口");
    }
}
