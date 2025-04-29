using System;

namespace Pike.PythonClient64.ColumnConverters
{
    public class DoubleConverter : IColumnConverter
    {
        public string PythonTypeName => "float64";

        public Type TargetType => typeof(double);

        public object[] ConvertValues(object[] pythonValues)
        {
            var result = new object[pythonValues.Length];
            pythonValues.CopyTo(result, 0);
            return result;
        }
    }
}
