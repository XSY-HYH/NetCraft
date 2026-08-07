using NetCraft.Game.Bootstrap;
using NetCraft.Game.World.Level.Chunk;
using NetCraft.Network;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Storage.Paletted;

namespace NetCraft.Test.Modules;

//LevelChunkSerializer 区块网络序列化测试覆盖 Write/Read 往返
//验证区块数据经过 FriendlyByteBuf 序列化后可正确还原
internal static class LevelChunkSerializerTests
{
    public const string Module = "levelchunkserializer";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("Round trip preserves chunk pos", TestRoundTripPreservesPos);
        yield return ("Round trip preserves block state", TestRoundTripPreservesBlockState);
        yield return ("Round trip empty chunk", TestRoundTripEmptyChunk);
        yield return ("Packet StreamCodec encode decode", TestPacketStreamCodec);
        yield return ("LightData round trip preserves bytes", TestLightDataRoundTrip);
    }

    //TestRoundTripPreservesPos 写入后读出 LevelChunk 的 Pos 不变
    private static bool TestRoundTripPreservesPos()
    {
        GameBootstrap.Bootstrap();
        var (factory, _) = NewFactory();
        var pos = new ChunkPos(3, -5);
        var chunk = new LevelChunk(pos, -1, 2, factory.CreateForBlockStates, factory.CreateForBiomes);

        var writeBuf = new FriendlyByteBuf();
        LevelChunkSerializer.Write(writeBuf, chunk, factory.CreateForBlockStates, factory.CreateForBiomes);

        var readBuf = new FriendlyByteBuf(writeBuf.ToArray());
        var restored = LevelChunkSerializer.Read(readBuf, pos, -1, 2,
            factory.CreateForBlockStates, factory.CreateForBiomes, factory);
        return restored.Pos.X == 3 && restored.Pos.Z == -5;
    }

    //TestRoundTripPreservesBlockState 写入 Stone 后读出仍为 Stone
    private static bool TestRoundTripPreservesBlockState()
    {
        GameBootstrap.Bootstrap();
        var (factory, stoneState) = NewFactory();
        var pos = new ChunkPos(0, 0);
        var chunk = new LevelChunk(pos, -1, 2, factory.CreateForBlockStates, factory.CreateForBiomes);

        chunk.SetBlockState(0, 0, 0, 0, stoneState);

        var writeBuf = new FriendlyByteBuf();
        LevelChunkSerializer.Write(writeBuf, chunk, factory.CreateForBlockStates, factory.CreateForBiomes);

        var readBuf = new FriendlyByteBuf(writeBuf.ToArray());
        var restored = LevelChunkSerializer.Read(readBuf, pos, -1, 2,
            factory.CreateForBlockStates, factory.CreateForBiomes, factory);
        var state = restored.GetBlockState(0, 0, 0);
        return state == stoneState;
    }

    //TestRoundTripEmptyChunk 空 LevelChunk 往返不出错
    private static bool TestRoundTripEmptyChunk()
    {
        GameBootstrap.Bootstrap();
        var (factory, _) = NewFactory();
        var pos = new ChunkPos(0, 0);
        var chunk = new LevelChunk(pos, -1, 2, factory.CreateForBlockStates, factory.CreateForBiomes);

        var writeBuf = new FriendlyByteBuf();
        LevelChunkSerializer.Write(writeBuf, chunk, factory.CreateForBlockStates, factory.CreateForBiomes);

        var readBuf = new FriendlyByteBuf(writeBuf.ToArray());
        var restored = LevelChunkSerializer.Read(readBuf, pos, -1, 2,
            factory.CreateForBlockStates, factory.CreateForBiomes, factory);
        return restored is not null;
    }

    //TestPacketStreamCodec ClientboundLevelChunkWithLightPacket StreamCodec 端到端往返
    //StreamCodec 内部硬编码 -4/24 维度chunk 创建时对齐
    private static bool TestPacketStreamCodec()
    {
        GameBootstrap.Bootstrap();
        var (factory, _) = NewFactory();
        var pos = new ChunkPos(7, -3);
        var chunk = new LevelChunk(pos, -4, 24, factory.CreateForBlockStates, factory.CreateForBiomes);

        var packet = new NetCraft.Game.Network.Protocol.Game.ClientboundLevelChunkWithLightPacket(
            pos.X, pos.Z, chunk, null);

        var writeBuf = new FriendlyByteBuf();
        NetCraft.Game.Network.Protocol.Game.ClientboundLevelChunkWithLightPacket.StreamCodec.Encode(writeBuf, packet);

        var readBuf = new FriendlyByteBuf(writeBuf.ToArray());
        var restored = NetCraft.Game.Network.Protocol.Game.ClientboundLevelChunkWithLightPacket.StreamCodec.Decode(readBuf);
        return restored.X == 7 && restored.Z == -3 && restored.ChunkData is not null;
    }

    //TestLightDataRoundTrip 验证 ClientboundLightUpdatePacketData 端到端往返保留字节
    //构造带非空 SkyLight/BlockLight 的 LightData 经 packet 编解码后字段应一致
    private static bool TestLightDataRoundTrip()
    {
        GameBootstrap.Bootstrap();
        var (factory, _) = NewFactory();
        var pos = new ChunkPos(1, 2);
        var chunk = new LevelChunk(pos, -4, 24, factory.CreateForBlockStates, factory.CreateForBiomes);
        var sky = new byte[] { 1, 2, 3 };
        var block = new byte[] { 4, 5, 6, 7 };
        var light = new NetCraft.Game.Network.Protocol.Game.ClientboundLightUpdatePacketData(true, sky, block);

        var packet = new NetCraft.Game.Network.Protocol.Game.ClientboundLevelChunkWithLightPacket(
            pos.X, pos.Z, chunk, light);

        var writeBuf = new FriendlyByteBuf();
        NetCraft.Game.Network.Protocol.Game.ClientboundLevelChunkWithLightPacket.StreamCodec.Encode(writeBuf, packet);

        var readBuf = new FriendlyByteBuf(writeBuf.ToArray());
        var restored = NetCraft.Game.Network.Protocol.Game.ClientboundLevelChunkWithLightPacket.StreamCodec.Decode(readBuf);
        return restored.LightData is not null
            && restored.LightData.TrustEdges
            && restored.LightData.SkyLight.SequenceEqual(sky)
            && restored.LightData.BlockLight.SequenceEqual(block);
    }

    //NewFactory 注册 mock block 与 biome 返回可用工厂与 stone 的 BlockState
    //真实 Block 的 BlockState.Id 在 BlockStateRegistry 未初始化时可能重复
    //改用 MockBlock 显式调 BlockStateRegistry.Register 分配唯一 Id
    private static (DefaultPalettedContainerFactory factory, BlockState stoneState) NewFactory()
    {
        var factory = new DefaultPalettedContainerFactory();
        factory.RegisterBlock(new MockBlock(Identifier.WithDefaultNamespace("air")));
        var stoneBlock = new MockBlock(Identifier.WithDefaultNamespace("stone"));
        factory.RegisterBlock(stoneBlock);
        factory.RegisterBlock(new MockBlock(Identifier.WithDefaultNamespace("dirt")));
        factory.RegisterBlock(new MockBlock(Identifier.WithDefaultNamespace("grass_block")));
        factory.RegisterBiome(Holder<Biome>.Direct(new MockBiome(Identifier.WithDefaultNamespace("plains"))));
        return (factory, stoneBlock.DefaultBlockState);
    }

    //MockBlock 测试用 Block 子类显式注册 BlockStateRegistry 分配唯一 Id
    private sealed class MockBlock : NetCraft.Registry.Block
    {
        public override Identifier Id { get; }
        public override BlockState DefaultBlockState { get; }

        public MockBlock(Identifier id)
        {
            Id = id;
            var state = BlockStateRegistry.Register(this, Array.Empty<PropertyBase>(), Array.Empty<object?>());
            BlockStateRegistry.InitializeNeighbors(state.Id, Array.Empty<int[]>());
            DefaultBlockState = state;
        }
    }

    //MockBiome 测试用 Biome 子类
    private sealed class MockBiome : Biome
    {
        public override Identifier Id { get; }
        public MockBiome(Identifier id) => Id = id;
    }
}
