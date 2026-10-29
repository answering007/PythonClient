using Pike.PythonClient64.ColumnConverters;
using System;
using System.Collections.Generic;

namespace Pike.PythonClient64
{
    /// <summary>
    /// Registry of converters to cast python values to .NET values.
    /// Thread-safe, converters can be extended with <see cref="Register"/>
    /// </summary>
    public static class SupportedTypes
    {
        static readonly object Sync = new object();

        static readonly Lazy<Dictionary<string, IColumnConverter>> Registry =
            new Lazy<Dictionary<string, IColumnConverter>>(CreateDefault, true);

        /// <summary>
        /// Collection with normalized python dtype name as a key and <see cref="IColumnConverter"/> as a value
        /// </summary>
        public static IReadOnlyDictionary<string, IColumnConverter> Values => Registry.Value;

        /// <summary>
        /// Normalizes pandas/numpy dtype name: removes byte order marker and dtype parameters
        /// (<c>datetime64[ns, UTC]</c> → <c>datetime64</c>, <c>string[python]</c> → <c>string</c>)
        /// </summary>
        /// <param name="dtype">dtype name</param>
        /// <returns>Normalized dtype name</returns>
        public static string NormalizeDtype(string dtype)
        {
            if (string.IsNullOrWhiteSpace(dtype)) return string.Empty;

            var normalized = dtype.Trim();

            // numpy byte order marker: '<i8', '>i8', '|O', '=i8'
            if (normalized.Length > 1 &&
                (normalized[0] == '<' || normalized[0] == '>' || normalized[0] == '|' || normalized[0] == '='))
                normalized = normalized.Substring(1);

            // pandas dtype parameters: 'datetime64[ns, UTC]', 'timedelta64[ns]', 'string[python]'
            var bracket = normalized.IndexOf('[');
            if (bracket > 0) normalized = normalized.Substring(0, bracket);

            return normalized.Trim();
        }

        /// <summary>
        /// Gets the converter for the specified dtype
        /// </summary>
        /// <param name="dtype">dtype name</param>
        /// <param name="converter">Found converter</param>
        /// <returns>True if the converter is registered</returns>
        public static bool TryGetConverter(string dtype, out IColumnConverter converter)
        {
            converter = null;
            var normalized = NormalizeDtype(dtype);

            return normalized.Length > 0 && Values.TryGetValue(normalized, out converter);
        }

        /// <summary>
        /// Gets the converter for the specified dtype
        /// </summary>
        /// <param name="dtype">dtype name</param>
        /// <returns>Converter for the specified dtype</returns>
        /// <exception cref="NotSupportedException">Thrown when the dtype has no registered converter</exception>
        public static IColumnConverter GetConverter(string dtype)
        {
            if (TryGetConverter(dtype, out var converter)) return converter;

            throw new NotSupportedException($"Column with dtype '{dtype}' is not supported");
        }

        /// <summary>
        /// Registers (or replaces) the converter for the specified dtype names
        /// </summary>
        /// <param name="converter">Converter to register</param>
        /// <param name="dtypeNames">dtype names; when omitted, <see cref="IColumnConverter.PythonTypeName"/> is used</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="converter"/> is null</exception>
        /// <exception cref="ArgumentException">Thrown when a dtype name is null or empty</exception>
        public static void Register(IColumnConverter converter, params string[] dtypeNames)
        {
            if (converter == null) throw new ArgumentNullException(nameof(converter));

            var names = dtypeNames == null || dtypeNames.Length == 0
                ? new[] { converter.PythonTypeName }
                : dtypeNames;

            lock (Sync)
            {
                foreach (var name in names)
                {
                    var key = NormalizeDtype(name);
                    if (key.Length == 0)
                        throw new ArgumentException("dtype name can't be null or empty", nameof(dtypeNames));

                    Registry.Value[key] = converter;
                }
            }
        }

        /// <summary>
        /// Restores the default converters
        /// </summary>
        public static void Reset()
        {
            lock (Sync)
            {
                var defaults = CreateDefault();
                var registry = Registry.Value;
                registry.Clear();
                foreach (var pair in defaults)
                    registry[pair.Key] = pair.Value;
            }
        }

        static Dictionary<string, IColumnConverter> CreateDefault()
        {
            var boolean = new BooleanConverter();
            var dateTime = new DateTimeConverter();
            var doubleConverter = new DoubleConverter();
            var integer = new IntegerConverter();
            var longConverter = new LongConverter();
            var stringConverter = new StringConverter();
            var timeSpan = new TimeSpanConverter();

            // Keys are case-insensitive: nullable pandas extension dtypes (Int64, UInt32, Float64, boolean)
            // share the converter with their numpy counterparts
            var result = new Dictionary<string, IColumnConverter>(StringComparer.OrdinalIgnoreCase);
            Add(boolean, "bool", "boolean");
            Add(dateTime, "datetime64", "datetime", "timestamp");
            Add(doubleConverter, "float64", "float32", "float", "double");
            Add(integer, "int8", "int16", "int32", "uint8", "uint16", "uint32", "short", "int");
            Add(longConverter, "int64", "uint64", "long");
            Add(timeSpan, "timedelta64", "timedelta");
            Add(stringConverter, "object", "o", "str", "string", "category", "unicode");

            return result;

            void Add(IColumnConverter converter, params string[] dtypeNames)
            {
                foreach (var dtypeName in dtypeNames)
                    result[dtypeName] = converter;
            }
        }
    }
}

