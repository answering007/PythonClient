using Pike.PythonClient64.ColumnConverters;
using System.Collections.Generic;
using System.Linq;

namespace Pike.PythonClient64
{
    /// <summary>
    /// Collection of converters to cast python values to .NET values
    /// </summary>
    public class SupportedTypes
    {
        static IDictionary<string, IColumnConverter> _supportedTypes;

        /// <summary>
        /// Collection with Python type name as a key and <see cref="IColumnConverter"/> as a value
        /// </summary>
        public static IDictionary<string, IColumnConverter> Values
        {
            get
            {
                return _supportedTypes ?? (_supportedTypes = new IColumnConverter[]
                {
                    new BooleanConverter(),
                    new DateTimeConverter(),
                    new DoubleConverter(),
                    new IntegerConverter(),
                    new LongConverter(),
                    new StringConverter(),
                    new TimeSpanConverter()
                }.ToDictionary(k => k.PythonTypeName));
            }
        }
    }
}
