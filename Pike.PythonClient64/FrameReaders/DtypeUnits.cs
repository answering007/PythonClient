using System;

namespace Pike.PythonClient64.FrameReaders
{
    /// <summary>
    /// Kind of the temporal dtype
    /// </summary>
    internal enum TemporalKind
    {
        /// <summary>
        /// Not a temporal dtype
        /// </summary>
        None,

        /// <summary>
        /// Calendar date (polars.Date)
        /// </summary>
        Date,

        /// <summary>
        /// Date and time (pandas datetime64, polars.Datetime)
        /// </summary>
        Datetime,

        /// <summary>
        /// Time span (pandas timedelta64, polars.Duration)
        /// </summary>
        Duration,

        /// <summary>
        /// Time of day (polars.Time)
        /// </summary>
        Time
    }

    /// <summary>
    /// Temporal dtype helpers for the pandas (<c>datetime64[ns, UTC]</c>) and polars
    /// (<c>Datetime(time_unit='us', time_zone=None)</c>) dtype names
    /// </summary>
    internal static class DtypeUnits
    {
        /// <summary>
        /// Nanoseconds per day: polars.Date is stored as days since 1970-01-01
        /// </summary>
        public const long NanosecondsPerDay = 86_400L * 1000 * 1000 * 1000;

        /// <summary>
        /// Gets the temporal kind of the dtype (case-insensitive, dtype parameters are ignored)
        /// </summary>
        /// <param name="dtype">dtype name</param>
        /// <returns>Temporal kind</returns>
        public static TemporalKind GetTemporalKind(string dtype)
        {
            var normalized = SupportedTypes.NormalizeDtype(dtype);

            if (Is(normalized, "Date")) return TemporalKind.Date;
            if (Is(normalized, "Datetime") || Is(normalized, "datetime64") || Is(normalized, "timestamp"))
                return TemporalKind.Datetime;
            if (Is(normalized, "Duration") || Is(normalized, "timedelta64")) return TemporalKind.Duration;
            if (Is(normalized, "Time")) return TemporalKind.Time;

            return TemporalKind.None;
        }

        /// <summary>
        /// Gets the number of nanoseconds per dtype unit
        /// </summary>
        /// <param name="dtype">dtype name</param>
        /// <returns>Nanoseconds per unit, 1 when the dtype has no unit</returns>
        public static long GetNanosecondsPerUnit(string dtype)
        {
            if (GetTemporalKind(dtype) == TemporalKind.Date) return NanosecondsPerDay;

            switch (GetTimeUnit(dtype))
            {
                case "us":
                    return 1000;
                case "ms":
                    return 1000000;
                case "s":
                    return 1000000000;
                case "m":
                    return 60L * 1000000000;
                case "h":
                    return 3600L * 1000000000;
                case "D":
                    return NanosecondsPerDay;
                default:
                    return 1;
            }
        }

        /// <summary>
        /// Gets the time unit of the dtype: polars <c>time_unit='us'</c>, pandas <c>datetime64[us]</c>
        /// </summary>
        /// <param name="dtype">dtype name</param>
        /// <returns>Unit name or null when the dtype has no unit</returns>
        public static string GetTimeUnit(string dtype)
        {
            if (string.IsNullOrEmpty(dtype)) return null;

            var polarsUnit = ExtractValue(dtype, "time_unit=");
            if (polarsUnit != null) return polarsUnit;

            var start = dtype.IndexOf('[');
            if (start < 0) return null;

            var end = dtype.IndexOfAny(new[] { ',', ']' }, start + 1);
            return end < 0 ? null : dtype.Substring(start + 1, end - start - 1).Trim();
        }

        /// <summary>
        /// Reads the keyword value from the dtype text: <c>time_unit='us'</c> → <c>us</c>
        /// </summary>
        /// <param name="dtype">dtype name</param>
        /// <param name="prefix">Keyword with '='</param>
        /// <returns>Value or null when the keyword is not found</returns>
        static string ExtractValue(string dtype, string prefix)
        {
            var start = dtype.IndexOf(prefix, StringComparison.Ordinal);
            if (start < 0) return null;

            start += prefix.Length;
            if (start < dtype.Length && (dtype[start] == '\'' || dtype[start] == '"')) start++;

            var end = dtype.IndexOfAny(new[] { '\'', '"', ',', ')', '}' }, start);
            var value = (end < 0 ? dtype.Substring(start) : dtype.Substring(start, end - start)).Trim();

            return value.Length == 0 ? null : value;
        }

        /// <summary>
        /// Compares the dtype name ignoring the case
        /// </summary>
        static bool Is(string dtype, string expected) =>
            string.Equals(dtype, expected, StringComparison.OrdinalIgnoreCase);
    }
}
