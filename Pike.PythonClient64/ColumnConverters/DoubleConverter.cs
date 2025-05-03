using System;

namespace Pike.PythonClient64.ColumnConverters
{
    /// <summary>
    /// Double values converter
    /// </summary>
    public class DoubleConverter : IColumnConverter
    {
        /// <inheritdoc />
        public string PythonTypeName => "float64";

        /// <inheritdoc />
        public Type TargetType => typeof(double);

        /// <inheritdoc />
        public object[] ConvertValues(object[] pythonValues)
        {
            var result = new object[pythonValues.Length];
            pythonValues.CopyTo(result, 0);
            return result;
        }
    }
}
