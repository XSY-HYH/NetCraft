using System.Numerics;
using NetCraft.Gpu;
using NetCraft.Gpu.Pipeline;

namespace NetCraft.Test.Modules;

//GuiRenderStateTests RenderState 数据层单元测试对标原版 26.2 行为
//覆盖 BuildVertices 顶点构造 GuiRenderState 层级 strata blur 分段排序
internal static class GuiRenderStateTests
{
    public const string Module = "guirenderstate";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("BlitRenderState.BuildVertices writes 4 vertices", TestBlitBuildVertices);
        yield return ("ColoredRectangleRenderState.BuildVertices writes diagonal colors", TestColoredBuildVertices);
        yield return ("TiledBlitRenderState.BuildVertices tiles geometry", TestTiledBlitBuildVertices);
        yield return ("ScreenRectangle.TransformMaxBounds identity returns same rect", TestTransformMaxBoundsIdentity);
        yield return ("ScreenRectangle.Intersect returns null when disjoint", TestIntersectDisjointNull);
        yield return ("ScreenRectangle.Intersect returns overlap region", TestIntersectOverlap);
        yield return ("ScreenRectangle.Encompasses true for inner rect", TestEncompassesInner);
        yield return ("GuiRenderState.ForEachElement traverses in add order", TestForEachAddOrder);
        yield return ("GuiRenderState intersecting element auto up", TestIntersectAutoUp);
        yield return ("GuiRenderState encompassed element auto up", TestEncompassedAutoUp);
        yield return ("GuiRenderState.NextStratum separates layers", TestNextStratumLayers);
        yield return ("GuiRenderState blur split TraverseRange", TestBlurSplitTraverseRange);
        yield return ("GuiRenderState.SortElements sorts per node", TestSortElementsPerNode);
        yield return ("GuiRenderState.Reset clears strata", TestResetClearsStrata);
        yield return ("GuiRenderState.AddGlyphToCurrentLayer bypasses bounds tree", TestGlyphBypassBounds);
        yield return ("GuiRenderState.Snapshot preserves element count", TestSnapshotPreservesElementCount);
        yield return ("GuiRenderState.Snapshot independent after Reset", TestSnapshotIndependentAfterReset);
        yield return ("GuiRenderState.Snapshot deep clones Up chain", TestSnapshotDeepClonesUpChain);
        yield return ("GuiRenderState.Snapshot empty state yields no elements", TestSnapshotEmptyState);
        yield return ("GuiRenderState.Snapshot multi level Up chain order", TestSnapshotMultiLevelUpChain);
        yield return ("GuiRenderState.Snapshot preserves blur split range", TestSnapshotPreservesBlurSplit);
    }

    //MakePipeline 测试用声明式 RenderPipeline 桩不依赖 Vulkan
    //直接复用 RenderPipelines.GUI 共享 sortKey 与生产路径一致
    private static RenderPipeline MakePipeline() => RenderPipelines.GUI;

    //CountingConsumer 记录所有顶点用于断言 BuildVertices 行为
    private sealed class CountingConsumer : IVertexConsumer
    {
        public List<(float X, float Y, float U, float V, int Color)> Vertices { get; } = new();
        public void AddVertexWith2DPose(Matrix3x2 pose, float x, float y, float u, float v, int color)
        {
            var p = Vector2.Transform(new Vector2(x, y), pose);
            Vertices.Add((p.X, p.Y, u, v, color));
        }
    }

    private static ScreenRectangle FullScreen() => new(0, 0, 800, 600);

    //TestBlitBuildVertices 验证 BlitRenderState 写 4 顶点位置经 pose bake
    private static bool TestBlitBuildVertices()
    {
        var pipeline = MakePipeline();
        const int white = unchecked((int)0xFFFFFFFF);
        var blit = new BlitRenderState(pipeline, TextureSetup.NoTexture, Matrix3x2.Identity,
            10, 20, 110, 220, 0f, 1f, 0f, 1f, white, FullScreen());
        var consumer = new CountingConsumer();
        blit.BuildVertices(consumer);
        if (consumer.Vertices.Count != 4) return false;
        return consumer.Vertices[0] == (10, 20, 0f, 0f, white)
            && consumer.Vertices[2] == (110, 220, 1f, 1f, white);
    }

    //TestColoredBuildVertices 验证对角双色 col1 在左上右下 col2 在左下右上
    private static bool TestColoredBuildVertices()
    {
        var pipeline = MakePipeline();
        var rect = new ColoredRectangleRenderState(pipeline, TextureSetup.NoTexture, Matrix3x2.Identity,
            0, 0, 100, 100, /*col1*/ 0x11111111, /*col2*/ 0x22222222, FullScreen());
        var consumer = new CountingConsumer();
        rect.BuildVertices(consumer);
        if (consumer.Vertices.Count != 4) return false;
        return consumer.Vertices[0].Color == 0x11111111
            && consumer.Vertices[1].Color == 0x22222222
            && consumer.Vertices[2].Color == 0x22222222
            && consumer.Vertices[3].Color == 0x11111111;
    }

    //TestTiledBlitBuildVertices 验证 80x80 区域用 40x40 tile 平铺产生 4 tile * 4 顶点=16 顶点
    private static bool TestTiledBlitBuildVertices()
    {
        var pipeline = MakePipeline();
        var tiled = new TiledBlitRenderState(pipeline, TextureSetup.NoTexture, Matrix3x2.Identity,
            tileWidth: 40, tileHeight: 40, x0: 0, y0: 0, x1: 80, y1: 80,
            u0: 0f, u1: 1f, v0: 0f, v1: 1f, color: unchecked((int)0xFFFFFFFF), FullScreen());
        var consumer = new CountingConsumer();
        tiled.BuildVertices(consumer);
        return consumer.Vertices.Count == 16;
    }

    //TestTransformMaxBoundsIdentity 验证 identity pose 时 bounds 等于原矩形
    private static bool TestTransformMaxBoundsIdentity()
    {
        var rect = new ScreenRectangle(10, 20, 100, 200);
        var bounds = rect.TransformMaxBounds(Matrix3x2.Identity);
        return bounds == new ScreenRectangle(10, 20, 100, 200);
    }

    //TestIntersectDisjointNull 验证不相交返回 null
    private static bool TestIntersectDisjointNull()
    {
        var a = new ScreenRectangle(0, 0, 10, 10);
        var b = new ScreenRectangle(20, 20, 10, 10);
        return a.Intersect(b) == null;
    }

    //TestIntersectOverlap 验证相交返回交集矩形
    private static bool TestIntersectOverlap()
    {
        var a = new ScreenRectangle(0, 0, 20, 20);
        var b = new ScreenRectangle(10, 10, 20, 20);
        var inter = a.Intersect(b);
        return inter == new ScreenRectangle(10, 10, 10, 10);
    }

    //TestEncompassesInner 验证大矩形包含小矩形小不包含大
    private static bool TestEncompassesInner()
    {
        var big = new ScreenRectangle(0, 0, 100, 100);
        var small = new ScreenRectangle(10, 10, 20, 20);
        return big.Encompasses(small) && !small.Encompasses(big);
    }

    //TestForEachAddOrder 验证不相交 3 元素按添加顺序遍历
    private static bool TestForEachAddOrder()
    {
        var pipeline = MakePipeline();
        var state = new GuiRenderState();
        var e1 = MakeBlit(pipeline, 0, 0, 10, 10);
        var e2 = MakeBlit(pipeline, 100, 0, 10, 10);
        var e3 = MakeBlit(pipeline, 200, 0, 10, 10);
        state.AddGuiElement(e1);
        state.AddGuiElement(e2);
        state.AddGuiElement(e3);

        var visited = new List<GuiElementRenderState>();
        state.ForEachElement(visited.Add, TraverseRange.All);
        return visited.Count == 3 && visited[0] == e1 && visited[1] == e2 && visited[2] == e3;
    }

    //TestIntersectAutoUp 验证相交元素自动 up 第 2 个加到上层深度优先先访问下层
    private static bool TestIntersectAutoUp()
    {
        var pipeline = MakePipeline();
        var state = new GuiRenderState();
        var e1 = MakeBlit(pipeline, 0, 0, 100, 100);
        var e2 = MakeBlit(pipeline, 50, 50, 100, 100);
        state.AddGuiElement(e1);
        state.AddGuiElement(e2);

        var visited = new List<GuiElementRenderState>();
        state.ForEachElement(visited.Add, TraverseRange.All);
        return visited.Count == 2 && visited[0] == e1 && visited[1] == e2;
    }

    //TestEncompassedAutoUp 验证被包含元素自动 up 加到上层
    private static bool TestEncompassedAutoUp()
    {
        var pipeline = MakePipeline();
        var state = new GuiRenderState();
        var big = MakeBlit(pipeline, 0, 0, 200, 200);
        var small = MakeBlit(pipeline, 50, 50, 20, 20);
        state.AddGuiElement(big);
        state.AddGuiElement(small);

        var visited = new List<GuiElementRenderState>();
        state.ForEachElement(visited.Add, TraverseRange.All);
        return visited.Count == 2 && visited[0] == big && visited[1] == small;
    }

    //TestNextStratumLayers 验证多 stratum 元素按 stratum 顺序遍历
    private static bool TestNextStratumLayers()
    {
        var pipeline = MakePipeline();
        var state = new GuiRenderState();
        var bg = MakeBlit(pipeline, 0, 0, 10, 10);
        state.AddGuiElement(bg);
        state.NextStratum();
        var fg = MakeBlit(pipeline, 0, 0, 10, 10);
        state.AddGuiElement(fg);

        var visited = new List<GuiElementRenderState>();
        state.ForEachElement(visited.Add, TraverseRange.All);
        return visited.Count == 2 && visited[0] == bg && visited[1] == fg;
    }

    //TestBlurSplitTraverseRange 验证 blur 标记后 BeforeBlur 与 AfterBlur 分别遍历对应 stratum
    private static bool TestBlurSplitTraverseRange()
    {
        var pipeline = MakePipeline();
        var state = new GuiRenderState();
        var before = MakeBlit(pipeline, 0, 0, 10, 10);
        state.AddGuiElement(before);
        state.NextStratum();
        state.BlurBeforeThisStratum();
        var after = MakeBlit(pipeline, 0, 0, 10, 10);
        state.AddGuiElement(after);

        var beforeList = new List<GuiElementRenderState>();
        state.ForEachElement(beforeList.Add, TraverseRange.BeforeBlur);
        var afterList = new List<GuiElementRenderState>();
        state.ForEachElement(afterList.Add, TraverseRange.AfterBlur);

        return beforeList.Count == 1 && beforeList[0] == before
            && afterList.Count == 1 && afterList[0] == after;
    }

    //TestSortElementsPerNode 验证 SortElements 按 comparator 对每个 Node 内 elementStates 排序
    private static bool TestSortElementsPerNode()
    {
        var pipeline = MakePipeline();
        var state = new GuiRenderState();
        //3 个不相交元素按 X 倒序添加 comparator 按 X 升序排序后应变为 X 升序
        var e3 = MakeBlit(pipeline, 200, 0, 10, 10);
        var e2 = MakeBlit(pipeline, 100, 0, 10, 10);
        var e1 = MakeBlit(pipeline, 0, 0, 10, 10);
        state.AddGuiElement(e3);
        state.AddGuiElement(e2);
        state.AddGuiElement(e1);

        state.SortElements((a, b) => a.Bounds.X.CompareTo(b.Bounds.X));

        var visited = new List<GuiElementRenderState>();
        state.ForEachElement(visited.Add, TraverseRange.All);
        return visited.Count == 3 && visited[0] == e1 && visited[1] == e2 && visited[2] == e3;
    }

    //TestResetClearsStrata 验证 Reset 后所有 strata 清空仅留首 stratum
    private static bool TestResetClearsStrata()
    {
        var pipeline = MakePipeline();
        var state = new GuiRenderState();
        state.AddGuiElement(MakeBlit(pipeline, 0, 0, 10, 10));
        state.NextStratum();
        state.AddGuiElement(MakeBlit(pipeline, 0, 0, 10, 10));

        var beforeReset = new List<GuiElementRenderState>();
        state.ForEachElement(beforeReset.Add, TraverseRange.All);
        if (beforeReset.Count != 2) return false;

        state.Reset();
        var afterReset = new List<GuiElementRenderState>();
        state.ForEachElement(afterReset.Add, TraverseRange.All);
        return afterReset.Count == 0;
    }

    //TestGlyphBypassBounds 验证 AddGlyphToCurrentLayer 直接加到 current 不参与 findAppropriateNode
    private static bool TestGlyphBypassBounds()
    {
        var pipeline = MakePipeline();
        var state = new GuiRenderState();
        var glyph1 = MakeBlit(pipeline, 0, 0, 10, 10);
        var glyph2 = MakeBlit(pipeline, 50, 50, 10, 10);
        state.AddGlyphToCurrentLayer(glyph1);
        state.AddGlyphToCurrentLayer(glyph2);

        var visited = new List<GuiElementRenderState>();
        state.ForEachElement(visited.Add, TraverseRange.All);
        return visited.Count == 2 && visited[0] == glyph1 && visited[1] == glyph2;
    }

    //TestSnapshotPreservesElementCount 验证快照元素数量与原状态一致
    private static bool TestSnapshotPreservesElementCount()
    {
        var pipeline = MakePipeline();
        var state = new GuiRenderState();
        state.AddGuiElement(MakeBlit(pipeline, 0, 0, 10, 10));
        state.AddGuiElement(MakeBlit(pipeline, 100, 0, 10, 10));
        var snap = state.Snapshot();
        var visited = new List<GuiElementRenderState>();
        snap.ForEachElement(visited.Add, TraverseRange.All);
        return visited.Count == 2;
    }

    //TestSnapshotIndependentAfterReset 验证原状态 Reset 后快照内容不受影响
    //证明快照的 List 容器独立 Reset 清空原 _strata 不波及快照副本
    private static bool TestSnapshotIndependentAfterReset()
    {
        var pipeline = MakePipeline();
        var state = new GuiRenderState();
        state.AddGuiElement(MakeBlit(pipeline, 0, 0, 10, 10));
        var snap = state.Snapshot();
        state.Reset();
        state.AddGuiElement(MakeBlit(pipeline, 50, 50, 10, 10));

        var snapVisited = new List<GuiElementRenderState>();
        snap.ForEachElement(snapVisited.Add, TraverseRange.All);
        var stateVisited = new List<GuiElementRenderState>();
        state.ForEachElement(stateVisited.Add, TraverseRange.All);
        return snapVisited.Count == 1 && stateVisited.Count == 1;
    }

    //TestSnapshotDeepClonesUpChain 验证相交元素自动 Up 的多层 Node 结构深拷贝正确
    //快照遍历顺序应与原状态一致深度优先下层先访问
    private static bool TestSnapshotDeepClonesUpChain()
    {
        var pipeline = MakePipeline();
        var state = new GuiRenderState();
        var e1 = MakeBlit(pipeline, 0, 0, 100, 100);
        var e2 = MakeBlit(pipeline, 50, 50, 100, 100);
        state.AddGuiElement(e1);
        state.AddGuiElement(e2);
        var snap = state.Snapshot();

        var snapVisited = new List<GuiElementRenderState>();
        snap.ForEachElement(snapVisited.Add, TraverseRange.All);
        return snapVisited.Count == 2 && snapVisited[0] == e1 && snapVisited[1] == e2;
    }

    //TestSnapshotEmptyState 验证空状态快照遍历得 0 元素不抛
    //覆盖 Render 线程首帧 Tick 未添加元素时读快照不崩
    private static bool TestSnapshotEmptyState()
    {
        var state = new GuiRenderState();
        var snap = state.Snapshot();
        var visited = new List<GuiElementRenderState>();
        snap.ForEachElement(visited.Add, TraverseRange.All);
        return visited.Count == 0;
    }

    //TestSnapshotMultiLevelUpChain 验证 3 层嵌套相交元素自动 Up 的快照遍历顺序
    //e1 大被 e2 包含相交 e2 被 e3 包含相交深度优先访问 e1→e2→e3
    private static bool TestSnapshotMultiLevelUpChain()
    {
        var pipeline = MakePipeline();
        var state = new GuiRenderState();
        var e1 = MakeBlit(pipeline, 0, 0, 200, 200);
        var e2 = MakeBlit(pipeline, 50, 50, 150, 150);
        var e3 = MakeBlit(pipeline, 100, 100, 80, 80);
        state.AddGuiElement(e1);
        state.AddGuiElement(e2);
        state.AddGuiElement(e3);
        var snap = state.Snapshot();

        var visited = new List<GuiElementRenderState>();
        snap.ForEachElement(visited.Add, TraverseRange.All);
        return visited.Count == 3 && visited[0] == e1 && visited[1] == e2 && visited[2] == e3;
    }

    //TestSnapshotPreservesBlurSplit 验证快照保留 _firstStratumAfterBlur 标记
    //BlurBeforeThisStratum 后快照 BeforeBlur/AfterBlur 遍历应正确分割
    private static bool TestSnapshotPreservesBlurSplit()
    {
        var pipeline = MakePipeline();
        var state = new GuiRenderState();
        var before = MakeBlit(pipeline, 0, 0, 10, 10);
        state.AddGuiElement(before);
        state.NextStratum();
        state.BlurBeforeThisStratum();
        var after = MakeBlit(pipeline, 0, 0, 10, 10);
        state.AddGuiElement(after);
        var snap = state.Snapshot();

        var beforeList = new List<GuiElementRenderState>();
        snap.ForEachElement(beforeList.Add, TraverseRange.BeforeBlur);
        var afterList = new List<GuiElementRenderState>();
        snap.ForEachElement(afterList.Add, TraverseRange.AfterBlur);
        return beforeList.Count == 1 && beforeList[0] == before
            && afterList.Count == 1 && afterList[0] == after;
    }

    private static BlitRenderState MakeBlit(RenderPipeline pipeline, int x, int y, int w, int h)
        => new(pipeline, TextureSetup.NoTexture, Matrix3x2.Identity,
            x, y, x + w, y + h, 0f, 1f, 0f, 1f, unchecked((int)0xFFFFFFFF), FullScreen());
}
