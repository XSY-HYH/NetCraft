using System.Numerics;
using NetCraft.Game.Client.Render;
using NetCraft.Game.Client.Render.Culling;
using NetCraft.Primitives;

namespace NetCraft.Test.Modules;

//CameraFrustumTests 相机+视锥剔除单元测试
//验证 Camera 的 Forward/Up/Left 向量 view 矩阵 投影矩阵（Vulkan Y 矫正）
//验证 Frustum 的 6 平面提取 near/far/left/right 剔除正确性
//相机场景：Position=(0,0,5) 朝 -Z near=0.1 far=1000 fov=PI/4
internal static class CameraFrustumTests
{
    public const string Module = "camerafrustum";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("Camera Forward default is -Z", TestCameraForwardDefaultNegativeZ);
        yield return ("Camera Forward yaw90 turns to +X", TestCameraForwardYaw90);
        yield return ("Camera Up default is +Y", TestCameraUpDefault);
        yield return ("Camera ViewMatrix transforms world origin to camera space", TestCameraViewMatrix);
        yield return ("Camera Projection flips Y for Vulkan (M22<0)", TestCameraProjectionVulkanYFlip);
        yield return ("Frustum point in front of camera is visible", TestFrustumPointInFrontVisible);
        yield return ("Frustum point behind camera is invisible", TestFrustumPointBehindInvisible);
        yield return ("Frustum point beyond far plane is invisible", TestFrustumPointBeyondFarInvisible);
        yield return ("Frustum AABB in front is visible", TestFrustumAABBInFrontVisible);
        yield return ("Frustum AABB behind camera is invisible", TestFrustumAABBBehindInvisible);
    }

    //yRot=0 xRot=0 时 Forward = (0,0,-1)
    private static bool TestCameraForwardDefaultNegativeZ()
    {
        var cam = NewCamera();
        return VecEqual(cam.Forward, new Vector3(0, 0, -1));
    }

    //yRot=90 度向左转 Forward 从 -Z 变 +X
    private static bool TestCameraForwardYaw90()
    {
        var cam = NewCamera();
        cam.SetRotation(90, 0);
        return VecEqual(cam.Forward, new Vector3(1, 0, 0));
    }

    //yRot=0 xRot=0 时 Up = (0,1,0)
    private static bool TestCameraUpDefault()
    {
        var cam = NewCamera();
        return VecEqual(cam.Up, new Vector3(0, 1, 0));
    }

    //Camera(0,0,5) 朝 -Z view 矩阵把世界原点变换到 (0,0,-5)
    private static bool TestCameraViewMatrix()
    {
        var cam = NewCamera();
        var view = cam.GetViewMatrix();
        var origin = Vector3.Transform(Vector3.Zero, view);
        return VecEqual(origin, new Vector3(0, 0, -5));
    }

    //Vulkan Y 矫正后 M22 为负（CreatePerspectiveFieldOfView 原 M22=1/tan(fov/2)>0 翻转后<0）
    private static bool TestCameraProjectionVulkanYFlip()
    {
        var cam = NewCamera();
        var proj = cam.GetProjectionMatrix();
        return proj.M22 < 0;
    }

    //点 (0,0,0) 在相机 (0,0,5) 前方 5 单位 near=0.1 far=1000 可见
    private static bool TestFrustumPointInFrontVisible()
    {
        var f = NewFrustum();
        return f.IsPointVisible(0, 0, 0);
    }

    //点 (0,0,10) 在相机后方 5 单位 不可见
    private static bool TestFrustumPointBehindInvisible()
    {
        var f = NewFrustum();
        return !f.IsPointVisible(0, 0, 10);
    }

    //点 (0,0,-1001) 距相机 1006 单位 超出 far=1000 不可见
    private static bool TestFrustumPointBeyondFarInvisible()
    {
        var f = NewFrustum();
        return !f.IsPointVisible(0, 0, -1001);
    }

    //AABB 在 (0,0,0) 附近 前方 可见
    private static bool TestFrustumAABBInFrontVisible()
    {
        var f = NewFrustum();
        var box = new AABB(-1, -1, -1, 1, 1, 1);
        return f.IsVisible(box);
    }

    //AABB 在 (0,0,10) 附近 后方 不可见
    private static bool TestFrustumAABBBehindInvisible()
    {
        var f = NewFrustum();
        var box = new AABB(9, -1, 9, 11, 1, 11);
        return !f.IsVisible(box);
    }

    //NewCamera 标准测试相机 Position=(0,0,5) 朝 -Z near=0.1 far=1000
    private static Camera NewCamera()
    {
        var cam = new Camera();
        cam.SetPosition(new Vector3(0, 0, 5));
        cam.SetRotation(0, 0);
        cam.UpdatePerspective(MathF.PI / 4f, 800, 600, 0.1f, 1000f);
        return cam;
    }

    //NewFrustum 用标准相机的 viewProj 构造视锥剔除器
    private static Frustum NewFrustum()
    {
        var cam = NewCamera();
        return new Frustum(cam.GetViewProjMatrix());
    }

    //VecEqual 浮点向量比较 1e-5 容差
    private static bool VecEqual(Vector3 a, Vector3 b)
        => Math.Abs(a.X - b.X) < 1e-5f
            && Math.Abs(a.Y - b.Y) < 1e-5f
            && Math.Abs(a.Z - b.Z) < 1e-5f;
}
