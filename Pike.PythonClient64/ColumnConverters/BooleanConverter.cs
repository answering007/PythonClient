using System;

namespace Pike.PythonClient64.ColumnConverters
{
    public class BooleanConverter : IColumnConverter
    {
        public string PythonTypeName => "bool";

        public Type TargetType => typeof(bool);

        public object[] ConvertValues(object[] pythonValues)
        {
            var result = new object[pythonValues.Length];
            pythonValues.CopyTo(result, 0);
            return result;
        }
    }
}
