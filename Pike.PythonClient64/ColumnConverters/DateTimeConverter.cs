using System;

namespace Pike.PythonClient64.ColumnConverters
{
    /// <summary>
    /// DateTime values converter
    /// </summary>
    public class DateTimeConverter : IColumnConverter
    {
        /// <summary>
        /// Python datetime start date
        /// </summary>
        public static readonly DateTime NumPyDateTime = new DateTime(1970, 1, 1);

        /// <summary>
        /// Nanoseconds per <see cref="DateTime"/> tick
        /// </summary>
        const long NanosecondsPerTick = 100;

        /// <inheritdoc />
        public string PythonTypeName => "datetime64[ns]";

        /// <inheritdoc />
        public Type TargetType => typeof(DateTime);

        /// <inheritdoc />
        public object[] ConvertValues(object[] pythonValues)
        {
            var result = new object[pythonValues.Length];
            for (var i = 0; i < pythonValues.Length; i++)
                result[i] = ConvertValue(pythonValues[i]);

            return result;
        }

        /// <summary>
        /// Converts numpy datetime64 value (nanoseconds since 1970-01-01) to <see cref="DateTime"/>.
        /// NaT and values out of the <see cref="DateTime"/> range become <see cref="DBNull"/>
        /// </summary>
        /// <param name="value">Value to convert</param>
        /// <returns><see cref="DateTime"/> value or <see cref="DBNull"/></returns>
        static object ConvertValue(object value)
        {
            if (PythonValue.IsNone(value)) return DBNull.Value;
            if (value is DateTime dateTime) return dateTime;
            if (!PythonValue.TryToInt64(value, out var nanoseconds)) return DBNull.Value;
            if (nanoseconds == PythonValue.NaT) return DBNull.Value;

            var ticksSinceEpoch = nanoseconds / NanosecondsPerTick;
            if (ticksSinceEpoch > DateTime.MaxValue.Ticks - NumPyDateTime.Ticks ||
                ticksSinceEpoch < DateTime.MinValue.Ticks - NumPyDateTime.Ticks)
                return DBNull.Value;

            return NumPyDateTime.AddTicks(ticksSinceEpoch);
        }
    }
}

