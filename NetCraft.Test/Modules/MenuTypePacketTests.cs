using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Network;
using NetCraft.Network.Chat;
using NetCraft.Network.Inventory;
using NetCraft.Registry;

namespace NetCraft.Test.Modules;

//MenuTypePacketTests ClientboundOpenScreenPacket 端到端编解码往返
//覆盖 MenuType.StreamCodec + ClientboundOpenScreenPacket 三字段 ContainerId+Kind+Title
internal static class MenuTypePacketTests
{
    public const string Module = "menutypepacket";

    private static RegistryAccess? _access;
    private static MenuType? _testMenu;

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("ClientboundOpenScreenPacket round-trip", TestOpenScreenRoundTrip);
        yield return ("MenuType StreamCodec round-trip", TestMenuTypeStreamCodec);
    }

    //EnsureBootstrap 一次性注册 TestMenuType 到 MENU 注册表并 Freeze
    private static RegistryAccess EnsureBootstrap()
    {
        if (_access is not null) return _access;
        _testMenu = new MenuType();
        Registry<object>.Register(BuiltInRegistries.MENU, "test_menu", _testMenu);
        BuiltInRegistries.MENU.Freeze();
        var entries = new[]
        {
            new RegistryEntry(Registries.MENU.Identifier, (object)BuiltInRegistries.MENU),
        };
        _access = new ImmutableRegistryAccess(entries);
        return _access;
    }

    //EncodeDecode 编码后用新 buf 解码返回解码值
    private static T EncodeDecode<T>(StreamCodec<RegistryFriendlyByteBuf, T> codec, T value, RegistryAccess access)
    {
        var buf = new RegistryFriendlyByteBuf(access);
        codec.Encode(buf, value);
        var data = buf.AsArray();
        var readBuf = new RegistryFriendlyByteBuf(access, data);
        return codec.Decode(readBuf);
    }

    private static bool TestOpenScreenRoundTrip()
    {
        var access = EnsureBootstrap();
        var title = Component.Literal("Test Screen");
        var packet = new ClientboundOpenScreenPacket(42, _testMenu!, title);
        var decoded = EncodeDecode(ClientboundOpenScreenPacket.StreamCodec, packet, access);
        return decoded.ContainerId == 42
            && ReferenceEquals(decoded.Kind, _testMenu)
            && decoded.Title.GetString() == "Test Screen";
    }

    private static bool TestMenuTypeStreamCodec()
    {
        var access = EnsureBootstrap();
        var decoded = EncodeDecode(MenuType.StreamCodec, _testMenu!, access);
        return ReferenceEquals(decoded, _testMenu);
    }
}
