using Python.Runtime;
using System;
using System.Linq;

namespace Pike.PythonClient64.ColumnConverters
{
    /// <summary>
    /// Long values converter
    /// </summary>
    public class LongConverter : IColumnConverter
    {
        /// <inheritdoc />
        public string PythonTypeName => "int64";

        /// <inheritdoc />
        public Type TargetType => typeof(long);

        /// <inheritdoc />
        public object[] ConvertValues(object[] pythonValues)
        {
            return pythonValues.Select(v => (object)((PyInt)v).ToInt64()).ToArray();
        }
    }
}
