using Python.Runtime;
using System;
using System.Linq;

namespace Pike.PythonClient64.ColumnConverters
{
    public class LongConverter : IColumnConverter
    {
        public string PythonTypeName => "int64";

        public Type TargetType => typeof(long);

        public object[] ConvertValues(object[] pythonValues)
        {
            return pythonValues.Select(v => (object)((PyInt)v).ToInt64()).ToArray();
        }
    }
}
