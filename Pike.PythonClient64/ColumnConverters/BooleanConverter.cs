using System;

namespace Pike.PythonClient64.ColumnConverters
{
    /// <summary>
    /// Boolean values converter
    /// </summary>
    public class BooleanConverter : IColumnConverter
    {
        /// <inheritdoc />
        public string PythonTypeName => "bool";

        /// <inheritdoc />
        public Type TargetType => typeof(bool);

        /// <inheritdoc />
        public object[] ConvertValues(object[] pythonValues)
        {
            var result = new object[pythonValues.Length];
            pythonValues.CopyTo(result, 0);
            return result;
        }
    }
}
