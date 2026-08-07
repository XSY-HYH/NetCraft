using NetCraft.Codec;
using NetCraft.Util.Collection;

namespace NetCraft.Test.Modules;

//Collection 集合工具测试
//覆盖 CollectionUtil/RandomCollections/IndexLookup/Memoize/SingleKeyCache/PredicateUtil/EnumCollections/TaskSequence/FixedSizeUtil/OptionalUtil
internal static class CollectionTests
{
    public const string Module = "collection";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("CollectionUtil Make factory", TestMakeFactory);
        yield return ("CollectionUtil Make configurator", TestMakeConfigurator);
        yield return ("CollectionUtil FindNextInIterable", TestFindNextInIterable);
        yield return ("CollectionUtil FindNext wraps around", TestFindNextWrap);
        yield return ("CollectionUtil FindPreviousInIterable", TestFindPreviousInIterable);
        yield return ("CollectionUtil FindPrevious wraps around", TestFindPreviousWrap);
        yield return ("CollectionUtil MapValues", TestMapValues);
        yield return ("CollectionUtil MapValuesLazy", TestMapValuesLazy);
        yield return ("CollectionUtil CopyAndAdd element", TestCopyAndAddElement);
        yield return ("CollectionUtil CopyAndAdd params", TestCopyAndAddParams);
        yield return ("CollectionUtil CopyAndAdd prepend", TestCopyAndAddPrepend);
        yield return ("CollectionUtil Join two", TestJoinTwo);
        yield return ("CollectionUtil Join params", TestJoinParams);
        yield return ("CollectionUtil CopyAndPut", TestCopyAndPut);
        yield return ("CollectionUtil IsSymmetrical true", TestIsSymmetricalTrue);
        yield return ("CollectionUtil IsSymmetrical false", TestIsSymmetricalFalse);
        yield return ("CollectionUtil IsSymmetrical width one", TestIsSymmetricalWidthOne);
        yield return ("CollectionUtil GrowByHalf", TestGrowByHalf);
        yield return ("CollectionUtil GrowByHalf minimal", TestGrowByHalfMinimal);
        yield return ("RandomCollections GetRandom list", TestGetRandomList);
        yield return ("RandomCollections GetRandom array", TestGetRandomArray);
        yield return ("RandomCollections GetRandom int array", TestGetRandomIntArray);
        yield return ("RandomCollections GetRandomSafe empty", TestGetRandomSafeEmpty);
        yield return ("RandomCollections GetRandomSafe present", TestGetRandomSafePresent);
        yield return ("RandomCollections Shuffle", TestShuffle);
        yield return ("RandomCollections ToShuffledList generic", TestToShuffledListGeneric);
        yield return ("RandomCollections ToShuffledList int", TestToShuffledListInt);
        yield return ("RandomCollections ShuffledCopy array", TestShuffledCopyArray);
        yield return ("RandomCollections ShuffledCopy list", TestShuffledCopyList);
        yield return ("IndexLookup CreateIndexLookup small", TestCreateIndexLookupSmall);
        yield return ("IndexLookup CreateIndexLookup large", TestCreateIndexLookupLarge);
        yield return ("IndexLookup CreateIndexLookup missing returns -1", TestCreateIndexLookupMissing);
        yield return ("IndexLookup CreateIndexIdentityLookup small", TestCreateIndexIdentityLookupSmall);
        yield return ("IndexLookup CreateIndexIdentityLookup large", TestCreateIndexIdentityLookupLarge);
        yield return ("Memoize Function caches", TestMemoizeFunctionCaches);
        yield return ("Memoize BiFunction caches", TestMemoizeBiFunctionCaches);
        yield return ("SingleKeyCache returns cached value", TestSingleKeyCacheHit);
        yield return ("SingleKeyCache recomputes on key change", TestSingleKeyCacheRecompute);
        yield return ("PredicateUtil AllOf empty", TestAllOfEmpty);
        yield return ("PredicateUtil AllOf and logic", TestAllOfAnd);
        yield return ("PredicateUtil AllOf params", TestAllOfParams);
        yield return ("PredicateUtil AnyOf empty", TestAnyOfEmpty);
        yield return ("PredicateUtil AnyOf or logic", TestAnyOfOr);
        yield return ("PredicateUtil AnyOf params", TestAnyOfParams);
        yield return ("EnumCollections MakeEnumMap", TestMakeEnumMap);
        yield return ("EnumCollections AllOfEnumExcept", TestAllOfEnumExcept);
        yield return ("TaskSequence Sequence empty", TestTaskSequenceEmpty);
        yield return ("TaskSequence Sequence values", TestTaskSequenceValues);
        yield return ("TaskSequence SequenceFailFast success", TestTaskSequenceFailFastSuccess);
        yield return ("FixedSizeUtil int stream exact", TestFixedSizeIntExact);
        yield return ("FixedSizeUtil int stream short", TestFixedSizeIntShort);
        yield return ("FixedSizeUtil int stream long", TestFixedSizeIntLong);
        yield return ("FixedSizeUtil list exact", TestFixedSizeListExact);
        yield return ("FixedSizeUtil list long partial", TestFixedSizeListLongPartial);
        yield return ("OptionalUtil IfElse present", TestIfElsePresent);
        yield return ("OptionalUtil IfElse empty", TestIfElseEmpty);
    }

    private static bool TestMakeFactory()
    {
        var result = CollectionUtil.Make(() => 42);
        return result == 42;
    }

    private static bool TestMakeConfigurator()
    {
        var list = CollectionUtil.Make(new List<int>(), l => { l.Add(1); l.Add(2); });
        return list.Count == 2 && list[0] == 1 && list[1] == 2;
    }

    private static bool TestFindNextInIterable()
    {
        var items = new List<string> { "a", "b", "c" };
        return CollectionUtil.FindNextInIterable(items, "b") == "c";
    }

    private static bool TestFindNextWrap()
    {
        var items = new List<string> { "a", "b", "c" };
        return CollectionUtil.FindNextInIterable(items, "c") == "a";
    }

    private static bool TestFindPreviousInIterable()
    {
        var items = new List<string> { "a", "b", "c" };
        return CollectionUtil.FindPreviousInIterable(items, "b") == "a";
    }

    private static bool TestFindPreviousWrap()
    {
        var items = new List<string> { "a", "b", "c" };
        return CollectionUtil.FindPreviousInIterable(items, "a") == "c";
    }

    private static bool TestMapValues()
    {
        var map = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
        var mapped = CollectionUtil.MapValues(map, v => v * 10);
        return mapped["a"] == 10 && mapped["b"] == 20;
    }

    private static bool TestMapValuesLazy()
    {
        var map = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
        var calls = 0;
        var lazy = CollectionUtil.MapValuesLazy(map, v =>
        {
            calls++;
            return v * 10;
        });
        var v1 = lazy["a"];
        var v2 = lazy["a"];
        return v1 == 10 && v2 == 10 && calls == 2;
    }

    private static bool TestCopyAndAddElement()
    {
        var list = new List<int> { 1, 2, 3 };
        var result = CollectionUtil.CopyAndAdd(list, 4);
        return result.Count == 4 && result[3] == 4 && list.Count == 3;
    }

    private static bool TestCopyAndAddParams()
    {
        var list = new List<int> { 1 };
        var result = CollectionUtil.CopyAndAdd(list, 2, 3, 4);
        return result.Count == 4 && result[0] == 1 && result[3] == 4;
    }

    private static bool TestCopyAndAddPrepend()
    {
        var list = new List<int> { 2, 3 };
        var result = CollectionUtil.CopyAndAdd(1, list);
        return result.Count == 3 && result[0] == 1 && result[1] == 2;
    }

    private static bool TestJoinTwo()
    {
        var a = new List<int> { 1, 2 };
        var b = new List<int> { 3, 4 };
        var result = CollectionUtil.Join(a, b);
        return result.Count == 4 && result[0] == 1 && result[3] == 4;
    }

    private static bool TestJoinParams()
    {
        var a = new List<int> { 1 };
        var b = new List<int> { 2 };
        var c = new List<int> { 3 };
        var result = CollectionUtil.Join(a, b, c);
        return result.Count == 3 && result[0] == 1 && result[2] == 3;
    }

    private static bool TestCopyAndPut()
    {
        var map = new Dictionary<string, int> { ["a"] = 1 };
        var result = CollectionUtil.CopyAndPut(map, "b", 2);
        return result["a"] == 1 && result["b"] == 2 && map.Count == 1;
    }

    private static bool TestIsSymmetricalTrue()
    {
        var items = new List<int> { 1, 2, 1, 4, 5, 4 };
        return CollectionUtil.IsSymmetrical(3, 2, items);
    }

    private static bool TestIsSymmetricalFalse()
    {
        var items = new List<int> { 1, 2, 3, 4, 5, 6 };
        return !CollectionUtil.IsSymmetrical(3, 2, items);
    }

    private static bool TestIsSymmetricalWidthOne()
    {
        var items = new List<int> { 1, 2, 3 };
        return CollectionUtil.IsSymmetrical(1, 3, items);
    }

    private static bool TestGrowByHalf()
    {
        return CollectionUtil.GrowByHalf(10, 5) == 15;
    }

    private static bool TestGrowByHalfMinimal()
    {
        return CollectionUtil.GrowByHalf(2, 10) == 10;
    }

    private static bool TestGetRandomList()
    {
        var r = NetCraft.Util.Random.RandomSource.Create(1L);
        var list = new List<int> { 10, 20, 30 };
        var v = RandomCollections.GetRandom(list, r);
        return list.Contains(v);
    }

    private static bool TestGetRandomArray()
    {
        var r = NetCraft.Util.Random.RandomSource.Create(2L);
        var array = new[] { 10, 20, 30 };
        var v = RandomCollections.GetRandom(array, r);
        return Array.IndexOf(array, v) >= 0;
    }

    private static bool TestGetRandomIntArray()
    {
        var r = NetCraft.Util.Random.RandomSource.Create(3L);
        var array = new[] { 10, 20, 30 };
        var v = RandomCollections.GetRandom(array, r);
        return Array.IndexOf(array, v) >= 0;
    }

    private static bool TestGetRandomSafeEmpty()
    {
        var r = NetCraft.Util.Random.RandomSource.Create(4L);
        var result = RandomCollections.GetRandomSafe(new List<int>(), r);
        return !result.IsPresent;
    }

    private static bool TestGetRandomSafePresent()
    {
        var r = NetCraft.Util.Random.RandomSource.Create(5L);
        var result = RandomCollections.GetRandomSafe(new List<int> { 42 }, r);
        return result.IsPresent && result.GetOrThrow() == 42;
    }

    private static bool TestShuffle()
    {
        var r = NetCraft.Util.Random.RandomSource.Create(6L);
        var list = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 };
        var before = list.ToList();
        RandomCollections.Shuffle(list, r);
        return list.Count == before.Count && !list.SequenceEqual(before);
    }

    private static bool TestToShuffledListGeneric()
    {
        var r = NetCraft.Util.Random.RandomSource.Create(7L);
        var source = Enumerable.Range(1, 100);
        var result = RandomCollections.ToShuffledList(source, r);
        return result.Count == 100
            && result.OrderBy(x => x).SequenceEqual(Enumerable.Range(1, 100))
            && !result.SequenceEqual(Enumerable.Range(1, 100));
    }

    private static bool TestToShuffledListInt()
    {
        var r = NetCraft.Util.Random.RandomSource.Create(8L);
        var source = Enumerable.Range(1, 50);
        var result = RandomCollections.ToShuffledIntArray(source, r);
        return result.Length == 50
            && result.OrderBy(x => x).SequenceEqual(Enumerable.Range(1, 50));
    }

    private static bool TestShuffledCopyArray()
    {
        var r = NetCraft.Util.Random.RandomSource.Create(9L);
        var array = new[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var result = RandomCollections.ShuffledCopy(array, r);
        return result.Count == array.Length
            && result.OrderBy(x => x).SequenceEqual(array)
            && !result.SequenceEqual(array);
    }

    private static bool TestShuffledCopyList()
    {
        var r = NetCraft.Util.Random.RandomSource.Create(10L);
        var list = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 };
        var result = RandomCollections.ShuffledCopy(list, r);
        return result.Count == list.Count
            && result.OrderBy(x => x).SequenceEqual(list)
            && !result.SequenceEqual(list);
    }

    private static bool TestCreateIndexLookupSmall()
    {
        var values = new List<string> { "a", "b", "c" };
        var lookup = IndexLookup.CreateIndexLookup<string>(values);
        return lookup("a") == 0 && lookup("b") == 1 && lookup("c") == 2;
    }

    private static bool TestCreateIndexLookupLarge()
    {
        var values = Enumerable.Range(0, 20).Select(i => i.ToString()).ToList();
        var lookup = IndexLookup.CreateIndexLookup<string>(values);
        return lookup("5") == 5 && lookup("19") == 19;
    }

    private static bool TestCreateIndexLookupMissing()
    {
        var values = new List<string> { "a", "b", "c" };
        var lookup = IndexLookup.CreateIndexLookup<string>(values);
        return lookup("z") == -1;
    }

    private static bool TestCreateIndexIdentityLookupSmall()
    {
        var a = new object();
        var b = new object();
        var values = new List<object> { a, b };
        var lookup = IndexLookup.CreateIndexIdentityLookup(values);
        return lookup(a) == 0 && lookup(b) == 1;
    }

    private static bool TestCreateIndexIdentityLookupLarge()
    {
        var objs = Enumerable.Range(0, 20).Select(_ => new object()).ToList();
        var lookup = IndexLookup.CreateIndexIdentityLookup(objs);
        return lookup(objs[5]) == 5 && lookup(new object()) == -1;
    }

    private static bool TestMemoizeFunctionCaches()
    {
        var calls = 0;
        Func<int, int> memoized = Memoize.MemoizeFunction<int, int>(x =>
        {
            calls++;
            return x * 2;
        });
        var v1 = memoized(5);
        var v2 = memoized(5);
        return v1 == 10 && v2 == 10 && calls == 1;
    }

    private static bool TestMemoizeBiFunctionCaches()
    {
        var calls = 0;
        Func<int, int, int> memoized = Memoize.MemoizeBiFunction<int, int, int>((a, b) =>
        {
            calls++;
            return a + b;
        });
        var v1 = memoized(2, 3);
        var v2 = memoized(2, 3);
        return v1 == 5 && v2 == 5 && calls == 1;
    }

    private static bool TestSingleKeyCacheHit()
    {
        var calls = 0;
        var cache = new SingleKeyCache<string, int>(key =>
        {
            calls++;
            return key.Length;
        });
        var v1 = cache.GetValue("hello");
        var v2 = cache.GetValue("hello");
        return v1 == 5 && v2 == 5 && calls == 1;
    }

    private static bool TestSingleKeyCacheRecompute()
    {
        var calls = 0;
        var cache = new SingleKeyCache<string, int>(key =>
        {
            calls++;
            return key.Length;
        });
        var v1 = cache.GetValue("hi");
        var v2 = cache.GetValue("hello");
        return v1 == 2 && v2 == 5 && calls == 2;
    }

    private static bool TestAllOfEmpty()
    {
        var pred = PredicateUtil.AllOf<int>();
        return pred(0) && pred(1) && pred(-1);
    }

    private static bool TestAllOfAnd()
    {
        var pred = PredicateUtil.AllOf<int>(x => x > 0, x => x < 10);
        return pred(5) && !pred(-1) && !pred(20);
    }

    private static bool TestAllOfParams()
    {
        var pred = PredicateUtil.AllOf<int>(x => x > 0, x => x < 10, x => x % 2 == 0);
        return pred(4) && !pred(3) && !pred(20);
    }

    private static bool TestAnyOfEmpty()
    {
        var pred = PredicateUtil.AnyOf<int>();
        return !pred(0) && !pred(1);
    }

    private static bool TestAnyOfOr()
    {
        var pred = PredicateUtil.AnyOf<int>(x => x < 0, x => x > 10);
        return pred(-1) && pred(20) && !pred(5);
    }

    private static bool TestAnyOfParams()
    {
        var pred = PredicateUtil.AnyOf<int>(x => x == 1, x => x == 2, x => x == 3);
        return pred(1) && pred(2) && pred(3) && !pred(4);
    }

    private enum TestColor { Red, Green, Blue }

    private static bool TestMakeEnumMap()
    {
        var map = EnumCollections.MakeEnumMap<TestColor, string>(c => c.ToString().ToUpperInvariant());
        return map[TestColor.Red] == "RED"
            && map[TestColor.Green] == "GREEN"
            && map[TestColor.Blue] == "BLUE"
            && map.Count == 3;
    }

    private static bool TestAllOfEnumExcept()
    {
        var set = EnumCollections.AllOfEnumExcept(TestColor.Green);
        return set.Count == 2 && set.Contains(TestColor.Red) && set.Contains(TestColor.Blue) && !set.Contains(TestColor.Green);
    }

    private static bool TestTaskSequenceEmpty()
    {
        var result = TaskSequence.Sequence<int>(new List<Task<int>>()).Result;
        return result.Count == 0;
    }

    private static bool TestTaskSequenceValues()
    {
        var tasks = new List<Task<int>> { Task.FromResult(1), Task.FromResult(2), Task.FromResult(3) };
        var result = TaskSequence.Sequence(tasks).Result;
        return result.Count == 3 && result[0] == 1 && result[1] == 2 && result[2] == 3;
    }

    private static bool TestTaskSequenceFailFastSuccess()
    {
        var tasks = new List<Task<int>> { Task.FromResult(10), Task.FromResult(20) };
        var result = TaskSequence.SequenceFailFast(tasks).Result;
        return result.Count == 2 && result[0] == 10 && result[1] == 20;
    }

    private static bool TestFixedSizeIntExact()
    {
        var stream = new[] { 1, 2, 3 }.AsEnumerable();
        var result = FixedSizeUtil.FixedSize(stream, 3);
        return result.GetOrThrow().Length == 3;
    }

    private static bool TestFixedSizeIntShort()
    {
        var stream = new[] { 1, 2 }.AsEnumerable();
        var result = FixedSizeUtil.FixedSize(stream, 3);
        return !result.Result().IsPresent;
    }

    private static bool TestFixedSizeIntLong()
    {
        var stream = new[] { 1, 2, 3, 4 }.AsEnumerable();
        var result = FixedSizeUtil.FixedSize(stream, 3);
        var partial = result.ResultOrPartial();
        return partial.IsPresent && partial.Get().Length == 3;
    }

    private static bool TestFixedSizeListExact()
    {
        var list = new List<string> { "a", "b" };
        var result = FixedSizeUtil.FixedSize(list, 2);
        return result.GetOrThrow().Count == 2;
    }

    private static bool TestFixedSizeListLongPartial()
    {
        var list = new List<string> { "a", "b", "c", "d" };
        var result = FixedSizeUtil.FixedSize(list, 2);
        var partial = result.ResultOrPartial();
        return partial.IsPresent && partial.Get().Count == 2;
    }

    private static bool TestIfElsePresent()
    {
        var received = 0;
        var otherCalled = false;
        var input = OptionalUtil.IfElse(Optional<int>.Of(42),
            v => received = v,
            () => otherCalled = true);
        return input.IsPresent && received == 42 && !otherCalled;
    }

    private static bool TestIfElseEmpty()
    {
        var received = 0;
        var otherCalled = false;
        var input = OptionalUtil.IfElse(Optional<int>.Empty(),
            v => received = v,
            () => otherCalled = true);
        return !input.IsPresent && received == 0 && otherCalled;
    }
}
