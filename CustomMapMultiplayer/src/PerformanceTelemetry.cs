using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CustomMapMultiplayer
{
    internal enum PerformanceMetric
    {
        AcidAuthority,
        AcidPoolRefresh,
        AcidHeroScan,
        AcidHeroCache,
        AcidHook,
        AcidObservation,
        EntitySubmit,
        EntityPending,
        EntityPrune,
        TraceBuild,
        TraceDedup,
        DiagnosticWrite
    }

    // Main-thread, allocation-free counters while enabled. The default is off so
    // normal gameplay does not pay for stopwatch or histogram work.
    internal static class PerformanceTelemetry
    {
        private const float FlushIntervalSeconds = 2f;
        private const int FrameHistogramMaxMilliseconds = 250;
        private static readonly long[] Calls = new long[MetricCount];
        private static readonly long[] Items = new long[MetricCount];
        private static readonly long[] Hits = new long[MetricCount];
        private static readonly long[] Misses = new long[MetricCount];
        private static readonly long[] TotalTicks = new long[MetricCount];
        private static readonly long[] MaxTicks = new long[MetricCount];
        private static readonly long[] FrameHistogram =
            new long[FrameHistogramMaxMilliseconds + 1];
        private static bool _wasEnabled;
        private static float _nextFlushAt = float.NegativeInfinity;
        private static long _frameCount;
        private static double _frameTotalMilliseconds;

        private static int MetricCount
        {
            get { return (int)PerformanceMetric.DiagnosticWrite + 1; }
        }

        internal static bool Enabled
        {
            get
            {
                var settings = Plugin.Settings;
                return settings != null && settings.EnablePerformanceTelemetry;
            }
        }

        internal static long Begin(PerformanceMetric metric)
        {
            return EnsureEnabled() ? Stopwatch.GetTimestamp() : 0L;
        }

        internal static void End(PerformanceMetric metric, long startedAt)
        {
            if (startedAt == 0L || !EnsureEnabled())
            {
                return;
            }

            var elapsed = Stopwatch.GetTimestamp() - startedAt;
            var index = (int)metric;
            Calls[index]++;
            TotalTicks[index] += elapsed;
            if (elapsed > MaxTicks[index])
            {
                MaxTicks[index] = elapsed;
            }
        }

        internal static void Count(PerformanceMetric metric)
        {
            if (!EnsureEnabled())
            {
                return;
            }

            Calls[(int)metric]++;
        }

        internal static void AddItems(PerformanceMetric metric, int amount)
        {
            if (!EnsureEnabled() || amount <= 0)
            {
                return;
            }

            Items[(int)metric] += amount;
        }

        internal static void Hit(PerformanceMetric metric)
        {
            if (EnsureEnabled())
            {
                Hits[(int)metric]++;
            }
        }

        internal static void Miss(PerformanceMetric metric)
        {
            if (EnsureEnabled())
            {
                Misses[(int)metric]++;
            }
        }

        internal static void ObserveFrame(float unscaledDeltaTime)
        {
            if (!EnsureEnabled())
            {
                return;
            }

            var milliseconds = Mathf.Max(0f, unscaledDeltaTime * 1000f);
            var bin = Mathf.Min(
                FrameHistogramMaxMilliseconds,
                Mathf.CeilToInt(milliseconds));
            FrameHistogram[bin]++;
            _frameCount++;
            _frameTotalMilliseconds += milliseconds;
        }

        internal static void Update()
        {
            var enabled = Enabled;
            if (!enabled)
            {
                if (_wasEnabled)
                {
                    Flush("disabled");
                    ResetState();
                    _wasEnabled = false;
                }
                return;
            }

            EnsureEnabled();
            var now = Time.unscaledTime;
            if (now >= _nextFlushAt)
            {
                Flush("interval");
                ResetState();
                _nextFlushAt = now + FlushIntervalSeconds;
            }
        }

        internal static void Reset()
        {
            ResetState();
            _wasEnabled = false;
            _nextFlushAt = float.NegativeInfinity;
        }

        private static bool EnsureEnabled()
        {
            if (!Enabled)
            {
                return false;
            }

            if (!_wasEnabled)
            {
                ResetState();
                _wasEnabled = true;
                _nextFlushAt = Time.unscaledTime + FlushIntervalSeconds;
            }

            return true;
        }

        private static void ResetState()
        {
            Array.Clear(Calls, 0, Calls.Length);
            Array.Clear(Items, 0, Items.Length);
            Array.Clear(Hits, 0, Hits.Length);
            Array.Clear(Misses, 0, Misses.Length);
            Array.Clear(TotalTicks, 0, TotalTicks.Length);
            Array.Clear(MaxTicks, 0, MaxTicks.Length);
            Array.Clear(FrameHistogram, 0, FrameHistogram.Length);
            _frameCount = 0L;
            _frameTotalMilliseconds = 0d;
        }

        private static void Flush(string reason)
        {
            if (_frameCount == 0L && !HasMetricData())
            {
                return;
            }

            var frameAverage = _frameCount == 0L
                ? 0f
                : (float)(_frameTotalMilliseconds / _frameCount);
            var builder = new StringBuilder(1200);
            builder.Append("PERF_SUMMARY reason=");
            builder.Append(reason);
            builder.Append("; buildHash=");
            builder.Append(BuildMetadata.BuildHash);
            builder.Append("; sessionId=");
            builder.Append(SanitizeToken(DiagnosticLog.SessionId));
            builder.Append("; role=");
            builder.Append(SanitizeToken(HarmonyDiagnostics.GetTelemetryNetworkRole()));
            builder.Append("; scene=");
            builder.Append(SanitizeToken(SceneManager.GetActiveScene().name));
            builder.Append("; frameCount=");
            builder.Append(_frameCount);
            builder.Append("; frameAvgMs=");
            builder.Append(frameAverage.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append("; frameP50Ms=");
            builder.Append(GetFramePercentile(0.50).ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append("; frameP95Ms=");
            builder.Append(GetFramePercentile(0.95).ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append("; frameP99Ms=");
            builder.Append(GetFramePercentile(0.99).ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append("; frameHist=");
            AppendFrameHistogram(builder);

            AppendMetric(builder, PerformanceMetric.AcidAuthority, "acidAuthority");
            AppendMetric(builder, PerformanceMetric.AcidPoolRefresh, "acidPoolRefresh");
            AppendMetric(builder, PerformanceMetric.AcidHeroScan, "acidHeroScan");
            AppendMetric(builder, PerformanceMetric.AcidHeroCache, "acidHeroCache");
            AppendMetric(builder, PerformanceMetric.AcidHook, "acidHook");
            AppendMetric(builder, PerformanceMetric.AcidObservation, "acidObservation");
            AppendMetric(builder, PerformanceMetric.EntitySubmit, "entitySubmit");
            AppendMetric(builder, PerformanceMetric.EntityPending, "entityPending");
            AppendMetric(builder, PerformanceMetric.EntityPrune, "entityPrune");
            AppendMetric(builder, PerformanceMetric.TraceBuild, "traceBuild");
            AppendMetric(builder, PerformanceMetric.TraceDedup, "traceDedup");
            AppendMetric(builder, PerformanceMetric.DiagnosticWrite, "diagnosticWrite");
            DiagnosticLog.Performance(builder.ToString());
        }

        private static bool HasMetricData()
        {
            for (var index = 0; index < Calls.Length; index++)
            {
                if (Calls[index] != 0L || Items[index] != 0L || Hits[index] != 0L ||
                    Misses[index] != 0L || TotalTicks[index] != 0L)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AppendMetric(
            StringBuilder builder,
            PerformanceMetric metric,
            string name)
        {
            var index = (int)metric;
            AppendField(builder, name + ".calls", Calls[index].ToString(CultureInfo.InvariantCulture));
            AppendField(builder, name + ".items", Items[index].ToString(CultureInfo.InvariantCulture));
            AppendField(builder, name + ".hits", Hits[index].ToString(CultureInfo.InvariantCulture));
            AppendField(builder, name + ".misses", Misses[index].ToString(CultureInfo.InvariantCulture));
            AppendField(
                builder,
                name + ".totalMs",
                ToMilliseconds(TotalTicks[index]).ToString("0.###", CultureInfo.InvariantCulture));
            AppendField(
                builder,
                name + ".maxMs",
                ToMilliseconds(MaxTicks[index]).ToString("0.###", CultureInfo.InvariantCulture));
        }

        private static void AppendField(StringBuilder builder, string name, string value)
        {
            builder.Append("; ");
            builder.Append(name);
            builder.Append('=');
            builder.Append(value);
        }

        private static void AppendFrameHistogram(StringBuilder builder)
        {
            var wroteBin = false;
            for (var index = 0; index < FrameHistogram.Length; index++)
            {
                if (FrameHistogram[index] == 0L)
                {
                    continue;
                }

                if (wroteBin)
                {
                    builder.Append(',');
                }

                builder.Append(index);
                builder.Append(':');
                builder.Append(FrameHistogram[index]);
                wroteBin = true;
            }
        }

        private static double GetFramePercentile(double percentile)
        {
            if (_frameCount == 0L)
            {
                return 0d;
            }

            var target = (long)global::System.Math.Ceiling(_frameCount * percentile);
            if (target < 1L)
            {
                target = 1L;
            }

            long seen = 0L;
            for (var index = 0; index < FrameHistogram.Length; index++)
            {
                seen += FrameHistogram[index];
                if (seen >= target)
                {
                    return index;
                }
            }

            return FrameHistogramMaxMilliseconds;
        }

        private static double ToMilliseconds(long ticks)
        {
            return ticks * 1000d / Stopwatch.Frequency;
        }

        private static string SanitizeToken(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "unknown";
            }

            var builder = new StringBuilder(value.Length);
            for (var index = 0; index < value.Length; index++)
            {
                var current = value[index];
                builder.Append(char.IsLetterOrDigit(current) || current == '_' ||
                               current == '-' || current == '.' ? current : '_');
            }

            return builder.ToString();
        }
    }
}
