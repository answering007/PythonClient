using Python.Runtime;
using System;
using System.Collections.Generic;
using System.Data;
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
        /// Python code text
        /// </summary>
        public static string Query { get; set; }

        /// <summary>
        /// Parameters dictionary
        /// </summary>
        public static IDictionary<string, object> Parameters { get; } = new Dictionary<string, object>();

        /// <summary>
        /// True (default) to throw <see cref="NotSupportedException"/> for a column with unknown dtype.
        /// False to convert such columns to string
        /// </summary>
        public static bool StrictColumnTypes { get; set; } = true;

        /// <summary>
        /// Runs the specified Python script and returns result as a DataTable
        /// </summary>
        /// <param name="scriptText">Python script text</param>
        /// <returns>Result as a DataTable</returns>
        /// <exception cref="ArgumentException">If <paramref name="scriptText"/> is null or empty</exception>
        /// <exception cref="InvalidOperationException">If connection is not open</exception>
        /// <exception cref="KeyNotFoundException">If Python script does not assign result to a variable named 'result'</exception>
        public static DataTable RunScript(string scriptText)
        {
            if (string.IsNullOrWhiteSpace(scriptText)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(scriptText));
            if (PythonDataProvider.State != ConnectionState.Open) throw new InvalidOperationException("Connection must be open");

            using (var module = Py.CreateScope())
            {
                using (dynamic variables = module.Variables())
                {
                    if (!string.IsNullOrWhiteSpace(Query))
                        variables[QueryKey] = Query.ToPython();

                    using (var pyDictionary = Parameters.ToPythonDictionary())
                    {
                        variables[PythonParametersKey] = pyDictionary;
                        module.Exec(scriptText);

                        if (!variables.HasKey(ResultKey)) throw new KeyNotFoundException($"Python script must assign result to a variable named '{ResultKey}'");

                        // Result object must be released, otherwise the python object stays alive until GC
                        using (PyObject result = AsPyObject(variables[ResultKey]))
                            return ConvertDataFrameToDataTable(result);
                    }
                }
            }
        }

        /// <summary>
        /// Converts a given key-value pair into a Python-compatible parameter for script execution, 
        /// handling conversion of DateTime objects to NumPy datetime64 format.
        /// </summary>
        /// <param name="parameter">The key-value pair to convert, where the key is a string and the value is an object.</param>
        /// <returns>A key-value pair where the key is the same and the value is a PyObject compatible with Python scripts.</returns>
        /// <exception cref="ArgumentException">Thrown when the key is null or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the value is null.</exception>
        public static KeyValuePair<string, PyObject> ToPythonParameter(this KeyValuePair<string, object> parameter)
        {
            if (string.IsNullOrWhiteSpace(parameter.Key)) throw new ArgumentException("parameter.Key can't be null or empty", nameof(parameter.Key));
            if (parameter.Value == null) throw new ArgumentNullException(nameof(parameter));

            var typeCode = Type.GetTypeCode(parameter.Value.GetType());
            if (typeCode != TypeCode.DateTime) return new KeyValuePair<string, PyObject>(parameter.Key, parameter.Value.ToPython());

            var dt = (DateTime)parameter.Value;
            var ticks = (dt.Ticks - DateTimeConverter.NumPyDateTime.Ticks) * 100;
            using (dynamic module = Py.Import("numpy"))
                return new KeyValuePair<string, PyObject>(parameter.Key, (PyObject)module.datetime64(ticks, "ns"));
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
            foreach (var parameter in parameters)
            {
                var kv = parameter.ToPythonParameter();
                dict.SetItem(kv.Key, kv.Value);
            }
            return dict;
        }

        /// <summary>
        /// Converts a Python Pandas DataFrame to a .NET DataTable.
        /// </summary>
        /// <param name="df">The Pandas DataFrame to convert.</param>
        /// <returns>A .NET DataTable containing the data from the DataFrame.</returns>
        static DataTable ConvertDataFrameToDataTable(PyObject dataFrame)
        {
            if (!dataFrame.HasAttr("columns") || !dataFrame.HasAttr("dtypes"))
            {
                using (PyObject pythonType = dataFrame.GetPythonType())
                    throw new InvalidOperationException(
                        $"'{ResultKey}' must be a pandas.DataFrame, got {pythonType}");
            }

            // Result table
            var dataTable = new DataTable(ResultKey);
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (PyObject columnsObject = dataFrame.GetAttr("columns"))
            using (PyObject columnNames = columnsObject.InvokeMethod("tolist"))
            {
                var columnsCount = (int)columnNames.Length();
                var tableValues = new object[columnsCount][];

                // Define columns and convert data to managed values
                for (var i = 0; i < columnsCount; i++)
                {
                    using (PyObject columnName = columnNames.GetItem(i))
                    using (PyObject series = dataFrame.GetItem(columnName))
                    using (PyObject dtype = series.GetAttr("dtype"))
                    {
                        var managedConverter = GetColumnConverter(columnName, dtype.ToString());

                        // Add columns
                        dataTable.Columns.Add(new DataColumn(MakeColumnName(columnName, i, usedNames))
                        {
                            AllowDBNull = true,
                            DataType = managedConverter.TargetType
                        });

                        // Fill values
                        tableValues[i] = ReadColumnValues(series, managedConverter);
                    }
                }

                FillRows(dataTable, tableValues, columnsCount);
            }

            return dataTable;
        }

        /// <summary>
        /// Gets the converter for the column dtype. Unknown dtype is rejected in strict mode (default)
        /// </summary>
        /// <param name="column">Column name</param>
        /// <param name="dtype">dtype name</param>
        /// <returns>Column converter</returns>
        /// <exception cref="NotSupportedException">Thrown when dtype has no registered converter and strict mode is on</exception>
        static IColumnConverter GetColumnConverter(PyObject column, string dtype)
        {
            if (SupportedTypes.TryGetConverter(dtype, out var converter)) return converter;
            if (StrictColumnTypes)
                throw new NotSupportedException($"Column '{column}' has unsupported dtype '{dtype}'");

            // Fallback for consumers that prefer string conversion over the exception
            return SupportedTypes.GetConverter("object");
        }

        /// <summary>
        /// Makes a unique valid <see cref="DataColumn"/> name from the python column name
        /// </summary>
        /// <param name="column">Python column name</param>
        /// <param name="index">Column index</param>
        /// <param name="usedNames">Names that are already used by the table</param>
        /// <returns>Unique valid column name</returns>
        static string MakeColumnName(PyObject column, int index, ISet<string> usedNames)
        {
            var name = column.ToString();
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
        /// <returns>Array of managed values</returns>
        static object[] ReadColumnValues(PyObject series, IColumnConverter converter)
        {
            // datetime64/timedelta64 must be read as exact nanoseconds, otherwise precision and NaT are lost
            if (converter is DateTimeConverter || converter is TimeSpanConverter)
            {
                try
                {
                    using (PyObject int64Name = new PyString("int64"))
                    using (PyObject nanoseconds = series.InvokeMethod("astype", new[] { int64Name }))
                        return ConvertSeriesValues(nanoseconds, converter);
                }
                catch (PythonException)
                {
                    // tz-aware datetime64[ns, UTC] can't be cast to int64, python datetime objects are used instead
                }
            }

            return ConvertSeriesValues(series, converter);
        }

        /// <summary>
        /// Converts series values with the specified converter
        /// </summary>
        /// <param name="series">Python series (DataFrame column)</param>
        /// <param name="converter">Column converter</param>
        /// <returns>Array of managed values</returns>
        static object[] ConvertSeriesValues(PyObject series, IColumnConverter converter)
        {
            using (PyObject values = series.GetAttr("values"))
            using (PyObject valuesList = values.InvokeMethod("tolist"))
                return converter.ConvertValues((object[])(dynamic)valuesList);
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
        /// Resets the parameters and query strings to their original state.
        /// </summary>
        public static void Reset()
        {
            Parameters.Clear();
            Query = null;
        }
    }
}
