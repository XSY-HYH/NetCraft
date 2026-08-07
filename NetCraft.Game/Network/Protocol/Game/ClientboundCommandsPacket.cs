namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundCommandsPacket 命令树包对应原版 ClientboundCommandsPacket
//字段 root CommandNode 业务类型占位
public sealed record ClientboundCommandsPacket(object Root) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundCommandsPacket> StreamCodec { get; } = new CommandsCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundCommands;

    public void Handle(ClientGamePacketListener handler) => handler.HandleCommands(this);

    private sealed class CommandsCodec : StreamCodec<FriendlyByteBuf, ClientboundCommandsPacket>
    {
        public ClientboundCommandsPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("CommandNode 业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundCommandsPacket value)
            => throw new NotImplementedException("CommandNode 业务类型待实现");
    }
}
