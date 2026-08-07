using NetCraft.Game.World.Items;

namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSetCreativeModeSlotPacket 数据包对应原版 ServerboundSetCreativeModeSlotPacket
//字段 SlotNum(short) Stack(ItemStack)
//字段名 Stack 避免与 ItemStack 类型名冲突
public sealed record ServerboundSetCreativeModeSlotPacket(short SlotNum, ItemStack Stack) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSetCreativeModeSlotPacket> StreamCodec { get; } = new SetCreativeModeSlotCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSetCreativeModeSlot;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSetCreativeModeSlot(this);

    private sealed class SetCreativeModeSlotCodec : StreamCodec<FriendlyByteBuf, ServerboundSetCreativeModeSlotPacket>
    {
        public ServerboundSetCreativeModeSlotPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundSetCreativeModeSlotPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
