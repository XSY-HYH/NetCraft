using NetCraft.Game.World.Entity;
using NetCraft.Game.World.Items;
using NetCraft.Network;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetEquipmentPacket 装备设置包对应原版 ClientboundSetEquipmentPacket
//字段 Entity(VarInt) Slots(List<KeyValuePair<EquipmentSlot, ItemStack>>)
//Slots 用 packed byte 编码低 7 bit 为 slot id 高位 0x80 为 hasMore 标记
//slot id > 7 或空栈视为列表结束
public sealed record ClientboundSetEquipmentPacket(int Entity, List<KeyValuePair<EquipmentSlot, ItemStack>> Slots) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<RegistryFriendlyByteBuf, ClientboundSetEquipmentPacket> StreamCodec { get; } = new SetEquipmentCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetEquipment;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetEquipment(this);

    private sealed class SetEquipmentCodec : StreamCodec<RegistryFriendlyByteBuf, ClientboundSetEquipmentPacket>
    {
        public ClientboundSetEquipmentPacket Decode(RegistryFriendlyByteBuf buf)
        {
            int entityId = buf.ReadVarInt();
            var slots = new List<KeyValuePair<EquipmentSlot, ItemStack>>();
            while (true)
            {
                byte b = buf.ReadByte();
                int slotId = b & 0x7F;
                if (slotId > 7) break;
                var slot = (EquipmentSlot)slotId;
                var stack = ItemStack.OptionalStreamCodec.Decode(buf);
                if (stack.IsEmpty()) break;
                slots.Add(new(slot, stack));
                if ((b & 0x80) == 0) break;
            }
            return new(entityId, slots);
        }

        public void Encode(RegistryFriendlyByteBuf buf, ClientboundSetEquipmentPacket value)
        {
            buf.WriteVarInt(value.Entity);
            for (int i = 0; i < value.Slots.Count; i++)
            {
                var pair = value.Slots[i];
                bool isLast = i == value.Slots.Count - 1;
                byte b = (byte)((int)pair.Key | (isLast ? 0 : 0x80));
                buf.WriteByte(b);
                ItemStack.OptionalStreamCodec.Encode(buf, pair.Value);
            }
        }
    }
}
