using NetCraft.Codec;
using NetCraft.Game.World.Level.LevelGen;
using NetCraft.Network;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Storage.Paletted;

namespace NetCraft.Game.World.Level.Chunk;

//LevelChunkSerializer 区块网络序列化器对应原版 net.minecraft.world.level.chunk.LevelChunk$Serializer
//C# partial 不能跨项目 NetCraft.Storage 不引用 NetCraft.Network
//改用独立静态类放 Game 层文件名 LevelChunk.Serializer.cs 保留原版语义
//序列化格式对齐原版 ChunkPacket 主体由 sections/heightmaps/blockEntities 三部分组成
//PaletteEntries 用 Identifier 字符串简化序列化对齐项目当前 PoC 阶段
public static class LevelChunkSerializer
{
    //Write 写入区块数据到 FriendlyByteBuf 对应原版 LevelChunk.Serializer.write
    //顺序 heightmaps NBT 简化为 0 字节占位 + sections 数组 + block entities 简化为 0 个占位
    public static void Write(FriendlyByteBuf buf, LevelChunk chunk, Func<PalettedContainer<BlockState>> statesFactory, Func<PalettedContainer<Holder<Biome>>> biomesFactory)
    {
        //Heightmaps NBT 占位对齐原版 NbtCompound 序列化此处直接写 0 字节
        buf.WriteByte(0);

        for (var i = 0; i < chunk.SectionsCount; i++)
        {
            var sectionY = chunk.MinSectionY + i;
            var section = chunk.GetSection(sectionY);
            if (section is null)
                WriteEmptySection(buf);
            else
                WriteSection(buf, section);
        }

        //Block entities 数量占位写 0 对齐原版 blockEntities 列表
        buf.WriteVarInt(0);
    }

    //Read 从 FriendlyByteBuf 读取区块数据对应原版 LevelChunk.Serializer.read
    //factory 默认 null 时回退 PalettedContainerFactory.Default 保证走真实 Unpack 路径
    //statesFactory/biomesFactory 仅用于初始化空 section 真实数据通过 SetSection 覆盖
    public static LevelChunk Read(FriendlyByteBuf buf, ChunkPos pos, int minSectionY, int sectionsCount,
        Func<PalettedContainer<BlockState>> statesFactory, Func<PalettedContainer<Holder<Biome>>> biomesFactory,
        PalettedContainerFactory? factory = null)
    {
        //Heightmaps NBT 占位读 1 字节丢弃
        _ = buf.ReadByte();

        factory ??= PalettedContainerFactory.Default;
        var chunk = new LevelChunk(pos, minSectionY, sectionsCount, statesFactory, biomesFactory);
        for (var i = 0; i < sectionsCount; i++)
        {
            var sectionY = minSectionY + i;
            ReadSectionInto(buf, chunk, sectionY, factory);
        }

        //Block entities 数量占位读 0 跳过
        _ = buf.ReadVarInt();
        return chunk;
    }

    //WriteSection 写入单个区段对应原版 LevelChunkSection.write
    //顺序 non-empty block count (short) + block states container + biomes container
    private static void WriteSection(FriendlyByteBuf buf, LevelChunkSection section)
    {
        buf.WriteShort(0);
        WritePalettedContainer(buf, section.States, WriteBlockState);
        WritePalettedContainer(buf, section.Biomes, WriteBiome);
    }

    //WriteEmptySection 空区段写入全默认值对应原版空 section 序列化
    private static void WriteEmptySection(FriendlyByteBuf buf)
    {
        buf.WriteShort(0);
        buf.WriteByte(0);
        buf.WriteVarInt(1);
        buf.WriteIdentifier(Identifier.WithDefaultNamespace("air"));
        buf.WriteVarInt(0);
        buf.WriteByte(0);
        buf.WriteVarInt(1);
        buf.WriteIdentifier(Identifier.WithDefaultNamespace("plains"));
        buf.WriteVarInt(0);
    }

    //ReadSectionInto 从 buf 读取区段数据写入 chunk 对应原版 section 反序列化
    //走 factory.Unpack 真实路径重建 storage 与 palette
    //readElement 优先用 factory.LookupBlockState/LookupBiome 查注册 Block 找不到再回退 BuiltInRegistries
    private static void ReadSectionInto(FriendlyByteBuf buf, LevelChunk chunk, int sectionY,
        PalettedContainerFactory factory)
    {
        _ = buf.ReadShort();
        Func<FriendlyByteBuf, BlockState> readBlockState = b => ReadBlockState(b, factory);
        Func<FriendlyByteBuf, Holder<Biome>> readBiome = b => ReadBiome(b, factory);
        var states = ReadPalettedContainer(buf, factory.UnpackBlockStates, readBlockState);
        var biomes = ReadPalettedContainer(buf, factory.UnpackBiomes, readBiome);
        var section = new LevelChunkSection(states, biomes);
        chunk.SetSection(sectionY, section);
    }

    //WritePalettedContainer 写入 PalettedContainer 网络序列化
    //格式 bitsPerEntry (byte) + palette count (VarInt) + palette entries + storage count (VarInt) + long[]
    private static void WritePalettedContainer<T>(
        FriendlyByteBuf buf,
        PalettedContainer<T> container,
        Action<FriendlyByteBuf, T> writeElement)
    {
        var packed = container.GetPackedData();
        var bits = packed.BitsPerEntry == PackedData<T>.UnknownBitsPerEntry ? 0 : packed.BitsPerEntry;
        buf.WriteByte((byte)bits);

        var palette = packed.PaletteEntries;
        buf.WriteVarInt(palette.Count);
        foreach (var entry in palette)
            writeElement(buf, entry);

        if (packed.Storage.IsPresent)
        {
            var values = packed.Storage.Get();
            buf.WriteVarInt(values.Length);
            foreach (var v in values)
                buf.WriteLong(v);
        }
        else
        {
            buf.WriteVarInt(0);
        }
    }

    //ReadPalettedContainer 读 palette 与 storage 后构造 PackedData 走 factory.Unpack 反序列化
    private static PalettedContainer<T> ReadPalettedContainer<T>(
        FriendlyByteBuf buf,
        Func<PackedData<T>, PalettedContainer<T>> unpack,
        Func<FriendlyByteBuf, T> readElement)
    {
        var bits = buf.ReadByte();
        var paletteCount = buf.ReadVarInt();
        var palette = new List<T>(paletteCount);
        for (var i = 0; i < paletteCount; i++)
            palette.Add(readElement(buf));

        var storageCount = buf.ReadVarInt();
        long[] storage;
        if (storageCount > 0)
        {
            storage = new long[storageCount];
            for (var i = 0; i < storageCount; i++)
                storage[i] = buf.ReadLong();
        }
        else
        {
            storage = Array.Empty<long>();
        }

        var packed = new PackedData<T>(palette, Optional<long[]>.Of(storage), bits);
        return unpack(packed);
    }

    //WriteBlockState 写入 BlockState 为 Owner.Identifier 字符串
    private static void WriteBlockState(FriendlyByteBuf buf, BlockState state)
        => buf.WriteIdentifier(state.Owner.Id);

    //WriteBiome 写入 Holder<Biome> 为 Biome.Identifier 字符串
    private static void WriteBiome(FriendlyByteBuf buf, Holder<Biome> holder)
        => buf.WriteIdentifier(holder.Value.Id);

    //ReadBlockState 从 buf 读取 Identifier 后优先查 factory.LookupBlockState
    //factory 找不到再回退 BuiltInRegistries.BLOCK 都找不到返回 default
    private static BlockState ReadBlockState(FriendlyByteBuf buf, PalettedContainerFactory? factory = null)
    {
        var id = buf.ReadIdentifier();
        if (factory is not null)
        {
            var looked = factory.LookupBlockState(id);
            if (looked is not null) return looked.Value;
        }
        var block = BuiltInRegistries.BLOCK.GetValue(id);
        return block is not null ? block.DefaultBlockState : default;
    }

    //ReadBiome 从 buf 读取 Identifier 后优先查 factory.LookupBiome
    //factory 找不到再回退 PlainsBiome 占位真实接入需 BuiltInRegistries.BIOME 就绪
    private static Holder<Biome> ReadBiome(FriendlyByteBuf buf, PalettedContainerFactory? factory = null)
    {
        var id = buf.ReadIdentifier();
        if (factory is not null)
        {
            var looked = factory.LookupBiome(id);
            if (looked is not null) return looked;
        }
        return Holder<Biome>.Direct(new PlainsBiome());
    }
}
