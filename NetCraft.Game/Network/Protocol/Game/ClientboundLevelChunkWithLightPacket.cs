using NetCraft.Game.Bootstrap;
using NetCraft.Game.World.Level.Chunk;
using NetCraft.Network;
using NetCraft.Primitives;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Storage.Paletted;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundLevelChunkWithLightPacket 带光照区块包对应原版 ClientboundLevelChunkWithLightPacket
//字段 X(int) Z(int) ChunkData(LevelChunk) LightData(ClientboundLightUpdatePacketData)
//阶段 E ChunkData 接入 LevelChunkSerializer + LightData 升级为 ClientboundLightUpdatePacketData 真实类型
public sealed record ClientboundLevelChunkWithLightPacket(int X, int Z, LevelChunk? ChunkData, ClientboundLightUpdatePacketData? LightData) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundLevelChunkWithLightPacket> StreamCodec { get; } = new LevelChunkWithLightCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundLevelChunkWithLight;

    public void Handle(ClientGamePacketListener handler) => handler.HandleLevelChunkWithLight(this);

    private sealed class LevelChunkWithLightCodec : StreamCodec<FriendlyByteBuf, ClientboundLevelChunkWithLightPacket>
    {
        public ClientboundLevelChunkWithLightPacket Decode(FriendlyByteBuf buf)
        {
            GameBootstrap.Bootstrap();
            var x = buf.ReadVarInt();
            var z = buf.ReadVarInt();
            var pos = new ChunkPos(x, z);
            var factory = PalettedContainerFactory.Default;
            var chunk = LevelChunkSerializer.Read(
                buf, pos, DefaultMinSectionY, DefaultSectionsCount,
                factory.CreateForBlockStates, factory.CreateForBiomes);
            var light = ClientboundLightUpdatePacketData.Read(buf);
            return new ClientboundLevelChunkWithLightPacket(x, z, chunk, light);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundLevelChunkWithLightPacket value)
        {
            GameBootstrap.Bootstrap();
            buf.WriteVarInt(value.X);
            buf.WriteVarInt(value.Z);
            if (value.ChunkData is null)
                return;
            var factory = PalettedContainerFactory.Default;
            LevelChunkSerializer.Write(buf, value.ChunkData, factory.CreateForBlockStates, factory.CreateForBiomes);
            var light = value.LightData ?? ClientboundLightUpdatePacketData.Empty;
            light.Write(buf);
        }

        //DefaultMinSectionY/DefaultSectionsCount 主世界默认参数对应原版 -64..320
        //真实接入维度配置后由 DimensionType 派生此处简化为常量
        private const int DefaultMinSectionY = -4;
        private const int DefaultSectionsCount = 24;
    }
}
