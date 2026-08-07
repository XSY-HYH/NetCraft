using System.Collections.Concurrent;
using System.Reflection;
using NetCraft.Logging;

namespace NetCraft.Network;

//PacketProcessor 包处理器对应原版 net.minecraft.network.PacketProcessor
//在主线程上调度和执行包处理避免并发问题
//runningThread 是主线程引用用于 IsSameThread 判断
public sealed class PacketProcessor : IDisposable
{
    private readonly Thread _runningThread;
    private readonly ConcurrentQueue<ListenerAndPacket> _packetsToBeHandled = new();
    private readonly ConcurrentDictionary<(Type, Type), Action<object, object>> _handleCache = new();
    private bool _closed;

    public PacketProcessor(Thread? runningThread = null)
    {
        _runningThread = runningThread ?? Thread.CurrentThread;
    }

    //IsSameThread 当前线程是否为主线程
    public bool IsSameThread => ReferenceEquals(Thread.CurrentThread, _runningThread);

    //IsClosed 是否已关闭
    public bool IsClosed => _closed;

    //ScheduleIfPossible 泛型版调度包到主线程处理
    public void ScheduleIfPossible<THandler>(THandler listener, Packet<THandler> packet)
        where THandler : class
        => ScheduleIfPossible((object)listener, (object)packet);

    //ScheduleIfPossible 非泛型版调度包到主线程处理
    //Connection.Receive 解码后用 object 装箱调用此方法
    public void ScheduleIfPossible(object listener, object packet)
    {
        if (_closed)
            throw new InvalidOperationException("PacketProcessor 已关闭");
        _packetsToBeHandled.Enqueue(new ListenerAndPacket(listener, packet));
    }

    //ProcessQueuedPackets 处理所有排队包
    //关闭后直接返回
    public void ProcessQueuedPackets()
    {
        if (_closed) return;
        while (_packetsToBeHandled.TryDequeue(out var item))
            item.Handle(this);
    }

    public void Dispose()
    {
        _closed = true;
        _packetsToBeHandled.Clear();
        _handleCache.Clear();
    }

    //ListenerAndPacket 监听器和包的关联
    //类型擦除存储用反射调用 Handle
    private readonly struct ListenerAndPacket
    {
        private readonly object _listener;
        private readonly object _packet;

        public ListenerAndPacket(object listener, object packet)
        {
            _listener = listener;
            _packet = packet;
        }

        public void Handle(PacketProcessor processor)
        {
            try
            {
                var packetType = _packet.GetType();
                var listenerType = _listener.GetType();
                var invoker = processor._handleCache.GetOrAdd(
                    (packetType, listenerType),
                    key => BuildHandleInvoker(key.Item1, key.Item2));
                invoker(_listener, _packet);
            }
            catch (Exception ex)
            {
                Log.Warning("PacketProcessor", $"包处理失败: {ex.Message}");
            }
        }
    }

    //BuildHandleInvoker 构造 (listener, packet) -> 调用 packet.Handle(listener) 的委托
    //首次反射查找 Handle 方法后续直接委托调用避免重复反射开销
    private static Action<object, object> BuildHandleInvoker(Type packetType, Type listenerType)
    {
        var handleMethod = packetType.GetMethod(
            "Handle",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new[] { listenerType },
            null);
        if (handleMethod == null)
        {
            return (listener, packet) =>
            {
                var listenerInterfaces = listener.GetType().GetInterfaces();
                foreach (var iface in listenerInterfaces)
                {
                    var ifaceMethod = packetType.GetMethod(
                        "Handle",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                        null,
                        new[] { iface },
                        null);
                    if (ifaceMethod != null)
                    {
                        ifaceMethod.Invoke(packet, new[] { listener });
                        return;
                    }
                }
                throw new MissingMethodException(packetType.Name, "Handle(" + listenerType.Name + ")");
            };
        }
        return (listener, packet) => handleMethod.Invoke(packet, new[] { listener });
    }
}
