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
            for (var i = 0; i < pythonValues.Length; i++)
                result[i] = ConvertValue(pythonValues[i]);

            return result;
        }

        /// <summary>
        /// Converts value to <see cref="double"/>. Missing values and NaN (numpy <c>np.nan</c>) become <see cref="DBNull"/>
        /// </summary>
        /// <param name="value">Value to convert</param>
        /// <returns><see cref="double"/> value or <see cref="DBNull"/></returns>
        static object ConvertValue(object value)
        {
            if (!PythonValue.TryToDouble(value, out var doubleValue)) return DBNull.Value;

            return double.IsNaN(doubleValue) ? (object)DBNull.Value : doubleValue;
        }
    }
}
