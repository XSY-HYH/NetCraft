using NetCraft.Codec;
using NetCraft.DataFixer;
using NetCraft.DataFixer.Functions;
using NetCraft.DataFixer.Fixes;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Schemas;
using NetCraft.DataFixer.Types.Templates;
using NetCraft.DataFixer.Util;
using NetCraft.Nbt;

namespace NetCraft.Test.Modules;

//DataFixer 子系统阶段 A 测试
//覆盖 util/kinds + Products 关键路径 IdF/Const/OptionalBox/ListBox/Either/Pair/Unit/Products
internal static class DataFixerTests
{
    public const string Module = "datafixer";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("Unit Instance equality", TestUnitEquality);
        yield return ("Either Left/Right", TestEitherLeftRight);
        yield return ("Either MapBoth branches", TestEitherMapBoth);
        yield return ("Either Swap", TestEitherSwap);
        yield return ("Either FlatMap left chain", TestEitherFlatMap);
        yield return ("Either OrThrow left returns", TestEitherOrThrowLeft);
        yield return ("Either OrThrow right throws", TestEitherOrThrowRightThrows);
        yield return ("Function3 invoke", TestFunction3Invoke);
        yield return ("IdF Map round-trip", TestIdFMap);
        yield return ("IdF Point", TestIdFPoint);
        yield return ("IdF Ap single", TestIdFAp);
        yield return ("IdF Ap2", TestIdFAp2);
        yield return ("IdF Ap3", TestIdFAp3);
        yield return ("IdF Ap5", TestIdFAp5);
        yield return ("IdF Ap10", TestIdFAp10);
        yield return ("IdF Apply2", TestIdFApply2);
        yield return ("Const Map keeps constant", TestConstMapKeepsConstant);
        yield return ("Const Point returns monoid unit", TestConstPoint);
        yield return ("OptionalBox Map Some", TestOptionalBoxMapSome);
        yield return ("OptionalBox Map Empty", TestOptionalBoxMapEmpty);
        yield return ("OptionalBox Ap2 Some+Some", TestOptionalBoxAp2SomeSome);
        yield return ("OptionalBox Ap2 Some+Empty", TestOptionalBoxAp2SomeEmpty);
        yield return ("OptionalBox Traverse IdF", TestOptionalBoxTraverseIdF);
        yield return ("ListBox Map", TestListBoxMap);
        yield return ("ListBox Traverse IdF", TestListBoxTraverseIdF);
        yield return ("ListBox Flip", TestListBoxFlip);
        yield return ("Pair MapFirst/MapSecond", TestPairMap);
        yield return ("Pair Swap", TestPairSwap);
        yield return ("Products Of + Apply2", TestProductsOfApply2);
        yield return ("Products P3 Apply3", TestProductsP3Apply3);
        yield return ("BangEta on Bang returns Empty", TestBangEtaOnBang);
        yield return ("BangEta on Fun returns Bang", TestBangEtaOnFun);
        yield return ("LensAppId on Bang returns Empty", TestLensAppIdOnBang);
        yield return ("AppNest on non Apply returns Empty", TestAppNestOnNonApply);
        yield return ("AppNest on single Apply returns Empty", TestAppNestOnSingleApply);
        yield return ("AppNest on nested Apply merges funcs", TestAppNestOnNestedApply);
        yield return ("CataFuseSame on non Fold returns Empty", TestCataFuseSameOnNonFold);
        yield return ("CataFuseDifferent on non Fold returns Empty", TestCataFuseDifferentOnNonFold);
        yield return ("SortProj Rewrite on non Comp returns Empty", TestSortProjOnNonComp);
        yield return ("SortProj Rewrite on matching Apply pair returns Present", TestSortProjOnMatching);
        yield return ("SortInj Rewrite on non Comp returns Empty", TestSortInjOnNonComp);
        yield return ("SortInj Rewrite on matching Apply pair returns Present", TestSortInjOnMatching);
        yield return ("LensComp Rewrite on non Comp returns Empty", TestLensCompOnNonComp);
        yield return ("LensComp Rewrite on equal optics returns Present", TestLensCompOnEqualOptics);
        yield return ("FieldFinder on TagType returns Adapter", TestFieldFinderOnTagType);
        yield return ("FieldFinder name mismatch returns Continue", TestFieldFinderNameMismatch);
        yield return ("DataFixer end-to-end counter 41 to 42", TestDataFixerEndToEndCounterIncrement);
    }

    //buildExampleFixer本地构造端到端示例DataFixer不依赖全局DataFixers.DataFixer
    //DFU层不主动注册任何Fix业务层（NetCraft.Game）自行构建
    //测试用本地builder验证ExampleSchema+ExampleCounterIncrementFix流程
    private static NetCraft.DataFixer.DataFixer BuildExampleFixer()
    {
        var builder = new DataFixerBuilder(dataVersion: 1001);
        builder.AddSchema(1000, 0, (key, parent) => new ExampleSchema(key, parent));
        var outputSchema = builder.AddSchema(1001, 0, (key, parent) => new ExampleSchema(key, parent));
        builder.AddFixer(new ExampleCounterIncrementFix(outputSchema));
        return builder.Build().Fixer();
    }

    private static bool TestUnitEquality()
    {
        return Unit.Instance.Equals(Unit.Instance) && Unit.Instance.Equals(default(Unit));
    }

    private static bool TestEitherLeftRight()
    {
        var left = Either<string, int>.Left("err");
        var right = Either<string, int>.Right(42);
        return left.IsLeft && !left.IsRight
            && right.IsRight && !right.IsLeft
            && left.GetLeft().Get() == "err"
            && right.GetRight().Get() == 42;
    }

    private static bool TestEitherMapBoth()
    {
        var left = Either<string, int>.Left("err");
        var right = Either<string, int>.Right(42);
        var lm = left.MapBoth(s => s.Length, i => i.ToString());
        var rm = right.MapBoth(s => s.Length, i => i.ToString());
        return lm.IsLeft && lm.GetLeft().Get() == 3
            && rm.IsRight && rm.GetRight().Get() == "42";
    }

    private static bool TestEitherSwap()
    {
        var left = Either<string, int>.Left("err");
        var right = Either<string, int>.Right(42);
        return left.Swap().IsRight && left.Swap().GetRight().Get() == "err"
            && right.Swap().IsLeft && right.Swap().GetLeft().Get() == 42;
    }

    private static bool TestEitherFlatMap()
    {
        var left = Either<string, int>.Left("err");
        var mapped = left.FlatMap(s => Either<int, int>.Left(s.Length));
        return mapped.IsLeft && mapped.GetLeft().Get() == 3;
    }

    private static bool TestEitherOrThrowLeft()
    {
        var left = Either<string, int>.Left("err");
        return left.OrThrow() == "err";
    }

    private static bool TestEitherOrThrowRightThrows()
    {
        var right = Either<string, int>.Right(42);
        try { right.OrThrow(); return false; }
        catch (InvalidOperationException) { return true; }
    }

    private static bool TestFunction3Invoke()
    {
        Function3<int, int, int, int> f = (a, b, c) => a + b + c;
        return f(1, 2, 3) == 6;
    }

    private static bool TestIdFMap()
    {
        var instance = IdFInstance.InstanceOf;
        var result = instance.Map(x => x * 2, IdFs.Create(5));
        return IdFs.Get(result) == 10;
    }

    private static bool TestIdFPoint()
    {
        var result = IdFInstance.InstanceOf.Point(42);
        return IdFs.Get(result) == 42;
    }

    private static bool TestIdFAp()
    {
        //Ap是接口默认方法须通过Applicative接口类型调用
        Applicative<IdFs.Mu, IdFInstance.Mu> instance = IdFInstance.InstanceOf;
        var func = instance.Point<Func<int, int>>(x => x + 1);
        var result = instance.Ap(func, IdFs.Create(5));
        return IdFs.Get(result) == 6;
    }

    private static bool TestIdFAp2()
    {
        Applicative<IdFs.Mu, IdFInstance.Mu> instance = IdFInstance.InstanceOf;
        var func = instance.Point<Func<int, int, int>>((a, b) => a + b);
        var result = instance.Ap2(func, IdFs.Create(2), IdFs.Create(3));
        return IdFs.Get(result) == 5;
    }

    private static bool TestIdFAp3()
    {
        Applicative<IdFs.Mu, IdFInstance.Mu> instance = IdFInstance.InstanceOf;
        Function3<int, int, int, int> f = (a, b, c) => a + b + c;
        var func = instance.Point(f);
        var result = instance.Ap3(func, IdFs.Create(1), IdFs.Create(2), IdFs.Create(3));
        return IdFs.Get(result) == 6;
    }

    private static bool TestIdFAp5()
    {
        Applicative<IdFs.Mu, IdFInstance.Mu> instance = IdFInstance.InstanceOf;
        Function5<int, int, int, int, int, int> f = (a, b, c, d, e) => a + b + c + d + e;
        var func = instance.Point(f);
        var result = instance.Ap5(func, IdFs.Create(1), IdFs.Create(2), IdFs.Create(3), IdFs.Create(4), IdFs.Create(5));
        return IdFs.Get(result) == 15;
    }

    private static bool TestIdFAp10()
    {
        Applicative<IdFs.Mu, IdFInstance.Mu> instance = IdFInstance.InstanceOf;
        Function10<int, int, int, int, int, int, int, int, int, int, int> f = (a, b, c, d, e, g, h, i, j, k) =>
            a + b + c + d + e + g + h + i + j + k;
        var func = instance.Point(f);
        var args = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var result = instance.Ap10(
            func,
            IdFs.Create(args[0]), IdFs.Create(args[1]), IdFs.Create(args[2]), IdFs.Create(args[3]), IdFs.Create(args[4]),
            IdFs.Create(args[5]), IdFs.Create(args[6]), IdFs.Create(args[7]), IdFs.Create(args[8]), IdFs.Create(args[9])
        );
        return IdFs.Get(result) == 55;
    }

    private static bool TestIdFApply2()
    {
        Applicative<IdFs.Mu, IdFInstance.Mu> instance = IdFInstance.InstanceOf;
        var result = instance.Apply2((a, b) => a * b, IdFs.Create(6), IdFs.Create(7));
        return IdFs.Get(result) == 42;
    }

    private static bool TestConstMapKeepsConstant()
    {
        var monoid = Monoid<List<int>>.ListMonoid<int>();
        var instance = new ConstInstance<List<int>>(monoid);
        var constant = new List<int> { 1, 2, 3 };
        var constBox = Consts.Create<List<int>, int>(constant);
        var mapped = instance.Map(x => x * 100, constBox);
        var result = Consts.Unbox<List<int>, int>(mapped);
        return result.SequenceEqual(constant);
    }

    private static bool TestConstPoint()
    {
        var monoid = Monoid<List<int>>.ListMonoid<int>();
        var instance = new ConstInstance<List<int>>(monoid);
        var pointed = instance.Point("ignored");
        var result = Consts.Unbox<List<int>, string>(pointed);
        return result.Count == 0;
    }

    private static bool TestOptionalBoxMapSome()
    {
        var instance = OptionalBoxInstance.InstanceOf;
        var result = instance.Map(x => x + 1, OptionalBox<int>.Create(Optional<int>.Of(5)));
        return OptionalBox<int>.Unbox<int>(result).Get() == 6;
    }

    private static bool TestOptionalBoxMapEmpty()
    {
        var instance = OptionalBoxInstance.InstanceOf;
        var result = instance.Map(x => x + 1, OptionalBox<int>.Create(Optional<int>.Empty()));
        return !OptionalBox<int>.Unbox<int>(result).IsPresent;
    }

    private static bool TestOptionalBoxAp2SomeSome()
    {
        //Ap2是接口默认方法须通过Applicative接口类型调用
        Applicative<OptionalBoxes.Mu, OptionalBoxInstance.Mu> instance = OptionalBoxInstance.InstanceOf;
        var func = instance.Point<Func<int, int, int>>((a, b) => a + b);
        var result = instance.Ap2(func,
            OptionalBox<int>.Create(Optional<int>.Of(2)),
            OptionalBox<int>.Create(Optional<int>.Of(3)));
        return OptionalBox<int>.Unbox<int>(result).Get() == 5;
    }

    private static bool TestOptionalBoxAp2SomeEmpty()
    {
        Applicative<OptionalBoxes.Mu, OptionalBoxInstance.Mu> instance = OptionalBoxInstance.InstanceOf;
        var func = instance.Point<Func<int, int, int>>((a, b) => a + b);
        var result = instance.Ap2(func,
            OptionalBox<int>.Create(Optional<int>.Of(2)),
            OptionalBox<int>.Create(Optional<int>.Empty()));
        return !OptionalBox<int>.Unbox<int>(result).IsPresent;
    }

    private static bool TestOptionalBoxTraverseIdF()
    {
        var instance = OptionalBoxInstance.InstanceOf;
        var idApp = IdFInstance.InstanceOf;
        var input = OptionalBox<int>.Create(Optional<int>.Of(5));
        var result = instance.Traverse<IdFs.Mu, IdFInstance.Mu, int, int>(idApp, x => IdFs.Create(x * 2), input);
        var unboxed = OptionalBox<int>.Unbox<int>(IdFs.Get(result));
        return unboxed.IsPresent && unboxed.Get() == 10;
    }

    private static bool TestListBoxMap()
    {
        var instance = ListBoxInstance.InstanceOf;
        var input = ListBox<int>.Create(new List<int> { 1, 2, 3 });
        var result = instance.Map(x => x * 2, input);
        return ListBox<int>.Unbox<int>(result).SequenceEqual(new[] { 2, 4, 6 });
    }

    private static bool TestListBoxTraverseIdF()
    {
        var idApp = IdFInstance.InstanceOf;
        var input = new List<int> { 1, 2, 3 };
        var result = ListBoxInstance.Traverse<IdFs.Mu, IdFInstance.Mu, int, int>(idApp, x => IdFs.Create(x * 2), input);
        return IdFs.Get(result).SequenceEqual(new[] { 2, 4, 6 });
    }

    private static bool TestListBoxFlip()
    {
        var idApp = IdFInstance.InstanceOf;
        var input = new List<App<IdFs.Mu, int>> { IdFs.Create(1), IdFs.Create(2), IdFs.Create(3) };
        var result = ListBoxInstance.Flip<IdFs.Mu, IdFInstance.Mu, int>(idApp, input);
        return IdFs.Get(result).SequenceEqual(new[] { 1, 2, 3 });
    }

    private static bool TestPairMap()
    {
        //完全限定名消歧Codec.Pair与DataFixer.Util.Pair
        var pair = NetCraft.DataFixer.Util.Pair<int, string>.Of(5, "hello");
        var mf = pair.MapFirst(x => x * 2);
        var ms = pair.MapSecond(s => s.Length);
        return mf.First == 10 && mf.Second == "hello"
            && ms.First == 5 && ms.Second == 5;
    }

    private static bool TestPairSwap()
    {
        var pair = NetCraft.DataFixer.Util.Pair<int, string>.Of(5, "hello");
        var swapped = pair.Swap();
        return swapped.First == "hello" && swapped.Second == 5;
    }

    private static bool TestProductsOfApply2()
    {
        var instance = IdFInstance.InstanceOf;
        var p = Products.Of(2, 3);
        var result = p.Apply(instance, (int a, int b) => a + b);
        return IdFs.Get(result) == 5;
    }

    private static bool TestProductsP3Apply3()
    {
        var instance = IdFInstance.InstanceOf;
        var p = new Products.P3<IdFs.Mu, int, int, int>(IdFs.Create(1), IdFs.Create(2), IdFs.Create(3));
        Function3<int, int, int, int> f = (a, b, c) => a * b * c;
        var result = p.Apply(instance, f);
        return IdFs.Get(result) == 6;
    }

    //BangEta 规则对 Bang 表达式自身返回 Empty
    private static bool TestBangEtaOnBang()
    {
        var bang = Functions.Bang<int>(DSL.IntType());
        var rule = PointFreeRule.BangEta();
        var result = rule.Rewrite(bang);
        return !result.IsPresent;
    }

    //BangEta 规则对 Fun<A, EmptyPart> 类型表达式包装为 Bang<A>
    private static bool TestBangEtaOnFun()
    {
        var fun = Functions.Fun("test", _ => _ => Unit.Instance, DSL.IntType(), DSL.EmptyPartType());
        var rule = PointFreeRule.BangEta();
        var result = rule.Rewrite(fun);
        return result.IsPresent && result.Get() is Bang<int>;
    }

    //LensAppId 规则对非 Apply 表达式返回 Empty
    private static bool TestLensAppIdOnBang()
    {
        var bang = Functions.Bang<int>(DSL.IntType());
        var rule = PointFreeRule.LensAppId();
        var result = rule.Rewrite(bang);
        return !result.IsPresent;
    }

    //AppNest 规则对非 Apply 表达式返回 Empty
    private static bool TestAppNestOnNonApply()
    {
        var bang = Functions.Bang<int>(DSL.IntType());
        var rule = PointFreeRule.AppNest();
        var result = rule.Rewrite(bang);
        return !result.IsPresent;
    }

    //AppNest 规则对单层 Apply（arg 非 Apply）返回 Empty
    private static bool TestAppNestOnSingleApply()
    {
        var bang = Functions.Bang<int>(DSL.IntType());
        var funcType = DSL.Func(DSL.IntType(), DSL.EmptyPartType());
        var func = Functions.Bang<Func<int, Unit>>(funcType);
        var apply = Functions.App(func, bang);
        var rule = PointFreeRule.AppNest();
        var result = rule.Rewrite(apply);
        return !result.IsPresent;
    }

    //AppNest 规则对嵌套 Apply 用 Id 短路合并 ap id (ap id arg)
    private static bool TestAppNestOnNestedApply()
    {
        var argType = DSL.Func(DSL.IntType(), DSL.EmptyPartType());
        var id = Functions.Id<Func<int, Unit>>(argType);
        var bang = Functions.Bang<int>(DSL.IntType());
        var inner = Functions.App(id, bang);
        var outer = Functions.App(id, inner);
        var rule = PointFreeRule.AppNest();
        var result = rule.Rewrite(outer);
        return result.IsPresent;
    }

    //CataFuseSame 对非 Fold 表达式返回 Empty
    private static bool TestCataFuseSameOnNonFold()
    {
        var bang = Functions.Bang<int>(DSL.IntType());
        var rule = PointFreeRule.CataFuseSame();
        var result = rule.Rewrite(bang);
        return !result.IsPresent;
    }

    //CataFuseDifferent 对非 Fold 表达式返回 Empty
    private static bool TestCataFuseDifferentOnNonFold()
    {
        var bang = Functions.Bang<int>(DSL.IntType());
        var rule = PointFreeRule.CataFuseDifferent();
        var result = rule.Rewrite(bang);
        return !result.IsPresent;
    }

    //SortProj Rewrite 对非 Comp 返回 Empty
    private static bool TestSortProjOnNonComp()
    {
        var bang = Functions.Bang<int>(DSL.IntType());
        var rule = PointFreeRule.SortProj();
        var result = rule.Rewrite(bang);
        return !result.IsPresent;
    }

    //SortInj Rewrite 对非 Comp 返回 Empty
    private static bool TestSortInjOnNonComp()
    {
        var bang = Functions.Bang<int>(DSL.IntType());
        var rule = PointFreeRule.SortInj();
        var result = rule.Rewrite(bang);
        return !result.IsPresent;
    }

    //LensComp Rewrite 对非 Comp 返回 Empty
    private static bool TestLensCompOnNonComp()
    {
        var bang = Functions.Bang<int>(DSL.IntType());
        var rule = PointFreeRule.LensComp();
        var result = rule.Rewrite(bang);
        return !result.IsPresent;
    }

    //SortProj Rewrite 对 first=Proj2 second=Proj1 的 Apply 对返回 Present
    //用 Unsafe.As 强转构造 Apply 与 Comp 对齐 Java 类型擦除语义
    private static bool TestSortProjOnMatching()
    {
        var intType = DSL.IntType();
        var proj1Optic = TypedOptics.Proj1<int, int, int>(intType, intType, intType);
        var proj2Optic = TypedOptics.Proj2<int, int, int>(intType, intType, intType);
        var pt1 = Functions.ProfunctorTransformer(proj2Optic);
        var pt2 = Functions.ProfunctorTransformer(proj1Optic);
        var arg = Functions.Id(intType);
        var apply1 = UnsafeAsApply(pt1, arg);
        var apply2 = UnsafeAsApply(pt2, arg);
        var comp = UnsafeAsComp(apply1, apply2);
        var rule = PointFreeRule.SortProj();
        var result = rule.Rewrite(comp);
        return result.IsPresent;
    }

    //SortInj Rewrite 对 first=Inj2 second=Inj1 的 Apply 对返回 Present
    private static bool TestSortInjOnMatching()
    {
        var intType = DSL.IntType();
        var inj1Optic = TypedOptics.Inj1<int, int, int>(intType, intType, intType);
        var inj2Optic = TypedOptics.Inj2<int, int, int>(intType, intType, intType);
        var pt1 = Functions.ProfunctorTransformer(inj2Optic);
        var pt2 = Functions.ProfunctorTransformer(inj1Optic);
        var arg = Functions.Id(intType);
        var apply1 = UnsafeAsApply(pt1, arg);
        var apply2 = UnsafeAsApply(pt2, arg);
        var comp = UnsafeAsComp(apply1, apply2);
        var rule = PointFreeRule.SortInj();
        var result = rule.Rewrite(comp);
        return result.IsPresent;
    }

    //LensComp Rewrite 对两个相同 ProfunctorTransformer 触发全等分支返回 Present
    private static bool TestLensCompOnEqualOptics()
    {
        var intType = DSL.IntType();
        var proj1Optic = TypedOptics.Proj1<int, int, int>(intType, intType, intType);
        var pt1 = Functions.ProfunctorTransformer(proj1Optic);
        var pt2 = Functions.ProfunctorTransformer(proj1Optic);
        var arg1 = Functions.Id(intType);
        var arg2 = Functions.Id(intType);
        var apply1 = UnsafeAsApply(pt1, arg1);
        var apply2 = UnsafeAsApply(pt2, arg2);
        var comp = UnsafeAsComp(apply1, apply2);
        var rule = PointFreeRule.LensComp();
        var result = rule.Rewrite(comp);
        return result.IsPresent;
    }

    //UnsafeAsApply 用 Unsafe.As 强转构造 Apply<object,object>
    //对齐 Java 类型擦除让 ProfunctorTransformer 当作 PointFree<Func<object,object>> 用
    private static Apply<object, object> UnsafeAsApply(object func, object arg)
    {
        var fObj = func;
        var fCast = System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<object, object>>>(ref fObj);
        var aObj = arg;
        var aCast = System.Runtime.CompilerServices.Unsafe.As<object, PointFree<object>>(ref aObj);
        return new Apply<object, object>(fCast, aCast);
    }

    //UnsafeAsComp 用 Functions.Comp<object,object,object> 构造复合
    private static PointFree<Func<object, object>> UnsafeAsComp(object first, object second)
    {
        var fObj = first;
        var fCast = System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<object, object>>>(ref fObj);
        var sObj = second;
        var sCast = System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<object, object>>>(ref sObj);
        return Functions.Comp<object, object, object>(fCast, sCast);
    }

    //FieldFinder 在 Tag.TagType 上按名+元素类型匹配返回 Adapter(Optics.Id)
    //对应原版Java FieldFinder.Matcher.match的Tag.TagType分支
    //用PrimitiveType<object>作元素以便强转Type<object>走FindType入口
    private static bool TestFieldFinderOnTagType()
    {
        var elementType = new Const.PrimitiveType<object>(null!);
        var tagType = DSL.Field("foo", elementType);
        var finder = DSL.FieldFinder<object>("foo", elementType);
        var result = finder.FindType(tagType, recurse: false);
        if (!result.IsLeft) return false;
        var optic = result.GetLeft().Get();
        return optic.SType().Equals(tagType)
            && optic.AType().Equals(elementType);
    }

    //FieldFinder 在 TagType 上名字不匹配返回 FieldNotFoundException
    //对应原版Java FieldFinder.Matcher.match的Tag.TagType分支name不匹配路径
    private static bool TestFieldFinderNameMismatch()
    {
        var elementType = new Const.PrimitiveType<object>(null!);
        var tagType = DSL.Field("foo", elementType);
        var finder = DSL.FieldFinder<object>("bar", elementType);
        var result = finder.FindType(tagType, recurse: false);
        if (!result.IsRight) return false;
        return result.GetRight().Get() is not NetCraft.DataFixer.Types.Type<object>.Continue;
    }

    //端到端示例验证本地构建的修复器能把counter从41升到42
    //走完整流程Schema注册TypeTemplate构建DataFix规则应用ReadAndWrite编码解码
    private static bool TestDataFixerEndToEndCounterIncrement()
    {
        NetCraft.DataFixer.DataFixer fixer;
        try
        {
            fixer = BuildExampleFixer();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("BuildExampleFixer failed: " + ex.Message);
            Console.Error.WriteLine(ex.StackTrace);
            return false;
        }
        try
        {
            var inputTag = NetCraft.Nbt.IntTag.ValueOf(41);
            var input = new Dynamic<NetCraft.Nbt.Tag>(NetCraft.Nbt.NbtOps.Instance, inputTag);
            var updated = fixer.Update(References.ExampleCounter, input, version: 1000, newVersion: 1001);
            var result = updated.Value.AsInt();
            return result == 42;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Update failed: " + ex.Message);
            Console.Error.WriteLine("Inner: " + ex.InnerException?.Message);
            Console.Error.WriteLine(ex.StackTrace);
            Console.Error.WriteLine(ex.InnerException?.StackTrace);
            return false;
        }
    }
}
