namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundCommandSuggestionPacket 数据包对应原版 ServerboundCommandSuggestionPacket
//字段 Id(int) Command(String)
public sealed record ServerboundCommandSuggestionPacket(int Id, string Command) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundCommandSuggestionPacket> StreamCodec { get; } = new CommandSuggestionCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundCommandSuggestion;

    public void Handle(ServerGamePacketListener handler) => handler.HandleCustomCommandSuggestions(this);

    private sealed class CommandSuggestionCodec : StreamCodec<FriendlyByteBuf, ServerboundCommandSuggestionPacket>
    {
        public ServerboundCommandSuggestionPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundCommandSuggestionPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
