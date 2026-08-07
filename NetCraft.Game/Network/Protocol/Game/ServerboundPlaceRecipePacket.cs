namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundPlaceRecipePacket 数据包对应原版 ServerboundPlaceRecipePacket
//字段 ContainerId(int) Recipe(RecipeDisplayId) UseMaxItems(boolean)
public sealed record ServerboundPlaceRecipePacket(int ContainerId, object Recipe, bool UseMaxItems) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundPlaceRecipePacket> StreamCodec { get; } = new PlaceRecipeCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundPlaceRecipe;

    public void Handle(ServerGamePacketListener handler) => handler.HandlePlaceRecipe(this);

    private sealed class PlaceRecipeCodec : StreamCodec<FriendlyByteBuf, ServerboundPlaceRecipePacket>
    {
        public ServerboundPlaceRecipePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundPlaceRecipePacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
