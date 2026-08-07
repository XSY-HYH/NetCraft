namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundRecipeBookChangeSettingsPacket 数据包对应原版 ServerboundRecipeBookChangeSettingsPacket
//字段 BookType(RecipeBookType) IsOpen(boolean) IsFiltering(boolean)
public sealed record ServerboundRecipeBookChangeSettingsPacket(object BookType, bool IsOpen, bool IsFiltering) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundRecipeBookChangeSettingsPacket> StreamCodec { get; } = new RecipeBookChangeSettingsCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundRecipeBookChangeSettings;

    public void Handle(ServerGamePacketListener handler) => handler.HandleRecipeBookChangeSettingsPacket(this);

    private sealed class RecipeBookChangeSettingsCodec : StreamCodec<FriendlyByteBuf, ServerboundRecipeBookChangeSettingsPacket>
    {
        public ServerboundRecipeBookChangeSettingsPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundRecipeBookChangeSettingsPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
