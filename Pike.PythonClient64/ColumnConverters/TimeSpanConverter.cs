using Python.Runtime;
using System;
using System.Linq;

namespace Pike.PythonClient64.ColumnConverters
{
    public class TimeSpanConverter : IColumnConverter
    {
        public string PythonTypeName => "timedelta64[ns]";

        public Type TargetType => typeof(TimeSpan);

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
