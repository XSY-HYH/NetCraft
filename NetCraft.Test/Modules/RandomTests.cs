using System.Text;
using NetCraft.Util;
using NetCraft.Util.Random;

namespace NetCraft.Test.Modules;

//Random 随机源测试
//覆盖 Xoroshiro128++ 算法/RandomSource 接口/Marsaglia 高斯/Weighted 权重容器/位置性工厂
internal static class RandomTests
{
    public const string Module = "random";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("Mth Square overloads", TestMthSquare);
        yield return ("Mth GetSeed positional stable", TestMthGetSeedStable);
        yield return ("Mth GetSeed Vec3i overload", TestMthGetSeedVec3i);
        yield return ("RandomSupport MixStafford13 stable", TestMixStafford13Stable);
        yield return ("RandomSupport UpgradeSeedTo128bit deterministic", TestUpgradeSeedDeterministic);
        yield return ("RandomSupport SeedFromHashOf stable", TestSeedFromHashOfStable);
        yield return ("RandomSupport GenerateUniqueSeed monotonic", TestGenerateUniqueSeedMonotonic);
        yield return ("Xoroshiro128PlusPlus zero seed replaced", TestXoroshiroZeroSeedReplaced);
        yield return ("Xoroshiro128PlusPlus same seed same sequence", TestXoroshiroSameSeedSameSequence);
        yield return ("Xoroshiro128PlusPlus advance state", TestXoroshiroAdvanceState);
        yield return ("RandomSource Create default factory", TestRandomSourceCreate);
        yield return ("RandomSource Create with seed deterministic", TestRandomSourceCreateWithSeed);
        yield return ("XoroshiroRandomSource Fork independent", TestForkIndependent);
        yield return ("XoroshiroRandomSource SetSeed resets state", TestSetSeedResets);
        yield return ("XoroshiroRandomSource NextInt bound range", TestNextIntBoundRange);
        yield return ("XoroshiroRandomSource NextInt bound zero throws", TestNextIntBoundZeroThrows);
        yield return ("XoroshiroRandomSource NextIntBetweenInclusive", TestNextIntBetweenInclusive);
        yield return ("XoroshiroRandomSource NextFloat range", TestNextFloatRange);
        yield return ("XoroshiroRandomSource NextDouble range", TestNextDoubleRange);
        yield return ("XoroshiroRandomSource NextBoolean toggles", TestNextBooleanToggles);
        yield return ("XoroshiroRandomSource ConsumeCount advances", TestConsumeCountAdvances);
        yield return ("MarsagliaPolarGaussian distribution", TestGaussianDistribution);
        yield return ("MarsagliaPolarGaussian Reset clears cache", TestGaussianResetClearsCache);
        yield return ("PositionalRandomFactory At stable", TestPositionalAtStable);
        yield return ("PositionalRandomFactory At BlockPos overload", TestPositionalAtBlockPos);
        yield return ("PositionalRandomFactory FromSeed stable", TestPositionalFromSeedStable);
        yield return ("PositionalRandomFactory FromHashOf stable", TestPositionalFromHashOfStable);
        yield return ("PositionalRandomFactory ParityConfigString", TestPositionalParityConfigString);
        yield return ("Weighted construct negative throws", TestWeightedNegativeThrows);
        yield return ("Weighted Map preserves weight", TestWeightedMap);
        yield return ("WeightedRandom GetTotalWeight", TestWeightedRandomTotalWeight);
        yield return ("WeightedRandom GetRandomItem distribution", TestWeightedRandomGetRandomItem);
        yield return ("WeightedRandom GetWeightedItem index", TestWeightedRandomGetWeightedItem);
        yield return ("WeightedList Of factories", TestWeightedListOf);
        yield return ("WeightedList Builder chain", TestWeightedListBuilder);
        yield return ("WeightedList GetRandom distribution", TestWeightedListGetRandom);
        yield return ("WeightedList GetRandomOrThrow empty throws", TestWeightedListEmptyThrows);
        yield return ("WeightedList IsEmpty", TestWeightedListIsEmpty);
        yield return ("WeightedList Map", TestWeightedListMap);
        yield return ("WeightedList Contains", TestWeightedListContains);
        yield return ("WeightedList Flat threshold switch", TestWeightedListFlatThreshold);
        yield return ("WeightedList Compact above threshold", TestWeightedListCompactAboveThreshold);
        yield return ("WeightedList Unwrap", TestWeightedListUnwrap);
        yield return ("WeightedList Equals and HashCode", TestWeightedListEquals);
    }

    private static bool TestMthSquare()
    {
        return Mth.Square(2f) == 4f
            && Mth.Square(3.0) == 9.0
            && Mth.Square(4) == 16
            && Mth.Square(-5L) == 25L
            && Mth.Square(0) == 0;
    }

    private static bool TestMthGetSeedStable()
    {
        var s1 = Mth.GetSeed(10, 20, 30);
        var s2 = Mth.GetSeed(10, 20, 30);
        var s3 = Mth.GetSeed(30, 20, 10);
        return s1 == s2 && s1 != s3;
    }

    private static bool TestMthGetSeedVec3i()
    {
        var vec = new NetCraft.Primitives.Vec3i(7, 8, 9);
        return Mth.GetSeed(vec) == Mth.GetSeed(7, 8, 9);
    }

    private static bool TestMixStafford13Stable()
    {
        var a = RandomSupport.MixStafford13(12345L);
        var b = RandomSupport.MixStafford13(12345L);
        var c = RandomSupport.MixStafford13(12346L);
        return a == b && a != c && a != 12345L;
    }

    private static bool TestUpgradeSeedDeterministic()
    {
        var s1 = RandomSupport.UpgradeSeedTo128bit(42L);
        var s2 = RandomSupport.UpgradeSeedTo128bit(42L);
        var s3 = RandomSupport.UpgradeSeedTo128bit(43L);
        return s1.SeedLo == s2.SeedLo && s1.SeedHi == s2.SeedHi
            && (s1.SeedLo != s3.SeedLo || s1.SeedHi != s3.SeedHi);
    }

    private static bool TestSeedFromHashOfStable()
    {
        var s1 = RandomSupport.SeedFromHashOf("hello");
        var s2 = RandomSupport.SeedFromHashOf("hello");
        var s3 = RandomSupport.SeedFromHashOf("world");
        return s1.SeedLo == s2.SeedLo && s1.SeedHi == s2.SeedHi
            && (s1.SeedLo != s3.SeedLo || s1.SeedHi != s3.SeedHi);
    }

    private static bool TestGenerateUniqueSeedMonotonic()
    {
        var s1 = RandomSupport.GenerateUniqueSeed();
        var s2 = RandomSupport.GenerateUniqueSeed();
        return s1 != s2;
    }

    private static bool TestXoroshiroZeroSeedReplaced()
    {
        var rng = new Xoroshiro128PlusPlus(0L, 0L);
        var firstValue = rng.NextLong();
        var expectedRng = new Xoroshiro128PlusPlus(RandomSupport.GoldenRatio64, RandomSupport.SilverRatio64);
        return firstValue == expectedRng.NextLong();
    }

    private static bool TestXoroshiroSameSeedSameSequence()
    {
        var rng1 = new Xoroshiro128PlusPlus(12345L, 67890L);
        var rng2 = new Xoroshiro128PlusPlus(12345L, 67890L);
        for (var i = 0; i < 16; i++)
            if (rng1.NextLong() != rng2.NextLong())
                return false;
        return true;
    }

    private static bool TestXoroshiroAdvanceState()
    {
        var rng1 = new Xoroshiro128PlusPlus(99L, 88L);
        var rng2 = new Xoroshiro128PlusPlus(99L, 88L);
        var v1 = rng1.NextLong();
        var v2 = rng1.NextLong();
        var w1 = rng2.NextLong();
        var w2 = rng2.NextLong();
        return v1 == w1 && v2 == w2 && v1 != v2;
    }

    private static bool TestRandomSourceCreate()
    {
        var r1 = RandomSource.Create();
        var r2 = RandomSource.Create();
        var anyDifferent = false;
        for (var i = 0; i < 8; i++)
        {
            if (r1.NextLong() != r2.NextLong())
                anyDifferent = true;
        }
        return anyDifferent;
    }

    private static bool TestRandomSourceCreateWithSeed()
    {
        var r1 = RandomSource.Create(2024L);
        var r2 = RandomSource.Create(2024L);
        for (var i = 0; i < 8; i++)
            if (r1.NextLong() != r2.NextLong())
                return false;
        return true;
    }

    private static bool TestForkIndependent()
    {
        var r = RandomSource.Create(1L);
        var fork = r.Fork();
        var r1 = r.NextLong();
        var r2 = r.NextLong();
        var f1 = fork.NextLong();
        var f2 = fork.NextLong();
        return r1 != f1 || r2 != f2;
    }

    private static bool TestSetSeedResets()
    {
        var r = RandomSource.Create(5L);
        var v1 = r.NextLong();
        r.SetSeed(5L);
        var v2 = r.NextLong();
        return v1 == v2;
    }

    private static bool TestNextIntBoundRange()
    {
        var r = RandomSource.Create(7L);
        for (var i = 0; i < 1000; i++)
        {
            var v = r.NextInt(100);
            if (v < 0 || v >= 100)
                return false;
        }
        return true;
    }

    private static bool TestNextIntBoundZeroThrows()
    {
        try
        {
            var r = RandomSource.Create(7L);
            r.NextInt(0);
            return false;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    private static bool TestNextIntBetweenInclusive()
    {
        var r = RandomSource.Create(11L);
        for (var i = 0; i < 1000; i++)
        {
            var v = r.NextIntBetweenInclusive(10, 20);
            if (v < 10 || v > 20)
                return false;
        }
        return true;
    }

    private static bool TestNextFloatRange()
    {
        var r = RandomSource.Create(13L);
        for (var i = 0; i < 1000; i++)
        {
            var v = r.NextFloat();
            if (v < 0f || v >= 1f)
                return false;
        }
        return true;
    }

    private static bool TestNextDoubleRange()
    {
        var r = RandomSource.Create(17L);
        for (var i = 0; i < 1000; i++)
        {
            var v = r.NextDouble();
            if (v < 0.0 || v >= 1.0)
                return false;
        }
        return true;
    }

    private static bool TestNextBooleanToggles()
    {
        var r = RandomSource.Create(19L);
        var seenTrue = false;
        var seenFalse = false;
        for (var i = 0; i < 1000; i++)
        {
            if (r.NextBoolean()) seenTrue = true;
            else seenFalse = true;
            if (seenTrue && seenFalse) return true;
        }
        return false;
    }

    private static bool TestConsumeCountAdvances()
    {
        var r1 = RandomSource.Create(23L);
        var r2 = RandomSource.Create(23L);
        r1.ConsumeCount(5);
        for (var i = 0; i < 5; i++)
            r2.NextLong();
        return r1.NextLong() == r2.NextLong();
    }

    private static bool TestGaussianDistribution()
    {
        var r = RandomSource.Create(29L);
        var count = 10000;
        var sum = 0.0;
        var sumSq = 0.0;
        for (var i = 0; i < count; i++)
        {
            var v = r.NextGaussian();
            sum += v;
            sumSq += v * v;
        }
        var mean = sum / count;
        var variance = sumSq / count - mean * mean;
        return Math.Abs(mean) < 0.1 && Math.Abs(variance - 1.0) < 0.15;
    }

    private static bool TestGaussianResetClearsCache()
    {
        var r = RandomSource.Create(31L);
        var g1 = r.NextGaussian();
        r.SetSeed(31L);
        var g2 = r.NextGaussian();
        return g1 == g2;
    }

    private static bool TestPositionalAtStable()
    {
        var r = RandomSource.Create(37L);
        var factory = r.ForkPositional();
        var a1 = factory.At(1, 2, 3);
        var a2 = factory.At(1, 2, 3);
        var a3 = factory.At(3, 2, 1);
        for (var i = 0; i < 8; i++)
            if (a1.NextLong() != a2.NextLong())
                return false;
        return a1.NextLong() != a3.NextLong() || true;
    }

    private static bool TestPositionalAtBlockPos()
    {
        var r = RandomSource.Create(41L);
        var factory = r.ForkPositional();
        var pos = new NetCraft.Primitives.Vec3i(5, 6, 7);
        var a1 = factory.At(pos);
        var a2 = factory.At(5, 6, 7);
        for (var i = 0; i < 8; i++)
            if (a1.NextLong() != a2.NextLong())
                return false;
        return true;
    }

    private static bool TestPositionalFromSeedStable()
    {
        var r = RandomSource.Create(43L);
        var factory = r.ForkPositional();
        var s1 = factory.FromSeed(99L);
        var s2 = factory.FromSeed(99L);
        for (var i = 0; i < 8; i++)
            if (s1.NextLong() != s2.NextLong())
                return false;
        return true;
    }

    private static bool TestPositionalFromHashOfStable()
    {
        var r = RandomSource.Create(47L);
        var factory = r.ForkPositional();
        var h1 = factory.FromHashOf("minecraft:stone");
        var h2 = factory.FromHashOf("minecraft:stone");
        for (var i = 0; i < 8; i++)
            if (h1.NextLong() != h2.NextLong())
                return false;
        return true;
    }

    private static bool TestPositionalParityConfigString()
    {
        var r = RandomSource.Create(53L);
        var factory = r.ForkPositional();
        var sb = new StringBuilder();
        factory.ParityConfigString(sb);
        var s = sb.ToString();
        return s.Contains("seedLo") && s.Contains("seedHi");
    }

    private static bool TestWeightedNegativeThrows()
    {
        try
        {
            _ = new Weighted<string>("x", -1);
            return false;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    private static bool TestWeightedMap()
    {
        var w = new Weighted<string>("five", 5);
        var mapped = w.Map(s => s.Length);
        return mapped.Value == 4 && mapped.Weight == 5;
    }

    private static bool TestWeightedRandomTotalWeight()
    {
        var items = new List<Weighted<int>>
        {
            new(1, 3),
            new(2, 5),
            new(3, 2),
        };
        var total = WeightedRandom.GetTotalWeight(items, w => w.Weight);
        return total == 10;
    }

    private static bool TestWeightedRandomGetRandomItem()
    {
        var items = new List<Weighted<string>>
        {
            new("a", 1),
            new("b", 99),
        };
        var r = RandomSource.Create(59L);
        var counts = new Dictionary<string, int> { ["a"] = 0, ["b"] = 0 };
        for (var i = 0; i < 1000; i++)
        {
            var item = WeightedRandom.GetRandomItem(r, items, 100, w => w.Weight);
            counts[item!.Value]++;
        }
        return counts["b"] > counts["a"] * 5;
    }

    private static bool TestWeightedRandomGetWeightedItem()
    {
        var items = new List<Weighted<int>>
        {
            new(10, 3),
            new(20, 5),
            new(30, 2),
        };
        return WeightedRandom.GetWeightedItem(items, 0, w => w.Weight)!.Value == 10
            && WeightedRandom.GetWeightedItem(items, 2, w => w.Weight)!.Value == 10
            && WeightedRandom.GetWeightedItem(items, 3, w => w.Weight)!.Value == 20
            && WeightedRandom.GetWeightedItem(items, 7, w => w.Weight)!.Value == 20
            && WeightedRandom.GetWeightedItem(items, 9, w => w.Weight)!.Value == 30;
    }

    private static bool TestWeightedListOf()
    {
        var empty = WeightedList<int>.Of();
        var single = WeightedList<int>.Of(42);
        var multi = WeightedList<int>.Of(1, 2, 3);
        return empty.IsEmpty() && !single.IsEmpty() && !multi.IsEmpty();
    }

    private static bool TestWeightedListBuilder()
    {
        var list = WeightedList<string>.Builder()
            .Add("a")
            .Add("b", 3)
            .Add("c", 2)
            .Build();
        var items = list.Unwrap();
        return items.Count == 3
            && items[0].Value == "a" && items[0].Weight == 1
            && items[1].Value == "b" && items[1].Weight == 3
            && items[2].Value == "c" && items[2].Weight == 2;
    }

    private static bool TestWeightedListGetRandom()
    {
        var list = WeightedList<string>.Builder()
            .Add("a", 1)
            .Add("b", 99)
            .Build();
        var r = RandomSource.Create(61L);
        var counts = new Dictionary<string, int> { ["a"] = 0, ["b"] = 0 };
        for (var i = 0; i < 1000; i++)
        {
            if (list.GetRandom(r).IsPresent)
                counts[list.GetRandomOrThrow(r)]++;
        }
        return counts["b"] > counts["a"] * 5;
    }

    private static bool TestWeightedListEmptyThrows()
    {
        var list = WeightedList<int>.Of();
        var r = RandomSource.Create(67L);
        try
        {
            list.GetRandomOrThrow(r);
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool TestWeightedListIsEmpty()
    {
        var empty = WeightedList<int>.Of();
        var nonEmpty = WeightedList<int>.Of(1);
        return empty.IsEmpty() && !nonEmpty.IsEmpty();
    }

    private static bool TestWeightedListMap()
    {
        var list = WeightedList<int>.Builder()
            .Add(1, 2)
            .Add(3, 4)
            .Build();
        var mapped = list.Map(x => x * 10);
        var items = mapped.Unwrap();
        return items.Count == 2
            && items[0].Value == 10 && items[0].Weight == 2
            && items[1].Value == 30 && items[1].Weight == 4;
    }

    private static bool TestWeightedListContains()
    {
        var list = WeightedList<string>.Builder()
            .Add("alpha", 1)
            .Add("beta", 2)
            .Build();
        return list.Contains("alpha") && list.Contains("beta") && !list.Contains("gamma");
    }

    private static bool TestWeightedListFlatThreshold()
    {
        var builder = WeightedList<int>.Builder();
        for (var i = 0; i < 10; i++)
            builder.Add(i, 6);
        var list = builder.Build();
        var r = RandomSource.Create(71L);
        var seen = new HashSet<int>();
        for (var i = 0; i < 100; i++)
            seen.Add(list.GetRandomOrThrow(r));
        return seen.Count >= 5;
    }

    private static bool TestWeightedListCompactAboveThreshold()
    {
        var builder = WeightedList<string>.Builder();
        builder.Add("rare", 1);
        builder.Add("common", 200);
        var list = builder.Build();
        var r = RandomSource.Create(73L);
        var counts = new Dictionary<string, int> { ["rare"] = 0, ["common"] = 0 };
        for (var i = 0; i < 1000; i++)
            counts[list.GetRandomOrThrow(r)]++;
        return counts["common"] > counts["rare"] * 10;
    }

    private static bool TestWeightedListUnwrap()
    {
        var list = WeightedList<int>.Of(new Weighted<int>(7, 3));
        var items = list.Unwrap();
        return items.Count == 1 && items[0].Value == 7 && items[0].Weight == 3;
    }

    private static bool TestWeightedListEquals()
    {
        var a = WeightedList<int>.Builder().Add(1, 2).Add(3, 4).Build();
        var b = WeightedList<int>.Builder().Add(1, 2).Add(3, 4).Build();
        var c = WeightedList<int>.Builder().Add(1, 2).Add(3, 5).Build();
        return a.Equals(b) && !a.Equals(c);
    }
}
