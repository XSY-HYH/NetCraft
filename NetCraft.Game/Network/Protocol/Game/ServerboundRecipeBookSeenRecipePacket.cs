namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundRecipeBookSeenRecipePacket 数据包对应原版 ServerboundRecipeBookSeenRecipePacket
//字段 Recipe(RecipeDisplayId)
public sealed record ServerboundRecipeBookSeenRecipePacket(object Recipe) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundRecipeBookSeenRecipePacket> StreamCodec { get; } = new RecipeBookSeenRecipeCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundRecipeBookSeenRecipe;

    public void Handle(ServerGamePacketListener handler) => handler.HandleRecipeBookSeenRecipePacket(this);

    private sealed class RecipeBookSeenRecipeCodec : StreamCodec<FriendlyByteBuf, ServerboundRecipeBookSeenRecipePacket>
    {
        public ServerboundRecipeBookSeenRecipePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundRecipeBookSeenRecipePacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
