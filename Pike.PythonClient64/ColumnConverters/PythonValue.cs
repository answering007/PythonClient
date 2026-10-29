using System;
using Python.Runtime;

namespace Pike.PythonClient64.ColumnConverters
{
    /// <summary>
    /// Helpers to convert raw python/numpy values to managed values.
    /// Tolerates <see cref="PyObject"/>, python int, managed numbers, <see cref="DateTime"/> and <see cref="TimeSpan"/>
    /// </summary>
    static class PythonValue
    {
        /// <summary>
        /// pandas NaT marker (numpy int64 min value) used by datetime64/timedelta64 columns
        /// </summary>
        public const long NaT = long.MinValue;

        /// <summary>
        /// Determines whether the value represents a missing value: null, <see cref="DBNull"/>,
        /// python None, pandas NA or pandas NaT
        /// </summary>
        /// <param name="value">Value to check</param>
        /// <returns>True if the value is missing</returns>
        public static bool IsNone(object value)
        {
            if (value == null || value == DBNull.Value) return true;

            var pyObject = value as PyObject;
            if (pyObject == null) return false;
            if (pyObject.IsNone()) return true;

            // pandas NA/NaT singletons have no NoneType, but their repr is stable
            var text = pyObject.ToString();
            return text == "<NA>" || text == "NaT";
        }

        /// <summary>
        /// Converts the value to <see cref="long"/>
        /// </summary>
        /// <param name="value">Value to convert</param>
        /// <param name="result">Converted value</param>
        /// <returns>True if the value has been converted</returns>
        public static bool TryToInt64(object value, out long result)
        {
            result = 0;
            if (IsNone(value) || value is bool) return false;

            switch (value)
            {
                case long longValue:
                    result = longValue;
                    return true;
                case int intValue:
                    result = intValue;
                    return true;
                case short shortValue:
                    result = shortValue;
                    return true;
                case byte byteValue:
                    result = byteValue;
                    return true;
                case sbyte sbyteValue:
                    result = sbyteValue;
                    return true;
                case ushort ushortValue:
                    result = ushortValue;
                    return true;
                case uint uintValue:
                    result = uintValue;
                    return true;
                case ulong ulongValue when ulongValue <= long.MaxValue:
                    result = (long)ulongValue;
                    return true;
                case DateTime dateTime:
                    result = dateTime.Ticks;
                    return true;
                case TimeSpan timeSpan:
                    result = timeSpan.Ticks;
                    return true;
                case PyInt pyInt:
                    return TryConvert(pyInt.ToInt64, out result);
                case PyObject pyObject:
                    return TryConvert(() => pyObject.As<long>(), out result);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Converts the value to <see cref="double"/>
        /// </summary>
        /// <param name="value">Value to convert</param>
        /// <param name="result">Converted value</param>
        /// <returns>True if the value has been converted</returns>
        public static bool TryToDouble(object value, out double result)
        {
            result = double.NaN;
            if (IsNone(value)) return false;

            switch (value)
            {
                case double doubleValue:
                    result = doubleValue;
                    return true;
                case float floatValue:
                    result = floatValue;
                    return true;
                case decimal decimalValue:
                    result = (double)decimalValue;
                    return true;
                case long longValue:
                    result = longValue;
                    return true;
                case int intValue:
                    result = intValue;
                    return true;
                case short shortValue:
                    result = shortValue;
                    return true;
                case byte byteValue:
                    result = byteValue;
                    return true;
                case PyObject pyObject:
                    return TryConvert(() => pyObject.As<double>(), out result);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Converts the value to <see cref="bool"/>
        /// </summary>
        /// <param name="value">Value to convert</param>
        /// <param name="result">Converted value</param>
        /// <returns>True if the value has been converted</returns>
        public static bool TryToBoolean(object value, out bool result)
        {
            result = false;
            if (IsNone(value)) return false;

            switch (value)
            {
                case bool boolValue:
                    result = boolValue;
                    return true;
                case PyObject pyObject:
                    return TryConvert(() => pyObject.As<bool>(), out result);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Converts the value to <see cref="string"/>, missing values become <see cref="DBNull"/>
        /// </summary>
        /// <param name="value">Value to convert</param>
        /// <returns>Managed string or <see cref="DBNull"/></returns>
        public static object ToManagedString(object value)
        {
            if (IsNone(value)) return DBNull.Value;
            if (value is string text) return text;

            return value.ToString();
        }

        static bool TryConvert(Func<long> converter, out long result)
        {
            result = 0;
            try
            {
                result = converter();
                return true;
            }
            catch (Exception)
            {
                // Value has no integer representation
                return false;
            }
        }

        static bool TryConvert(Func<double> converter, out double result)
        {
            result = double.NaN;
            try
            {
                result = converter();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        static bool TryConvert(Func<bool> converter, out bool result)
        {
            result = false;
            try
            {
                result = converter();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
