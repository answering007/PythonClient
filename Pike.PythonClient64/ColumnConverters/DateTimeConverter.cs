using Python.Runtime;
using System;
using System.Linq;

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

        /// <inheritdoc />
        public string PythonTypeName => "datetime64[ns]";

        /// <inheritdoc />
        public Type TargetType => typeof(DateTime);

        /// <inheritdoc />
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
