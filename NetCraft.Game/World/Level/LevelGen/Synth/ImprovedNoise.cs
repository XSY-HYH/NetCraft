using NetCraft.Util;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Synth;

//ImprovedNoise 改进版柏林噪声对应原版 net.minecraft.world.level.levelgen.synth.ImprovedNoise
//经典 Perlin 噪声实现byte[256] 排列数组+8 角点三线性插值
//GRADIENT 梯度表与 SimplexNoise 共享放此类内
public sealed class ImprovedNoise
{
    private const float ShiftUpEpsilon = 1.0E-7f;

    //GRADIENT 16 个 3D 梯度向量对齐原版 SimplexNoise.GRADIENT
    //ImprovedNoise.gradDot 与 SimplexNoise 共用此表
    internal static readonly int[][] GRADIENT =
    {
        new[] { 1, 1, 0 }, new[] { -1, 1, 0 }, new[] { 1, -1, 0 }, new[] { -1, -1, 0 },
        new[] { 1, 0, 1 }, new[] { -1, 0, 1 }, new[] { 1, 0, -1 }, new[] { -1, 0, -1 },
        new[] { 0, 1, 1 }, new[] { 0, -1, 1 }, new[] { 0, 1, -1 }, new[] { 0, -1, -1 },
        new[] { 1, 1, 0 }, new[] { 0, -1, 1 }, new[] { -1, 1, 0 }, new[] { 0, -1, -1 }
    };

    private readonly byte[] _p = new byte[256];
    public double Xo { get; }
    public double Yo { get; }
    public double Zo { get; }

    public ImprovedNoise(RandomSource random)
    {
        Xo = random.NextDouble() * 256;
        Yo = random.NextDouble() * 256;
        Zo = random.NextDouble() * 256;
        for (var i = 0; i < 256; i++)
            _p[i] = (byte)i;
        for (var i = 0; i < 256; i++)
        {
            var offset = random.NextInt(256 - i);
            var swap = _p[i];
            _p[i] = _p[i + offset];
            _p[i + offset] = swap;
        }
    }

    public double Noise(double x, double y, double z)
    {
        var dx = x + Xo;
        var dy = y + Yo;
        var dz = z + Zo;
        var xi = Mth.Floor(dx);
        var yi = Mth.Floor(dy);
        var zi = Mth.Floor(dz);
        var xf = dx - xi;
        var yf = dy - yi;
        var zf = dz - zi;
        var u = Mth.Smoothstep(xf);
        var v = Mth.Smoothstep(yf);
        var w = Mth.Smoothstep(zf);
        return SampleAndLerp(xi, yi, zi, xf, yf, zf, u, v, w);
    }

    //2D 重载 z=0 方便调用
    public double Noise(double x, double y) => Noise(x, y, 0);

    //5 参数 Noise 对应原版 noise(x,y,z,yScale,yFudge)
    //BlendedNoise 用 yScale/yFudge 在 y 方向做阶梯偏移使低倍频噪声在同一 y 区间内重复
    public double Noise(double x, double y, double z, double yScale, double yFudge)
    {
        var dx = x + Xo;
        var dy = y + Yo;
        var dz = z + Zo;
        var xi = Mth.Floor(dx);
        var yi = Mth.Floor(dy);
        var zi = Mth.Floor(dz);
        var xr = dx - xi;
        var yr = dy - yi;
        var zr = dz - zi;
        double yrFudge;
        if (yScale != 0.0)
        {
            //fudgeLimit 取 yFudge 与 yr 较小者防止 yrFudge 超出当前 y 格子
            var fudgeLimit = yFudge >= 0.0 && yFudge < yr ? yFudge : yr;
            yrFudge = Mth.Floor(fudgeLimit / yScale + 1.0000000116860974E-7) * yScale;
        }
        else
        {
            yrFudge = 0.0;
        }
        return SampleAndLerpFudge(xi, yi, zi, xr, yr - yrFudge, zr, yr);
    }

    //SampleAndLerpFudge 7 参数版本对应原版 sampleAndLerp(yrOriginal)
    //yrOriginal 用于 yAlpha 平滑插值yr 已减去 yrFudge 用于梯度点积
    private double SampleAndLerpFudge(int xi, int yi, int zi, double xr, double yr, double zr, double yrOriginal)
    {
        var n000 = GradDot(P(xi, yi, zi), xr, yr, zr);
        var n100 = GradDot(P(xi + 1, yi, zi), xr - 1, yr, zr);
        var n010 = GradDot(P(xi, yi + 1, zi), xr, yr - 1, zr);
        var n110 = GradDot(P(xi + 1, yi + 1, zi), xr - 1, yr - 1, zr);
        var n001 = GradDot(P(xi, yi, zi + 1), xr, yr, zr - 1);
        var n101 = GradDot(P(xi + 1, yi, zi + 1), xr - 1, yr, zr - 1);
        var n011 = GradDot(P(xi, yi + 1, zi + 1), xr, yr - 1, zr - 1);
        var n111 = GradDot(P(xi + 1, yi + 1, zi + 1), xr - 1, yr - 1, zr - 1);
        var xAlpha = Mth.Smoothstep(xr);
        var yAlpha = Mth.Smoothstep(yrOriginal);
        var zAlpha = Mth.Smoothstep(zr);
        return Mth.Lerp3(xAlpha, yAlpha, zAlpha, n000, n100, n010, n110, n001, n101, n011, n111);
    }

    private double SampleAndLerp(int xi, int yi, int zi, double xf, double yf, double zf, double u, double v, double w)
    {
        var n000 = GradDot(P(xi, yi, zi), xf, yf, zf);
        var n100 = GradDot(P(xi + 1, yi, zi), xf - 1, yf, zf);
        var n010 = GradDot(P(xi, yi + 1, zi), xf, yf - 1, zf);
        var n110 = GradDot(P(xi + 1, yi + 1, zi), xf - 1, yf - 1, zf);
        var n001 = GradDot(P(xi, yi, zi + 1), xf, yf, zf - 1);
        var n101 = GradDot(P(xi + 1, yi, zi + 1), xf - 1, yf, zf - 1);
        var n011 = GradDot(P(xi, yi + 1, zi + 1), xf, yf - 1, zf - 1);
        var n111 = GradDot(P(xi + 1, yi + 1, zi + 1), xf - 1, yf - 1, zf - 1);
        var nx00 = Mth.Lerp(u, n000, n100);
        var nx10 = Mth.Lerp(u, n010, n110);
        var nx01 = Mth.Lerp(u, n001, n101);
        var nx11 = Mth.Lerp(u, n011, n111);
        var nxy0 = Mth.Lerp(v, nx00, nx10);
        var nxy1 = Mth.Lerp(v, nx01, nx11);
        return Mth.Lerp(w, nxy0, nxy1);
    }

    private static double GradDot(int hash, double x, double y, double z)
    {
        var g = GRADIENT[hash & 15];
        return g[0] * x + g[1] * y + g[2] * z;
    }

    private int P(int x, int y, int z)
        => _p[(_p[(_p[x & 255] + y) & 255] + z) & 255] & 255;
}
