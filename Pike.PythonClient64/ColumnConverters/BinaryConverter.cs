using System;
using Python.Runtime;

namespace Pike.PythonClient64.ColumnConverters
{
    /// <summary>
    /// Binary (bytes) values converter: polars.Binary and any bytes-like value
    /// </summary>
    public class BinaryConverter : IColumnConverter
    {
        /// <inheritdoc />
        public string PythonTypeName => "Binary";

        /// <inheritdoc />
        public Type TargetType => typeof(byte[]);

        /// <inheritdoc />
        public object[] ConvertValues(object[] pythonValues)
        {
            var result = new object[pythonValues.Length];
            for (var i = 0; i < pythonValues.Length; i++)
                result[i] = ConvertValue(pythonValues[i]);

            return result;
        }

        /// <summary>
        /// Converts value to <see cref="T:byte[]"/>, missing values become <see cref="DBNull"/>
        /// </summary>
        /// <param name="value">Value to convert</param>
        /// <returns><see cref="T:byte[]"/> value or <see cref="DBNull"/></returns>
        static object ConvertValue(object value)
        {
            if (PythonValue.IsNone(value)) return DBNull.Value;

            switch (value)
            {
                case byte[] bytes:
                    return bytes;
                case PyObject pyObject:
                    return TryConvert(pyObject);
                default:
                    return DBNull.Value;
            }
        }

        /// <summary>
        /// Converts the python bytes object to the managed array
        /// </summary>
        /// <param name="value">Python object</param>
        /// <returns><see cref="T:byte[]"/> value or <see cref="DBNull"/></returns>
        static object TryConvert(PyObject value)
        {
            try
            {
                return value.As<byte[]>() ?? (object)DBNull.Value;
            }
            catch (Exception)
            {
                // Not a bytes-like value
                return DBNull.Value;
            }
        }
    }
}
