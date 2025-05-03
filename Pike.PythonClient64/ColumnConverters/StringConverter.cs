using System;
using System.Linq;

namespace Pike.PythonClient64.ColumnConverters
{
    /// <summary>
    /// String values converter
    /// </summary>
    public class StringConverter : IColumnConverter
    {
        /// <inheritdoc />
        public string PythonTypeName => "object";

        /// <inheritdoc />
        public Type TargetType => typeof(string);

        /// <inheritdoc />
        public object[] ConvertValues(object[] pythonValues)
        {
            return pythonValues.Select(StringConverter).ToArray();

            // Values converter
            object StringConverter(object obj)
            {
                if (obj == null) return DBNull.Value;
                return obj.ToString();
            }
        }
    }
}