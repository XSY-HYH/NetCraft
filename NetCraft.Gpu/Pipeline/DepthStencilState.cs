namespace NetCraft.Gpu.Pipeline;

//DepthStencilState 深度模板状态对标原版 DepthStencilState record
//depthTest=null 表示不启用深度测试 writeDepth 控制是否写入深度缓冲
//depthBiasScaleFactor/depthBiasConstant 用于消除 z-fighting
public readonly record struct DepthStencilState(
    CompareOp? DepthTest,
    bool WriteDepth,
    float DepthBiasScaleFactor,
    float DepthBiasConstant)
{
    //DEFAULT 默认深度状态 GEQUAL 写入无偏移
    public static readonly DepthStencilState DEFAULT = new(CompareOp.GreaterOrEqual, true, 0f, 0f);

    public DepthStencilState(CompareOp depthTest, bool depthWrite)
        : this(depthTest, depthWrite, 0f, 0f) { }
}
