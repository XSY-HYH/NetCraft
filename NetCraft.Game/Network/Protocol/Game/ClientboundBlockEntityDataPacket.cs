using NetCraft.Primitives;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundBlockEntityDataPacket 方块实体数据包对应原版 ClientboundBlockEntityDataPacket
//字段 pos BlockPos type BlockEntityType registry VarInt 占位 tag CompoundTag 用 byte[] 占位
public sealed record ClientboundBlockEntityDataPacket(BlockPos Pos, int Kind, byte[] Tag) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundBlockEntityDataPacket> StreamCodec { get; } = new BlockEntityDataCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundBlockEntityData;

    public void Handle(ClientGamePacketListener handler) => handler.HandleBlockEntityData(this);

    private sealed class BlockEntityDataCodec : StreamCodec<FriendlyByteBuf, ClientboundBlockEntityDataPacket>
    {
        public ClientboundBlockEntityDataPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadBlockPos(), buf.ReadVarInt(), buf.ReadByteArray());

        public void Encode(FriendlyByteBuf buf, ClientboundBlockEntityDataPacket value)
        {
            buf.WriteBlockPos(value.Pos);
            buf.WriteVarInt(value.Kind);
            buf.WriteByteArray(value.Tag);
        }
    }
}
