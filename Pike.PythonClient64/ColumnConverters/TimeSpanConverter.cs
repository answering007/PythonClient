using System;

namespace Pike.PythonClient64.ColumnConverters
{
    /// <summary>
    /// TimeSpan values converter
    /// </summary>
    public class TimeSpanConverter : IColumnConverter
    {
        /// <summary>
        /// Nanoseconds per <see cref="TimeSpan"/> tick
        /// </summary>
        const long NanosecondsPerTick = 100;

        /// <inheritdoc />
        public string PythonTypeName => "timedelta64[ns]";

        /// <inheritdoc />
        public Type TargetType => typeof(TimeSpan);

        /// <inheritdoc />
        public object[] ConvertValues(object[] pythonValues)
        {
            var result = new object[pythonValues.Length];
            for (var i = 0; i < pythonValues.Length; i++)
                result[i] = ConvertValue(pythonValues[i]);

            return result;
        }

        /// <summary>
        /// Converts numpy timedelta64 value (nanoseconds) to <see cref="TimeSpan"/>.
        /// NaT and values out of the <see cref="TimeSpan"/> range become <see cref="DBNull"/>
        /// </summary>
        /// <param name="value">Value to convert</param>
        /// <returns><see cref="TimeSpan"/> value or <see cref="DBNull"/></returns>
        static object ConvertValue(object value)
        {
            if (PythonValue.IsNone(value)) return DBNull.Value;
            if (value is TimeSpan timeSpan) return timeSpan;
            if (!PythonValue.TryToInt64(value, out var nanoseconds)) return DBNull.Value;
            if (nanoseconds == PythonValue.NaT) return DBNull.Value;

            var ticks = nanoseconds / NanosecondsPerTick;
            if (ticks > TimeSpan.MaxValue.Ticks || ticks < TimeSpan.MinValue.Ticks)
                return DBNull.Value;

            return TimeSpan.FromTicks(ticks);
        }
    }
}

