using System;

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
            var result = new object[pythonValues.Length];
            for (var i = 0; i < pythonValues.Length; i++)
                result[i] = ConvertValue(pythonValues[i]);

            return result;
        }

        /// <summary>
        /// Converts value to <see cref="int"/>, missing values and out of range values become <see cref="DBNull"/>
        /// </summary>
        /// <param name="value">Value to convert</param>
        /// <returns><see cref="int"/> value or <see cref="DBNull"/></returns>
        static object ConvertValue(object value)
        {
            if (!PythonValue.TryToInt64(value, out var int64Value)) return DBNull.Value;
            if (int64Value < int.MinValue || int64Value > int.MaxValue) return DBNull.Value;

            return (int)int64Value;
        }
    }
}
