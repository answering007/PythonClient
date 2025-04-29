using Pike.PythonClient64.ColumnConverters;
using System.Collections.Generic;
using System.Linq;

namespace Pike.PythonClient64
{
    public class SupportedTypes
    {
        static IDictionary<string, IColumnConverter> _supportedTypes;
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
