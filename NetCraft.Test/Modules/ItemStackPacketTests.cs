using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.World.Entity;
using NetCraft.Game.World.Items;
using NetCraft.Network;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Test.Modules;

//ItemStackPacketTests 6 个 ItemStack 相关 Play 包 StreamCodec 端到端往返
//覆盖 ItemStack.OptionalStreamCodec + 6 个 Clientbound 包编码解码还原
internal static class ItemStackPacketTests
{
    public const string Module = "itemstackpacket";

    private static RegistryAccess? _access;
    private static Item? _testItem;

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("ItemStack empty OptionalStreamCodec round-trip", TestItemStackEmpty);
        yield return ("ItemStack non-empty OptionalStreamCodec round-trip", TestItemStackNonEmpty);
        yield return ("ClientboundSetCursorItemPacket empty round-trip", TestSetCursorItemEmpty);
        yield return ("ClientboundSetCursorItemPacket non-empty round-trip", TestSetCursorItemNonEmpty);
        yield return ("ClientboundSetPlayerInventoryPacket round-trip", TestSetPlayerInventory);
        yield return ("ClientboundContainerSetSlotPacket round-trip", TestContainerSetSlot);
        yield return ("ClientboundContainerSetDataPacket round-trip", TestContainerSetData);
        yield return ("ClientboundContainerSetContentPacket empty list round-trip", TestContainerSetContentEmpty);
        yield return ("ClientboundContainerSetContentPacket multi items round-trip", TestContainerSetContentMulti);
        yield return ("ClientboundSetEquipmentPacket single slot round-trip", TestSetEquipmentSingle);
        yield return ("ClientboundSetEquipmentPacket multi slots round-trip", TestSetEquipmentMulti);
    }

    //TestItem 测试用 Item 子类实现 Id 属性供注册
    private sealed class TestItem : Item
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("test_item");
    }

    //EnsureBootstrap 一次性初始化注册表注册 TestItem 并 Freeze
    private static RegistryAccess EnsureBootstrap()
    {
        if (_access is not null) return _access;
        DataComponents.Bootstrap();
        _testItem = new TestItem();
        Registry<Item>.Register(BuiltInRegistries.ITEM, "test_item", _testItem);
        BuiltInRegistries.ITEM.Freeze();
        BuiltInRegistries.DATA_COMPONENT_TYPE.Freeze();
        var entries = new[]
        {
            new RegistryEntry(Registries.ITEM.Identifier, (object)BuiltInRegistries.ITEM),
            new RegistryEntry(Registries.DATA_COMPONENT_TYPE.Identifier, (object)BuiltInRegistries.DATA_COMPONENT_TYPE),
        };
        _access = new ImmutableRegistryAccess(entries);
        return _access;
    }

    //NewStack 构造非空 ItemStack 用 TestItem + count=1
    private static ItemStack NewStack(int count = 1)
        => new(_testItem!.BuiltInRegistryHolder, count, DataComponentPatch.Empty);

    //EncodeDecode 编码后解码返回解码值
    private static T EncodeDecode<T>(
        StreamCodec<RegistryFriendlyByteBuf, T> codec, T value, RegistryAccess access)
    {
        var buf = new RegistryFriendlyByteBuf(access);
        codec.Encode(buf, value);
        var data = buf.AsArray();
        var readBuf = new RegistryFriendlyByteBuf(access, data);
        return codec.Decode(readBuf);
    }

    private static bool TestItemStackEmpty()
    {
        var access = EnsureBootstrap();
        var decoded = EncodeDecode(ItemStack.OptionalStreamCodec, ItemStack.Empty, access);
        return decoded.IsEmpty();
    }

    private static bool TestItemStackNonEmpty()
    {
        var access = EnsureBootstrap();
        var stack = NewStack(5);
        var decoded = EncodeDecode(ItemStack.OptionalStreamCodec, stack, access);
        return !decoded.IsEmpty()
            && decoded.GetCount() == 5
            && decoded.GetItem().Id == _testItem!.Id;
    }

    private static bool TestSetCursorItemEmpty()
    {
        var access = EnsureBootstrap();
        var packet = new ClientboundSetCursorItemPacket(ItemStack.Empty);
        var decoded = EncodeDecode(ClientboundSetCursorItemPacket.StreamCodec, packet, access);
        return decoded.Contents.IsEmpty();
    }

    private static bool TestSetCursorItemNonEmpty()
    {
        var access = EnsureBootstrap();
        var packet = new ClientboundSetCursorItemPacket(NewStack(3));
        var decoded = EncodeDecode(ClientboundSetCursorItemPacket.StreamCodec, packet, access);
        return !decoded.Contents.IsEmpty() && decoded.Contents.GetCount() == 3;
    }

    private static bool TestSetPlayerInventory()
    {
        var access = EnsureBootstrap();
        var packet = new ClientboundSetPlayerInventoryPacket(15, NewStack(2));
        var decoded = EncodeDecode(ClientboundSetPlayerInventoryPacket.StreamCodec, packet, access);
        return decoded.Slot == 15 && decoded.Contents.GetCount() == 2;
    }

    private static bool TestContainerSetSlot()
    {
        var access = EnsureBootstrap();
        var packet = new ClientboundContainerSetSlotPacket(7, 100, 36, NewStack(64));
        var decoded = EncodeDecode(ClientboundContainerSetSlotPacket.StreamCodec, packet, access);
        return decoded.ContainerId == 7
            && decoded.StateId == 100
            && decoded.Slot == 36
            && decoded.Stack.GetCount() == 64;
    }

    private static bool TestContainerSetData()
    {
        var access = EnsureBootstrap();
        var packet = new ClientboundContainerSetDataPacket(3, 200, -1);
        var decoded = EncodeDecode(ClientboundContainerSetDataPacket.StreamCodec, packet, access);
        return decoded.ContainerId == 3 && decoded.Id == 200 && decoded.Value == -1;
    }

    private static bool TestContainerSetContentEmpty()
    {
        var access = EnsureBootstrap();
        var packet = new ClientboundContainerSetContentPacket(1, 0, new List<ItemStack>(), null);
        var decoded = EncodeDecode(ClientboundContainerSetContentPacket.StreamCodec, packet, access);
        return decoded.ContainerId == 1
            && decoded.StateId == 0
            && decoded.Items.Count == 0
            && decoded.CarriedItem is null;
    }

    private static bool TestContainerSetContentMulti()
    {
        var access = EnsureBootstrap();
        var items = new List<ItemStack> { NewStack(10), ItemStack.Empty, NewStack(1) };
        var packet = new ClientboundContainerSetContentPacket(2, 5, items, NewStack(7));
        var decoded = EncodeDecode(ClientboundContainerSetContentPacket.StreamCodec, packet, access);
        return decoded.ContainerId == 2
            && decoded.StateId == 5
            && decoded.Items.Count == 3
            && decoded.Items[0].GetCount() == 10
            && decoded.Items[1].IsEmpty()
            && decoded.Items[2].GetCount() == 1
            && decoded.CarriedItem is not null
            && decoded.CarriedItem.GetCount() == 7;
    }

    private static bool TestSetEquipmentSingle()
    {
        var access = EnsureBootstrap();
        var slots = new List<KeyValuePair<EquipmentSlot, ItemStack>>
        {
            new(EquipmentSlot.HEAD, NewStack(1)),
        };
        var packet = new ClientboundSetEquipmentPacket(42, slots);
        var decoded = EncodeDecode(ClientboundSetEquipmentPacket.StreamCodec, packet, access);
        return decoded.Entity == 42
            && decoded.Slots.Count == 1
            && decoded.Slots[0].Key == EquipmentSlot.HEAD
            && decoded.Slots[0].Value.GetCount() == 1;
    }

    private static bool TestSetEquipmentMulti()
    {
        var access = EnsureBootstrap();
        var slots = new List<KeyValuePair<EquipmentSlot, ItemStack>>
        {
            new(EquipmentSlot.MAINHAND, NewStack(2)),
            new(EquipmentSlot.CHEST, NewStack(1)),
            new(EquipmentSlot.FEET, NewStack(1)),
        };
        var packet = new ClientboundSetEquipmentPacket(7, slots);
        var decoded = EncodeDecode(ClientboundSetEquipmentPacket.StreamCodec, packet, access);
        if (decoded.Entity != 7 || decoded.Slots.Count != 3) return false;
        return decoded.Slots[0].Key == EquipmentSlot.MAINHAND && decoded.Slots[0].Value.GetCount() == 2
            && decoded.Slots[1].Key == EquipmentSlot.CHEST && decoded.Slots[1].Value.GetCount() == 1
            && decoded.Slots[2].Key == EquipmentSlot.FEET && decoded.Slots[2].Value.GetCount() == 1;
    }
}
