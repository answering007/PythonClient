using Python.Runtime;
using System;
using System.Linq;

namespace Pike.PythonClient64.ColumnConverters
{
    /// <summary>
    /// TimeSpan values converter
    /// </summary>
    public class TimeSpanConverter : IColumnConverter
    {
        /// <inheritdoc />
        public string PythonTypeName => "timedelta64[ns]";

        /// <inheritdoc />
        public Type TargetType => typeof(TimeSpan);

        /// <inheritdoc />
        public object[] ConvertValues(object[] pythonValues)
        {
            return pythonValues.Select(SpanConverter).ToArray();

            // Values converter
            object SpanConverter(object obj)
            {
                var value = (PyInt)obj;
                if (value == null) return DBNull.Value;

                var ticks = value.ToInt64() / 100;
                return new TimeSpan(ticks);

            }
        }
    }
}
