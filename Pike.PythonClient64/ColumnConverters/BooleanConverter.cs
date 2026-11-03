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
            for (var i = 0; i < pythonValues.Length; i++)
                result[i] = ConvertValue(pythonValues[i]);

            return result;
        }

        /// <summary>
        /// Converts value to <see cref="bool"/>, missing values become <see cref="DBNull"/>
        /// </summary>
        /// <param name="value">Value to convert</param>
        /// <returns><see cref="bool"/> value or <see cref="DBNull"/></returns>
        static object ConvertValue(object value)
        {
            return PythonValue.TryToBoolean(value, out var boolValue) ? (object)boolValue : DBNull.Value;
        }
    }
}
