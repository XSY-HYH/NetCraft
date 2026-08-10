using System.Numerics;

namespace NetCraft.Gpu;

//EntityRenderState 实体渲染状态对标原版 EntityRenderState
//持有实体渲染所需的位置朝向等数据由 EntityRenderer 从 Entity 提取填充
//PoC 简化版只含 position 和旋转完整版加 age/nameScale 等
public sealed class EntityRenderState
{
    //Position 实体世界位置
    public Vector3 Position { get; set; }
    //YRot 实体 Y 轴旋转弧度朝向
    public float YRot { get; set; }
    //XRot 实体 X 轴旋转弧度俯仰
    public float XRot { get; set; }
    //Name 实体名称用于调试
    public string Name { get; set; } = string.Empty;
}
