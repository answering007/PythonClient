using Python.Runtime;
using System;
using System.Linq;

namespace Pike.PythonClient64.ColumnConverters
{
    public class IntegerConverter : IColumnConverter
    {
        public string PythonTypeName => "int32";

        public Type TargetType => typeof(int);

        public object[] ConvertValues(object[] pythonValues)
        {
            return pythonValues.Select(v => (object)((PyInt)v).ToInt32()).ToArray();
        }
    }
}
