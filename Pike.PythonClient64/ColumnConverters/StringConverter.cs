using System;

namespace Pike.PythonClient64.ColumnConverters
{
    /// <summary>
    /// String values converter
    /// </summary>
    public class StringConverter : IColumnConverter
    {
        /// <inheritdoc />
        public string PythonTypeName => "object";

        /// <inheritdoc />
        public Type TargetType => typeof(string);

        /// <inheritdoc />
        public object[] ConvertValues(object[] pythonValues)
        {
            var result = new object[pythonValues.Length];
            for (var i = 0; i < pythonValues.Length; i++)
                result[i] = PythonValue.ToManagedString(pythonValues[i]);

            return result;
        }
    }
}