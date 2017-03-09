namespace KK.AspNetCore.StaticFileCache
{
    using System;
    using System.Diagnostics;

    internal static class Timing
    {
        private static readonly double TimestampToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;

        public static long GetTimestamp() => Stopwatch.GetTimestamp();

        public static TimeSpan GetDuration(long start) => GetDuration(start, Stopwatch.GetTimestamp());

        public static TimeSpan GetDuration(long start, long end) => new TimeSpan((long)(TimestampToTicks * (end - start)));
    }
}
