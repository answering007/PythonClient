using System;
using System.Globalization;
using Python.Runtime;

namespace Pike.PythonClient64.ColumnConverters
{
    /// <summary>
    /// Decimal values converter: polars.Decimal (python <see cref="decimal"/> objects) and any text representation.
    /// The value is transferred as <see cref="double"/>, the same way as a decimal parameter is passed to the script
    /// </summary>
    public class DecimalConverter : IColumnConverter
    {
        /// <inheritdoc />
        public string PythonTypeName => "Decimal";

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
        /// Converts value to <see cref="double"/>, missing values and values without a decimal representation
        /// become <see cref="DBNull"/>
        /// </summary>
        /// <param name="value">Value to convert</param>
        /// <returns><see cref="double"/> value or <see cref="DBNull"/></returns>
        static object ConvertValue(object value)
        {
            if (PythonValue.IsNone(value)) return DBNull.Value;

            switch (value)
            {
                case decimal decimalValue:
                    return (double)decimalValue;
                case double doubleValue:
                    return double.IsNaN(doubleValue) ? (object)DBNull.Value : doubleValue;
                case PyObject pyObject:
                    return ConvertText(pyObject.ToString());
                default:
                    return ConvertText(value.ToString());
            }
        }

        /// <summary>
        /// Parses the decimal text with the invariant culture
        /// </summary>
        /// <param name="text">Decimal text, for example '1.23'</param>
        /// <returns><see cref="double"/> value or <see cref="DBNull"/></returns>
        static object ConvertText(string text)
        {
            return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? (object)(double)value
                : DBNull.Value;
        }
    }
}
