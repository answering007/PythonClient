using Python.Runtime;
using System;
using System.Linq;

namespace Pike.PythonClient64.ColumnConverters
{
    public class DateTimeConverter : IColumnConverter
    {
        // Python datetime start date
        public static readonly DateTime NumPyDateTime = new DateTime(1970, 1, 1);

        public string PythonTypeName => "datetime64[ns]";

        public Type TargetType => typeof(DateTime);

        public object[] ConvertValues(object[] pythonValues)
        {
            return pythonValues.Select(DateTimeConverter).ToArray();

            // Values converter
            object DateTimeConverter(object obj)
            {
                var value = (PyInt)obj;
                if (value == null) return DBNull.Value;

                var ticks = value.ToInt64() / 100;
                var pyDateTime = new DateTime(ticks);
                return NumPyDateTime.AddTicks(pyDateTime.Ticks);

            }
        }
    }
}
