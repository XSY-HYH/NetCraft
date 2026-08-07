namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundCommandSuggestionsPacket 命令建议包对应原版 ClientboundCommandSuggestionsPacket
//字段 Id(int) Start(int) Length(int) Suggestions(List<Entry>)
public sealed record ClientboundCommandSuggestionsPacket(int Id, int Start, int Length, object Suggestions) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundCommandSuggestionsPacket> StreamCodec { get; } = new CommandSuggestionsCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundCommandSuggestions;

    public void Handle(ClientGamePacketListener handler) => handler.HandleCommandSuggestions(this);

    private sealed class CommandSuggestionsCodec : StreamCodec<FriendlyByteBuf, ClientboundCommandSuggestionsPacket>
    {
        public ClientboundCommandSuggestionsPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundCommandSuggestionsPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
