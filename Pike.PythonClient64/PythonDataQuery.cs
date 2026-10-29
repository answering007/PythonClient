using Python.Runtime;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using Pike.PythonClient64.ColumnConverters;

namespace Pike.PythonClient64
{
    /// <summary>
    /// Data query to execute
    /// </summary>
    public static class PythonDataQuery
    {
        /// <summary>
        /// Python 'query' variable name
        /// </summary>
        public const string QueryKey = "query";

        /// <summary>
        /// Python 'result' variable name
        /// </summary>
        public const string ResultKey = "result";

        /// <summary>
        /// Python 'params' variable name
        /// </summary>
        public const string PythonParametersKey = "params";

        /// <summary>
        /// Python code text of the ambient context
        /// </summary>
        [Obsolete("Use PythonScriptContext.Query or PythonCommand.CommandText instead")]
        public static string Query
        {
            get => CurrentContext.Query;
            set => CurrentContext.Query = value;
        }

        /// <summary>
        /// Parameters dictionary of the ambient context
        /// </summary>
        [Obsolete("Use PythonScriptContext.Parameters or PythonCommand.Parameters instead")]
        public static IDictionary<string, object> Parameters => CurrentContext.Parameters;

        static readonly AsyncLocal<PythonScriptContext> AmbientContext = new AsyncLocal<PythonScriptContext>();

        /// <summary>
        /// Context used by the calls without the explicit context
        /// </summary>
        static PythonScriptContext CurrentContext =>
            AmbientContext.Value ?? (AmbientContext.Value = new PythonScriptContext());

        /// <summary>
        /// True (default) to throw <see cref="NotSupportedException"/> for a column with unknown dtype.
        /// False to convert such columns to string
        /// </summary>
        public static bool StrictColumnTypes { get; set; } = true;

        /// <summary>
        /// Runs the specified Python script and returns result as a DataTable.
        /// The ambient context (<see cref="Query"/>, <see cref="Parameters"/>) is used
        /// </summary>
        /// <param name="scriptText">Python script text</param>
        /// <returns>Result as a DataTable</returns>
        /// <exception cref="ArgumentException">If <paramref name="scriptText"/> is null or empty</exception>
        /// <exception cref="InvalidOperationException">If connection is not open</exception>
        /// <exception cref="KeyNotFoundException">If Python script does not assign result to a variable named 'result'</exception>
        public static DataTable RunScript(string scriptText)
        {
            return RunScript(scriptText, null);
        }

        /// <summary>
        /// Runs the specified Python script with the specified execution context and returns result as a DataTable
        /// </summary>
        /// <param name="scriptText">Python script text</param>
        /// <param name="context">Execution context; when null, the ambient context is used</param>
        /// <returns>Result as a DataTable</returns>
        /// <exception cref="ArgumentException">If <paramref name="scriptText"/> is null or empty</exception>
        /// <exception cref="InvalidOperationException">If connection is not open</exception>
        /// <exception cref="KeyNotFoundException">If Python script does not assign the required result variable</exception>
        public static DataTable RunScript(string scriptText, PythonScriptContext context)
        {
            if (string.IsNullOrWhiteSpace(scriptText)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(scriptText));
            if (PythonDataProvider.State != ConnectionState.Open) throw new InvalidOperationException("Connection must be open");

            var executionContext = context ?? AmbientContext.Value ?? new PythonScriptContext();
            var resultName = string.IsNullOrWhiteSpace(executionContext.ResultName)
                ? ResultKey
                : executionContext.ResultName;

            // GIL must be acquired for every python call on the current thread:
            // storing GILState for the whole application corrupts the interpreter in multi-threaded scenarios
            using (Py.GIL())
            using (var module = Py.CreateScope())
            {
                using (dynamic variables = module.Variables())
                {
                    if (!string.IsNullOrWhiteSpace(executionContext.Query))
                        variables[QueryKey] = executionContext.Query.ToPython();

                    using (var pyDictionary = executionContext.Parameters.ToPythonDictionary())
                    {
                        variables[PythonParametersKey] = pyDictionary;
                        module.Exec(scriptText);

                        if (!variables.HasKey(resultName))
                        {
                            if (!executionContext.ResultRequired) return new DataTable(resultName);
                            throw new KeyNotFoundException($"Python script must assign result to a variable named '{resultName}'");
                        }

                        // Result object must be released, otherwise the python object stays alive until GC
                        using (PyObject result = AsPyObject(variables[resultName]))
                            return ConvertDataFrameToDataTable(result, resultName);
                    }
                }
            }
        }

        /// <summary>
        /// Converts a given key-value pair into a Python-compatible parameter for script execution,
        /// handling conversion of DateTime objects to NumPy datetime64 format
        /// </summary>
        /// <param name="parameter">The key-value pair to convert, where the key is a string and the value is an object.</param>
        /// <returns>A key-value pair where the key is the same (without '@'/':' prefix) and the value is a PyObject compatible with Python scripts.</returns>
        /// <exception cref="ArgumentException">Thrown when the key is null or empty.</exception>
        public static KeyValuePair<string, PyObject> ToPythonParameter(this KeyValuePair<string, object> parameter)
        {
            if (string.IsNullOrWhiteSpace(parameter.Key)) throw new ArgumentException("parameter.Key can't be null or empty", nameof(parameter.Key));

            // '@name' and ':name' are the common ADO.NET parameter name syntax
            var name = parameter.Key.TrimStart('@', ':');

            return new KeyValuePair<string, PyObject>(name, ToPythonValue(parameter.Value));
        }

        /// <summary>
        /// Converts the managed value to the python object
        /// </summary>
        /// <param name="value">Value to convert</param>
        /// <returns>Python object</returns>
        static PyObject ToPythonValue(object value)
        {
            switch (value)
            {
                case null:
                case DBNull _:
                    return PyObject.None;
                case DateTime dateTime:
                {
                    // Utc values are transferred as is, local values are converted to UTC
                    var utcDateTime = dateTime.Kind == DateTimeKind.Local ? dateTime.ToUniversalTime() : dateTime;
                    var ticks = (utcDateTime.Ticks - DateTimeConverter.NumPyDateTime.Ticks) * 100;
                    using (dynamic numpyModule = Py.Import("numpy"))
                        return (PyObject)numpyModule.datetime64(ticks, "ns");
                }
                case DateTimeOffset dateTimeOffset:
                    return ToPythonValue(dateTimeOffset.UtcDateTime);
                case TimeSpan timeSpan:
                {
                    var nanoseconds = timeSpan.Ticks * 100;
                    using (dynamic numpyModule = Py.Import("numpy"))
                        return (PyObject)numpyModule.timedelta64(nanoseconds, "ns");
                }
                case Guid guid:
                    return new PyString(guid.ToString());
                case decimal decimalValue:
                    // Python has no decimal type, so the value is transferred as float
                    return new PyFloat((double)decimalValue);
                case string text:
                    return new PyString(text);
                default:
                    return value.ToPython();
            }
        }

        /// <summary>
        /// Converts a dictionary of key-value pairs to a Python dictionary (PyDict) with keys as strings and values as PyObject.
        /// </summary>
        /// <param name="parameters">A dictionary where keys are strings and values are objects to convert.</param>
        /// <returns>A PyDict object compatible with Python scripts.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the parameters dictionary is null.</exception>
        public static PyDict ToPythonDictionary(this IDictionary<string, object> parameters)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            var dict = new PyDict();
            try
            {
                foreach (var parameter in parameters)
                {
                    var kv = parameter.ToPythonParameter();
                    dict.SetItem(kv.Key, kv.Value);
                }

                return dict;
            }
            catch (Exception)
            {
                // Dictionary must not leak if a parameter conversion fails
                dict.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Converts a Python Pandas DataFrame to a .NET DataTable
        /// </summary>
        /// <param name="dataFrame">The Pandas DataFrame to convert</param>
        /// <param name="resultName">Result variable name used as the table name</param>
        /// <returns>A .NET DataTable containing the data from the DataFrame</returns>
        /// <exception cref="InvalidOperationException">Thrown when the result is not a pandas.DataFrame</exception>
        /// <exception cref="NotSupportedException">Thrown when a column dtype is not supported and strict mode is on</exception>
        static DataTable ConvertDataFrameToDataTable(PyObject dataFrame, string resultName)
        {
            if (!dataFrame.HasAttr("columns") || !dataFrame.HasAttr("dtypes"))
            {
                using (PyObject pythonType = dataFrame.GetPythonType())
                    throw new InvalidOperationException(
                        $"'{resultName}' must be a pandas.DataFrame, got {pythonType}");
            }

            // Result table
            var dataTable = new DataTable(resultName);
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (PyObject columnsObject = dataFrame.GetAttr("columns"))
            using (PyObject columnNames = columnsObject.InvokeMethod("tolist"))
            {
                var columnsCount = (int)columnNames.Length();
                var columnNameList = ReadColumnNames(columnNames, columnsCount);
                var tableValues = new object[columnsCount][];

                // Define columns and convert data to managed values
                for (var i = 0; i < columnsCount; i++)
                {
                    // Column is selected by the original label: labels are not always strings (0, 1, tuples...)
                    using (PyObject columnLabel = columnNames.GetItem(i))
                    using (PyObject series = dataFrame.GetItem(columnLabel))
                    using (PyObject dtype = series.GetAttr("dtype"))
                    {
                        var dtypeName = dtype.ToString();
                        var managedConverter = GetColumnConverter(columnNameList[i], dtypeName);

                        // Add columns
                        dataTable.Columns.Add(new DataColumn(MakeColumnName(columnNameList[i], i, usedNames))
                        {
                            AllowDBNull = true,
                            DataType = managedConverter.TargetType
                        });

                        // Fill values
                        tableValues[i] = ReadColumnValues(series, managedConverter, dtypeName);
                    }
                }

                FillRows(dataTable, tableValues, columnsCount);
            }

            return dataTable;
        }

        /// <summary>
        /// Reads column names and rejects the duplicated ones, since duplicated labels make
        /// <c>DataFrame[label]</c> return a DataFrame instead of a Series
        /// </summary>
        /// <param name="columnNames">Python list of column names</param>
        /// <param name="columnsCount">Number of columns</param>
        /// <returns>Column names as strings</returns>
        /// <exception cref="NotSupportedException">Thrown when the result has duplicated column names</exception>
        static string[] ReadColumnNames(PyObject columnNames, int columnsCount)
        {
            var result = new string[columnsCount];
            for (var i = 0; i < columnsCount; i++)
            {
                using (PyObject columnName = columnNames.GetItem(i))
                    result[i] = columnName.ToString();
            }

            var duplicates = result
                .GroupBy(name => name, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();

            if (duplicates.Length > 0)
                throw new NotSupportedException(
                    $"Duplicated column names are not supported: {string.Join(", ", duplicates)}");

            return result;
        }

        /// <summary>
        /// Gets the converter for the column dtype. Unknown dtype is rejected in strict mode (default)
        /// </summary>
        /// <param name="columnName">Column name</param>
        /// <param name="dtype">dtype name</param>
        /// <returns>Column converter</returns>
        /// <exception cref="NotSupportedException">Thrown when dtype has no registered converter and strict mode is on</exception>
        static IColumnConverter GetColumnConverter(string columnName, string dtype)
        {
            if (SupportedTypes.TryGetConverter(dtype, out var converter)) return converter;
            if (StrictColumnTypes)
                throw new NotSupportedException($"Column '{columnName}' has unsupported dtype '{dtype}'");

            // Fallback for consumers that prefer string conversion over the exception
            return SupportedTypes.GetConverter("object");
        }

        /// <summary>
        /// Makes a unique valid <see cref="DataColumn"/> name from the python column name
        /// </summary>
        /// <param name="name">Python column name</param>
        /// <param name="index">Column index</param>
        /// <param name="usedNames">Names that are already used by the table</param>
        /// <returns>Unique valid column name</returns>
        static string MakeColumnName(string name, int index, ISet<string> usedNames)
        {
            if (string.IsNullOrWhiteSpace(name)) name = $"Column{index}";

            // '[', ']' and '.' are not allowed in the DataColumn name
            name = name.Replace("[", "(").Replace("]", ")").Replace(".", "_");

            var uniqueName = name;
            for (var i = 2; !usedNames.Add(uniqueName); i++)
                uniqueName = $"{name}_{i}";

            return uniqueName;
        }

        /// <summary>
        /// Reads and converts column values
        /// </summary>
        /// <param name="series">Python series (DataFrame column)</param>
        /// <param name="converter">Column converter</param>
        /// <param name="dtype">Column dtype name</param>
        /// <returns>Array of managed values</returns>
        static object[] ReadColumnValues(PyObject series, IColumnConverter converter, string dtype)
        {
            // datetime64/timedelta64 are read as exact integers, so NaT and the resolution do not depend on tolist()
            if (converter is DateTimeConverter || converter is TimeSpanConverter)
            {
                var nanosecondsPerUnit = GetNanosecondsPerUnit(dtype);

                try
                {
                    using (PyObject int64Name = new PyString("int64"))
                    using (PyObject int64Series = series.InvokeMethod("astype", new[] { int64Name }))
                        return converter.ConvertValues(
                            ScaleToNanoseconds(ConvertSeriesValues(int64Series), nanosecondsPerUnit));
                }
                catch (PythonException)
                {
                    // tz-aware datetime64[ns, UTC] can't be cast to int64, python datetime objects are used instead
                }
            }

            return converter.ConvertValues(ConvertSeriesValues(series));
        }

        /// <summary>
        /// Converts series values to the array of python objects
        /// </summary>
        /// <param name="series">Python series (DataFrame column)</param>
        /// <returns>Array of python values</returns>
        static object[] ConvertSeriesValues(PyObject series)
        {
            using (PyObject values = series.GetAttr("values"))
            using (PyObject valuesList = values.InvokeMethod("tolist"))
                return (object[])(dynamic)valuesList;
        }

        /// <summary>
        /// Scales integer values to nanoseconds (converter contract), NaT and None are kept as is
        /// </summary>
        /// <param name="values">Values in the dtype resolution</param>
        /// <param name="nanosecondsPerUnit">Nanoseconds per dtype unit</param>
        /// <returns>Values in nanoseconds</returns>
        static object[] ScaleToNanoseconds(object[] values, long nanosecondsPerUnit)
        {
            if (nanosecondsPerUnit == 1) return values;

            var result = new object[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                // Values come from tolist() as python objects, so they are converted before scaling
                if (!PythonValue.TryToInt64(values[i], out var int64Value) ||
                    int64Value == PythonValue.NaT ||
                    int64Value > long.MaxValue / nanosecondsPerUnit ||
                    int64Value < long.MinValue / nanosecondsPerUnit)
                {
                    result[i] = values[i];
                    continue;
                }

                result[i] = int64Value * nanosecondsPerUnit;
            }

            return result;
        }

        /// <summary>
        /// Gets the dtype unit: pandas uses datetime64[ns], datetime64[us] (pandas 3 default) and so on
        /// </summary>
        /// <param name="dtype">Column dtype name</param>
        /// <returns>Unit name, empty string when the dtype has no unit</returns>
        static string GetDtypeUnit(string dtype)
        {
            if (string.IsNullOrEmpty(dtype)) return string.Empty;

            var start = dtype.IndexOf('[');
            if (start < 0) return string.Empty;

            var end = dtype.IndexOfAny(new[] { ',', ']' }, start + 1);
            if (end < 0) return string.Empty;

            return dtype.Substring(start + 1, end - start - 1).Trim();
        }

        /// <summary>
        /// Gets the number of nanoseconds per dtype unit
        /// </summary>
        /// <param name="dtype">Column dtype name</param>
        /// <returns>Nanoseconds per unit, 1 when the unit is unknown</returns>
        static long GetNanosecondsPerUnit(string dtype)
        {
            switch (GetDtypeUnit(dtype))
            {
                case "ns":
                    return 1;
                case "us":
                    return 1000;
                case "ms":
                    return 1000000;
                case "s":
                    return 1000000000;
                case "m":
                    return 60L * 1000000000;
                case "h":
                    return 3600L * 1000000000;
                case "D":
                    return 24L * 3600 * 1000000000;
                default:
                    return 1;
            }
        }

        /// <summary>
        /// Fills the table with converted values
        /// </summary>
        /// <param name="dataTable">Target table</param>
        /// <param name="tableValues">Converted column values</param>
        /// <param name="columnsCount">Number of columns</param>
        static void FillRows(DataTable dataTable, object[][] tableValues, int columnsCount)
        {
            var rowsCount = columnsCount == 0 ? 0 : tableValues[0].Length;
            foreach (var columnValues in tableValues)
                rowsCount = Math.Min(rowsCount, columnValues.Length);

            dataTable.BeginLoadData();
            try
            {
                for (var rowIndex = 0; rowIndex < rowsCount; rowIndex++)
                {
                    var rowValues = new object[columnsCount];
                    for (var columnIndex = 0; columnIndex < columnsCount; columnIndex++)
                        rowValues[columnIndex] = tableValues[columnIndex][rowIndex];

                    dataTable.Rows.Add(rowValues);
                }
            }
            finally
            {
                dataTable.EndLoadData();
            }
        }

        /// <summary>
        /// Casts runtime python object to <see cref="PyObject"/>
        /// </summary>
        /// <param name="value">Value to cast</param>
        /// <returns><see cref="PyObject"/> instance</returns>
        static PyObject AsPyObject(object value)
        {
            return value as PyObject ?? throw new InvalidOperationException($"Unexpected result type: {value?.GetType()}");
        }

        /// <summary>
        /// Resets the ambient context: query text, parameters and result settings
        /// </summary>
        [Obsolete("Use PythonScriptContext instead")]
        public static void Reset()
        {
            var context = CurrentContext;
            context.Query = null;
            context.Parameters.Clear();
            context.ResultName = ResultKey;
            context.ResultRequired = true;
        }
    }
}
