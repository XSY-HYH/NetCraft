using NetCraft.Network;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items;

//ItemStack 物品栈对应原版 net.minecraft.world.item.ItemStack
//持有 Holder<Item> count PatchedDataComponentMap 三元组
//STREAM_CODEC 编码 count+Item.STREAM_CODEC+DataComponentPatch.STREAM_CODEC
//OPTIONAL_STREAM_CODEC 允许空栈 count<=0 视为 EMPTY
//STREAM_CODEC 在 OPTIONAL 基础上禁止空栈编解码抛 EncoderException/DecoderException
public sealed class ItemStack
{
    //Empty 空栈单例 _item=null
    public static readonly ItemStack Empty = new();

    //OptionalStreamCodec 允许空栈编解码
    public static readonly StreamCodec<RegistryFriendlyByteBuf, ItemStack> OptionalStreamCodec
        = new ItemStackOptionalStreamCodec();

    //StreamCodec 禁止空栈编解码
    public static readonly StreamCodec<RegistryFriendlyByteBuf, ItemStack> StreamCodec
        = new ItemStackStreamCodec();

    private readonly Holder<Item>? _item;
    private int _count;
    private readonly PatchedDataComponentMap _components;

    private ItemStack()
    {
        _item = null;
        _count = 0;
        _components = new PatchedDataComponentMap(DataComponentMap.Empty, DataComponentPatch.Empty);
    }

    public ItemStack(Holder<Item> item, int count, DataComponentPatch patch)
    {
        _item = item;
        _count = count;
        _components = new PatchedDataComponentMap(item.Components, patch);
    }

    //GetCount 物品数量
    public int GetCount() => _count;

    //SetCount 设置数量
    public void SetCount(int count)
    {
        _count = count;
    }

    //IsEmpty 是否空栈
    public bool IsEmpty() => _item is null || _count <= 0;

    //GetItem 获取 Item 空栈抛异常
    public Item GetItem()
    {
        if (_item is null)
            throw new InvalidOperationException("Cannot get item from empty ItemStack");
        return _item.Value;
    }

    //GetTypeHolder 获取 Holder<Item> 空栈返回 null
    public Holder<Item>? GetTypeHolder() => _item;

    //GetComponents 获取组件映射
    public PatchedDataComponentMap GetComponents() => _components;

    //Copy 复制物品栈
    public ItemStack Copy()
    {
        if (IsEmpty()) return Empty;
        return new ItemStack(_item!, _count, _components.AsPatch());
    }

    //CopyWithCount 按指定数量复制
    public ItemStack CopyWithCount(int count)
    {
        if (IsEmpty()) return Empty;
        return new ItemStack(_item!, count, _components.AsPatch());
    }
}

//ItemStackOptionalStreamCodec 允许空栈编解码对应原版 OPTIONAL_STREAM_CODEC
//count<=0 视为 EMPTY 编码空栈写 count=0
internal sealed class ItemStackOptionalStreamCodec : StreamCodec<RegistryFriendlyByteBuf, ItemStack>
{
    public ItemStack Decode(RegistryFriendlyByteBuf buf)
    {
        int count = buf.ReadVarInt();
        if (count <= 0)
            return ItemStack.Empty;
        var item = ItemCodecs.StreamCodec.Decode(buf);
        var patch = DataComponentPatch.StreamCodec.Decode(buf);
        return new ItemStack(item, count, patch);
    }

    public void Encode(RegistryFriendlyByteBuf buf, ItemStack value)
    {
        if (value.IsEmpty())
        {
            buf.WriteVarInt(0);
            return;
        }
        buf.WriteVarInt(value.GetCount());
        ItemCodecs.StreamCodec.Encode(buf, value.GetTypeHolder()!);
        DataComponentPatch.StreamCodec.Encode(buf, value.GetComponents().AsPatch());
    }
}

//ItemStackStreamCodec 禁止空栈编解码对应原版 STREAM_CODEC
//空栈 encode/decode 抛异常
internal sealed class ItemStackStreamCodec : StreamCodec<RegistryFriendlyByteBuf, ItemStack>
{
    public ItemStack Decode(RegistryFriendlyByteBuf buf)
    {
        var stack = ItemStack.OptionalStreamCodec.Decode(buf);
        if (stack.IsEmpty())
            throw new InvalidOperationException("Empty ItemStack not allowed");
        return stack;
    }

    public void Encode(RegistryFriendlyByteBuf buf, ItemStack value)
    {
        if (value.IsEmpty())
            throw new InvalidOperationException("Empty ItemStack not allowed");
        ItemStack.OptionalStreamCodec.Encode(buf, value);
    }
}
