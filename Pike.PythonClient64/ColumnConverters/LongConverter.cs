using System;

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
            var result = new object[pythonValues.Length];
            for (var i = 0; i < pythonValues.Length; i++)
                result[i] = ConvertValue(pythonValues[i]);

            return result;
        }

        /// <summary>
        /// Converts value to <see cref="long"/>, missing values become <see cref="DBNull"/>
        /// </summary>
        /// <param name="value">Value to convert</param>
        /// <returns><see cref="long"/> value or <see cref="DBNull"/></returns>
        static object ConvertValue(object value)
        {
            return PythonValue.TryToInt64(value, out var int64Value) ? (object)int64Value : DBNull.Value;
        }
    }
}
