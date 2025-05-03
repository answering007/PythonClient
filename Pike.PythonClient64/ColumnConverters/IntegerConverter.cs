using Python.Runtime;
using System;
using System.Linq;

namespace Pike.PythonClient64.ColumnConverters
{
    /// <summary>
    /// Integer values converter
    /// </summary>
    public class IntegerConverter : IColumnConverter
    {
        /// <inheritdoc />
        public string PythonTypeName => "int32";

        /// <inheritdoc />
        public Type TargetType => typeof(int);

        /// <inheritdoc />
        public object[] ConvertValues(object[] pythonValues)
        {
            return pythonValues.Select(v => (object)((PyInt)v).ToInt32()).ToArray();
        }
    }
}
