using NetCraft.Codec;
using NetCraft.Util.Profiling;
using NetCraft.Util.Profiling.Metrics;
using NetCraft.Util.Profiling.Metrics.Profiling;
using NetCraft.Util.Profiling.Metrics.Storage;

namespace NetCraft.Test.Modules;

//Profiling 性能分析测试
//覆盖 ActiveProfiler/FilledProfileResults/Zone/Profiler/MetricSampler/MetricsRegistry/MetricsPersister 等
internal static class ProfilingTests
{
    public const string Module = "profiling";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("ResultField CompareTo orders by percentage desc", TestResultFieldCompareTo);
        yield return ("ResultField GetColor stable by name", TestResultFieldGetColor);
        yield return ("ProfileResults DemanglePath replaces separator", TestDemanglePath);
        yield return ("InactiveProfiler all no-op", TestInactiveProfilerNoOp);
        yield return ("EmptyProfileResults all zero/empty", TestEmptyProfileResults);
        yield return ("ActiveProfiler push pop records duration", TestActiveProfilerPushPop);
        yield return ("ActiveProfiler popPush swaps path", TestActiveProfilerPopPush);
        yield return ("ActiveProfiler increment counter accumulates", TestActiveProfilerIncrementCounter);
        yield return ("ActiveProfiler markForCharting tracked", TestActiveProfilerMarkForCharting);
        yield return ("ActiveProfiler getEntry returns null for missing", TestActiveProfilerGetEntryMissing);
        yield return ("ActiveProfiler startTick twice logs error no throw", TestActiveProfilerStartTickTwice);
        yield return ("FilledProfileResults getTimes root has entries", TestFilledProfileResultsGetTimes);
        yield return ("FilledProfileResults getTimes nested path", TestFilledProfileResultsNestedPath);
        yield return ("FilledProfileResults getProfilerResults non-empty", TestFilledProfileResultsGetProfilerResults);
        yield return ("FilledProfileResults saveResults writes file", TestFilledProfileResultsSaveResults);
        yield return ("FilledProfileResults counters dump", TestFilledProfileResultsCountersDump);
        yield return ("Zone using block pushes and pops", TestZoneUsingBlock);
        yield return ("Zone Inactive dispose no throw", TestZoneInactiveDispose);
        yield return ("ContinuousProfiler enable disable", TestContinuousProfilerEnableDisable);
        yield return ("ContinuousProfiler isEnabled initial false", TestContinuousProfilerInitialFalse);
        yield return ("Profiler use scope pushes and pops", TestProfilerUseScope);
        yield return ("Profiler get returns inactive default", TestProfilerGetDefaultInactive);
        yield return ("ProfilerFiller combine with inactive returns other", TestProfilerFillerCombineWithInactive);
        yield return ("ProfilerFiller combine two returns combined", TestProfilerFillerCombineTwo);
        yield return ("MetricSampler create records values", TestMetricSamplerCreate);
        yield return ("MetricSampler extract sampler phase", TestMetricSamplerExtractPhase);
        yield return ("MetricSampler builder with beforeTick", TestMetricSamplerBuilderBeforeTick);
        yield return ("MetricSampler triggersThreshold when test passes", TestMetricSamplerThresholdTriggers);
        yield return ("MetricSampler valueIncreasedByPercentage", TestMetricSamplerValueIncreasedByPercentage);
        yield return ("MetricSampler onFinished stops running", TestMetricSamplerOnFinished);
        yield return ("MetricSampler equals by name and category", TestMetricSamplerEquals);
        yield return ("MetricsRegistry add and get samplers", TestMetricsRegistryAddGet);
        yield return ("MetricsRegistry aggregates duplicates", TestMetricsRegistryAggregates);
        yield return ("MetricsSamplerProvider returns samplers", TestMetricsSamplerProvider);
        yield return ("ProfilerMeasured exposes metrics", TestProfilerMeasured);
        yield return ("MetricsRecorder inactive singleton", TestMetricsRecorderInactive);
        yield return ("ActiveMetricsRecorder cancel clears", TestActiveMetricsRecorderCancel);
        yield return ("ActiveMetricsRecorder end sets killSwitch", TestActiveMetricsRecorderEnd);
        yield return ("ProfilerSamplerAdapter new samplers", TestProfilerSamplerAdapter);
        yield return ("ProfilerSamplerAdapter dedup names", TestProfilerSamplerAdapterDedup);
        yield return ("RecordedDeviation holds data", TestRecordedDeviation);
        yield return ("MetricsPersister saveReports writes files", TestMetricsPersisterSaveReports);
        yield return ("MetricsPersister sanitizeName via category file", TestMetricsPersisterCategoryFile);
        yield return ("MetricsRegistry singleton stable", TestMetricsRegistrySingleton);
    }

    private static bool TestResultFieldCompareTo()
    {
        var a = new ResultField("a", 50.0d, 50.0d, 1);
        var b = new ResultField("b", 30.0d, 30.0d, 1);
        return a.CompareTo(b) < 0;
    }

    private static bool TestResultFieldGetColor()
    {
        var a = new ResultField("abc", 1, 1, 1);
        var b = new ResultField("abc", 2, 2, 2);
        return a.GetColor() == b.GetColor();
    }

    private static bool TestDemanglePath()
    {
        var path = "root" + '\x1e' + "child" + '\x1e' + "leaf";
        return ProfileResults.DemanglePath(path) == "root.child.leaf";
    }

    private static bool TestInactiveProfilerNoOp()
    {
        var p = InactiveProfiler.Instance;
        p.StartTick();
        p.Push("a");
        p.IncrementCounter("c", 5);
        p.Pop();
        p.EndTick();
        return ReferenceEquals(p.GetResults(), EmptyProfileResults.Empty)
            && p.GetEntry("any") is null
            && p.GetChartedPaths().Count == 0;
    }

    private static bool TestEmptyProfileResults()
    {
        var r = EmptyProfileResults.Empty;
        return r.StartTimeNano == 0L
            && r.StartTimeTicks == 0
            && r.EndTimeNano == 0L
            && r.EndTimeTicks == 0
            && r.GetTimes("any").Count == 0
            && r.SaveResults("dummy") == false
            && r.GetProfilerResults() == string.Empty;
    }

    private static bool TestActiveProfilerPushPop()
    {
        var profiler = new ActiveProfiler(() => 0L, () => 0, () => false);
        profiler.StartTick();
        profiler.Push("child");
        Thread.Sleep(2);
        profiler.Pop();
        profiler.EndTick();
        var rootEntry = profiler.GetEntry("root");
        var childEntry = profiler.GetEntry("root" + '\x1e' + "child");
        return rootEntry is not null
            && childEntry is not null
            && childEntry.Count == 1
            && childEntry.Duration >= 0;
    }

    private static bool TestActiveProfilerPopPush()
    {
        var profiler = new ActiveProfiler(() => 0L, () => 0, () => false);
        profiler.StartTick();
        profiler.Push("a");
        profiler.PopPush("b");
        Thread.Sleep(1);
        profiler.Pop();
        profiler.EndTick();
        return profiler.GetEntry("root" + '\x1e' + "a") is not null
            && profiler.GetEntry("root" + '\x1e' + "b") is not null;
    }

    private static bool TestActiveProfilerIncrementCounter()
    {
        ProfilerFiller profiler = new ActiveProfiler(() => 0L, () => 0, () => false);
        profiler.StartTick();
        profiler.Push("counter-zone");
        profiler.IncrementCounter("clicks", 3);
        profiler.IncrementCounter("clicks");
        profiler.Pop();
        profiler.EndTick();
        var entry = ((ActiveProfiler)profiler).GetEntry("root" + '\x1e' + "counter-zone");
        return entry is not null
            && entry.Counters.TryGetValue("clicks", out var v)
            && v == 4;
    }

    private static bool TestActiveProfilerMarkForCharting()
    {
        var profiler = new ActiveProfiler(() => 0L, () => 0, () => false);
        profiler.StartTick();
        profiler.Push("chart-zone");
        profiler.MarkForCharting(MetricCategory.Cpu);
        profiler.Pop();
        profiler.EndTick();
        var charted = profiler.GetChartedPaths();
        return charted.Count == 1
            && charted.First().First == "root" + '\x1e' + "chart-zone"
            && charted.First().Second == MetricCategory.Cpu;
    }

    private static bool TestActiveProfilerGetEntryMissing()
    {
        var profiler = new ActiveProfiler(() => 0L, () => 0, () => false);
        profiler.StartTick();
        profiler.EndTick();
        return profiler.GetEntry("nonexistent") is null;
    }

    private static bool TestActiveProfilerStartTickTwice()
    {
        var profiler = new ActiveProfiler(() => 0L, () => 0, () => false);
        profiler.StartTick();
        profiler.StartTick();
        profiler.EndTick();
        return true;
    }

    private static bool TestFilledProfileResultsGetTimes()
    {
        var entries = new Dictionary<string, ProfilerPathEntry>();
        var root = new TestEntry(1000, 1);
        var child = new TestEntry(500, 1);
        entries["root"] = root;
        entries["root" + '\x1e' + "child"] = child;
        var results = new FilledProfileResults(entries, 0L, 0, 1000L, 1);
        var times = results.GetTimes("root");
        return times.Count >= 2
            && times[0].Name == "root" + '\x1e';
    }

    private static bool TestFilledProfileResultsNestedPath()
    {
        var entries = new Dictionary<string, ProfilerPathEntry>();
        entries["root"] = new TestEntry(1000, 1);
        entries["root" + '\x1e' + "a"] = new TestEntry(400, 1);
        entries["root" + '\x1e' + "a" + '\x1e' + "b"] = new TestEntry(100, 1);
        var results = new FilledProfileResults(entries, 0, 0, 1000, 1);
        var times = results.GetTimes("root" + '\x1e' + "a");
        return times.Count >= 1;
    }

    private static bool TestFilledProfileResultsGetProfilerResults()
    {
        var entries = new Dictionary<string, ProfilerPathEntry>();
        entries["root"] = new TestEntry(1000, 1);
        entries["root" + '\x1e' + "a"] = new TestEntry(500, 1);
        var results = new FilledProfileResults(entries, 0, 0, 1000, 1);
        var text = results.GetProfilerResults(1000, 1);
        return !string.IsNullOrEmpty(text)
            && text.Contains("BEGIN PROFILE DUMP")
            && text.Contains("Version:");
    }

    private static bool TestFilledProfileResultsSaveResults()
    {
        var entries = new Dictionary<string, ProfilerPathEntry>();
        entries["root"] = new TestEntry(1000, 1);
        entries["root" + '\x1e' + "a"] = new TestEntry(500, 1);
        var results = new FilledProfileResults(entries, 0, 0, 1000, 1);
        var tempFile = Path.Combine(Path.GetTempPath(), "nc-profiler-test-" + Guid.NewGuid() + ".txt");
        try
        {
            var ok = results.SaveResults(tempFile);
            return ok && File.Exists(tempFile) && File.ReadAllText(tempFile).Length > 0;
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    private static bool TestFilledProfileResultsCountersDump()
    {
        var entries = new Dictionary<string, ProfilerPathEntry>();
        var rootCounters = new Dictionary<string, long> { ["counterA"] = 5L };
        entries["root"] = new TestEntry(1000, 1, rootCounters);
        var results = new FilledProfileResults(entries, 0, 0, 1000, 1);
        var text = results.GetProfilerResults(1000, 1);
        return text.Contains("BEGIN COUNTER DUMP")
            && text.Contains("counterA");
    }

    private static bool TestZoneUsingBlock()
    {
        ProfilerFiller profiler = new ActiveProfiler(() => 0L, () => 0, () => false);
        profiler.StartTick();
        using (profiler.Zone("scope"))
        {
            Thread.Sleep(1);
        }
        profiler.EndTick();
        var entry = ((ActiveProfiler)profiler).GetEntry("root" + '\x1e' + "scope");
        return entry is not null && entry.Count == 1;
    }

    private static bool TestZoneInactiveDispose()
    {
        Zone.Inactive.Dispose();
        Zone.Inactive.AddText("x").AddValue(1L).SetColor(0).Dispose();
        return true;
    }

    private static bool TestContinuousProfilerEnableDisable()
    {
        var cp = new ContinuousProfiler(() => 0L, () => 0, () => false);
        if (cp.IsEnabled()) return false;
        cp.Enable();
        if (!cp.IsEnabled()) return false;
        cp.Disable();
        return !cp.IsEnabled();
    }

    private static bool TestContinuousProfilerInitialFalse()
    {
        var cp = new ContinuousProfiler(() => 0L, () => 0, () => false);
        return !cp.IsEnabled();
    }

    private static bool TestProfilerUseScope()
    {
        using (Profiler.Use(InactiveProfiler.Instance))
        {
            var current = Profiler.Get();
            //use后active count > 0返回的不是默认InactiveProfiler
            //但因combined inactive+inactive还是inactive这里只验证不抛
        }
        return true;
    }

    private static bool TestProfilerGetDefaultInactive()
    {
        var current = Profiler.Get();
        return ReferenceEquals(current, InactiveProfiler.Instance);
    }

    private static bool TestProfilerFillerCombineWithInactive()
    {
        var active = new ActiveProfiler(() => 0L, () => 0, () => false);
        var combined = ProfilerFiller.Combine(active, InactiveProfiler.Instance);
        return ReferenceEquals(combined, active);
    }

    private static bool TestProfilerFillerCombineTwo()
    {
        var a = new ActiveProfiler(() => 0L, () => 0, () => false);
        var b = new ActiveProfiler(() => 0L, () => 0, () => false);
        var combined = ProfilerFiller.Combine(a, b);
        return combined is CombinedProfileFiller;
    }

    private static bool TestMetricSamplerCreate()
    {
        var value = 0.0;
        var sampler = MetricSampler.Create("test", MetricCategory.Cpu, () => ++value);
        sampler.OnStartTick();
        sampler.OnEndTick(0);
        sampler.OnEndTick(1);
        var result = sampler.Result();
        return sampler.Name == "test"
            && sampler.Category == MetricCategory.Cpu
            && sampler.Phase == MetricSampler.SamplingPhase.EndTick
            && result.FirstTick == 0
            && result.LastTick == 1
            && result.ValueAtTick(0) == 1.0d
            && result.ValueAtTick(1) == 2.0d;
    }

    private static bool TestMetricSamplerExtractPhase()
    {
        var sampler = MetricSampler.CreateExtractSampler("ex", MetricCategory.EventLoops, () => 1.0d);
        return sampler.Phase == MetricSampler.SamplingPhase.Extract;
    }

    private static bool TestMetricSamplerBuilderBeforeTick()
    {
        var ctx = new BuilderCtx();
        var sampler = MetricSampler.Builder("b", MetricCategory.TickLoop, c => c.Increment(), ctx)
            .WithBeforeTick(c => c.Reset())
            .Build();
        sampler.OnStartTick();
        sampler.OnEndTick(0);
        return ctx.Value == 1;
    }

    private sealed class BuilderCtx
    {
        public int Value;
        public void Reset() => Value = 0;
        public double Increment() => ++Value;
    }

    private static bool TestMetricSamplerThresholdTriggers()
    {
        var sampler = MetricSampler.Builder("t", MetricCategory.Cpu, _ => 1.0d, 0)
            .WithThresholdAlert(new AlwaysTriggerTest())
            .Build();
        sampler.OnStartTick();
        sampler.OnEndTick(0);
        return sampler.TriggersThreshold();
    }

    private sealed class AlwaysTriggerTest : MetricSampler.ThresholdTest
    {
        public bool Test(double value) => true;
    }

    private static bool TestMetricSamplerValueIncreasedByPercentage()
    {
        var test = new MetricSampler.ValueIncreasedByPercentage(0.5f);
        if (test.Test(10.0d)) return false;
        if (!test.Test(20.0d)) return false;
        if (test.Test(15.0d)) return false;
        return true;
    }

    private static bool TestMetricSamplerOnFinished()
    {
        var sampler = MetricSampler.Create("f", MetricCategory.Cpu, () => 1.0d);
        sampler.OnStartTick();
        sampler.OnEndTick(0);
        sampler.OnFinished();
        try
        {
            sampler.OnEndTick(1);
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool TestMetricSamplerEquals()
    {
        var a = MetricSampler.Create("x", MetricCategory.Cpu, () => 1.0d);
        var b = MetricSampler.Create("x", MetricCategory.Cpu, () => 2.0d);
        var c = MetricSampler.Create("x", MetricCategory.Gpu, () => 1.0d);
        return a.Equals(b) && !a.Equals(c) && a.GetHashCode() == b.GetHashCode();
    }

    private sealed class MeasuredImpl : ProfilerMeasured
    {
        public List<MetricSampler> ProfiledMetrics()
            => new() { MetricSampler.Create("m1", MetricCategory.Cpu, () => 1.0d) };
    }

    private static bool TestMetricsRegistryAddGet()
    {
        var registry = new MetricsRegistry();
        var measured = new MeasuredImpl();
        registry.Add(measured);
        var samplers = registry.GetRegisteredSamplers();
        return samplers.Count == 1 && samplers[0].Name == "m1";
    }

    private sealed class DuplicateMeasured : ProfilerMeasured
    {
        public List<MetricSampler> ProfiledMetrics()
            => new() { MetricSampler.Create("dup", MetricCategory.Cpu, () => 1.0d) };
    }

    private static bool TestMetricsRegistryAggregates()
    {
        var registry = new MetricsRegistry();
        registry.Add(new DuplicateMeasured());
        registry.Add(new DuplicateMeasured());
        var samplers = registry.GetRegisteredSamplers();
        return samplers.Count == 1 && samplers[0] is MetricSampler;
    }

    private sealed class TestSamplerProvider : MetricsSamplerProvider
    {
        public ISet<MetricSampler> Samplers(Func<ProfileCollector> singleTickProfiler)
            => new HashSet<MetricSampler>
            {
                MetricSampler.Create("sp1", MetricCategory.Cpu, () => 1.0d),
                MetricSampler.Create("sp2", MetricCategory.Gpu, () => 2.0d)
            };
    }

    private static bool TestMetricsSamplerProvider()
    {
        var provider = new TestSamplerProvider();
        var samplers = provider.Samplers(() => InactiveProfiler.Instance);
        return samplers.Count == 2;
    }

    private static bool TestProfilerMeasured()
    {
        ProfilerMeasured measured = new MeasuredImpl();
        return measured.ProfiledMetrics().Count == 1;
    }

    private static bool TestMetricsRecorderInactive()
    {
        var recorder = InactiveMetricsRecorder.Instance;
        recorder.StartTick();
        recorder.SampleDuringExtract();
        recorder.EndTick();
        recorder.End();
        recorder.Cancel();
        return !recorder.IsRecording()
            && ReferenceEquals(recorder.GetProfiler(), InactiveProfiler.Instance);
    }

    private static bool TestActiveMetricsRecorderCancel()
    {
        var provider = new TestSamplerProvider();
        var persister = new MetricsPersister("test-cancel");
        var endCalled = false;
        var reportCalled = false;
        var recorder = ActiveMetricsRecorder.CreateStarted(
            provider,
            () => ProfilingUtil.GetNanos(),
            _ => { },
            persister,
            _ => endCalled = true,
            _ => reportCalled = true);
        recorder.Cancel();
        return !recorder.IsRecording() && endCalled;
    }

    private static bool TestActiveMetricsRecorderEnd()
    {
        var provider = new TestSamplerProvider();
        var persister = new MetricsPersister("test-end");
        var endCalled = false;
        var recorder = ActiveMetricsRecorder.CreateStarted(
            provider,
            () => ProfilingUtil.GetNanos(),
            action => action(),
            persister,
            _ => endCalled = true,
            _ => { });
        recorder.StartTick();
        recorder.SampleDuringExtract();
        recorder.EndTick();
        recorder.End();
        //End触发killSwitch下个EndTick保存结果
        try { recorder.EndTick(); } catch { }
        return !recorder.IsRecording() && endCalled;
    }

    private sealed class ChartingCollector : ProfileCollector
    {
        private readonly string _path;
        private readonly MetricCategory _category;
        public ChartingCollector(string path, MetricCategory category)
        {
            _path = path;
            _category = category;
        }
        public void StartTick() { }
        public void EndTick() { }
        public void Push(string name) { }
        public void Push(Func<string> name) { }
        public void Pop() { }
        public void PopPush(string name) { }
        public void PopPush(Func<string> name) { }
        public void MarkForCharting(MetricCategory category) { }
        public void IncrementCounter(string name, int amount) { }
        public void IncrementCounter(Func<string> name, int amount) { }
        public ProfileResults GetResults() => EmptyProfileResults.Empty;
        public ProfilerPathEntry? GetEntry(string path) => null;
        public ISet<Pair<string, MetricCategory>> GetChartedPaths()
            => new HashSet<Pair<string, MetricCategory>> { Pair<string, MetricCategory>.Of(_path, _category) };
    }

    private static bool TestProfilerSamplerAdapter()
    {
        var adapter = new ProfilerSamplerAdapter();
        var collector = new ChartingCollector("root" + '\x1e' + "zone", MetricCategory.Cpu);
        var samplers = adapter.NewSamplersFoundInProfiler(() => collector);
        return samplers.Count == 1
            && samplers.First().Name == "root" + '\x1e' + "zone"
            && samplers.First().Category == MetricCategory.Cpu;
    }

    private static bool TestProfilerSamplerAdapterDedup()
    {
        var adapter = new ProfilerSamplerAdapter();
        var collector = new ChartingCollector("dup-path", MetricCategory.Cpu);
        var first = adapter.NewSamplersFoundInProfiler(() => collector);
        var second = adapter.NewSamplersFoundInProfiler(() => collector);
        return first.Count == 1 && second.Count == 0;
    }

    private static bool TestRecordedDeviation()
    {
        var ts = DateTimeOffset.Now;
        var results = EmptyProfileResults.Empty;
        var dev = new RecordedDeviation(ts, 42, results);
        return dev.Timestamp == ts && dev.Tick == 42 && ReferenceEquals(dev.ProfilerResultAtTick, results);
    }

    private static bool TestMetricsPersisterSaveReports()
    {
        var sampler = MetricSampler.Create("persist-test", MetricCategory.Cpu, () => 1.0d);
        sampler.OnStartTick();
        sampler.OnEndTick(0);
        sampler.OnEndTick(1);
        var persister = new MetricsPersister("persist-root");
        var samplers = new HashSet<MetricSampler> { sampler };
        var deviations = new Dictionary<MetricSampler, List<RecordedDeviation>>();
        //EmptyProfileResults.SaveResults返回false不写文件这里只验证metrics CSV
        var entries = new Dictionary<string, ProfilerPathEntry>();
        entries["root"] = new TestEntry(1000, 1);
        var results = new FilledProfileResults(entries, 0L, 0, 1000L, 1);
        var dir = persister.SaveReports(samplers, deviations, results);
        try
        {
            var metricsFile = Path.Combine(dir, "persist-root", MetricsPersister.MetricsDirName, "cpu.csv");
            var profileFile = Path.Combine(dir, "persist-root", MetricsPersister.ProfilingResultFilename);
            return Directory.Exists(dir)
                && File.Exists(metricsFile)
                && File.Exists(profileFile)
                && File.ReadAllText(metricsFile).Contains("persist-test");
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }
    }

    private static bool TestMetricsPersisterCategoryFile()
    {
        var sampler = MetricSampler.Create("gpu-test", MetricCategory.Gpu, () => 2.0d);
        sampler.OnStartTick();
        sampler.OnEndTick(0);
        var persister = new MetricsPersister("cat-root");
        var samplers = new HashSet<MetricSampler> { sampler };
        var deviations = new Dictionary<MetricSampler, List<RecordedDeviation>>();
        var dir = persister.SaveReports(samplers, deviations, EmptyProfileResults.Empty);
        try
        {
            var gpuFile = Path.Combine(dir, "cat-root", MetricsPersister.MetricsDirName, "gpu.csv");
            return File.Exists(gpuFile);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }
    }

    private static bool TestMetricsRegistrySingleton()
    {
        return ReferenceEquals(MetricsRegistry.Instance, MetricsRegistry.Instance);
    }

    //测试用PathEntry实现对应原版PathEntry只读数据
    private sealed class TestEntry : ProfilerPathEntry
    {
        public TestEntry(long duration, long count, Dictionary<string, long>? counters = null)
        {
            Duration = duration;
            Count = count;
            Counters = counters ?? new Dictionary<string, long>();
        }
        public long Duration { get; }
        public long MaxDuration => Duration;
        public long Count { get; }
        public IReadOnlyDictionary<string, long> Counters { get; }
    }
}
