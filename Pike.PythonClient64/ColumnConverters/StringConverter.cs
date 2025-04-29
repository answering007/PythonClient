using System;
using System.Linq;

namespace Pike.PythonClient64.ColumnConverters
{
    public class StringConverter : IColumnConverter
    {
        public string PythonTypeName => "object";

        public Type TargetType => typeof(string);

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