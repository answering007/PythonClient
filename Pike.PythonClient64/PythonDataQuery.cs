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
                        return ConvertDataFrameToDataTable(variables[ResultKey]);
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
        static DataTable ConvertDataFrameToDataTable(dynamic df)
        {
            // Result table
            var dataTable = new DataTable(ResultKey);

            // Get column names
            var columns = (PyObject[])df.columns.tolist();

            // Get column types
            var pythonTypes = df.dtypes.to_dict();

            try
            {
                // Get number of rows and create
                var rowsCount = (int)df.shape[0];

                // Managed values
                var tableValues = new object[columns.Length][];

                // Define columns and convert data to managed values
                for (var i = 0; i < columns.Length; i++)
                {
                    // Column name
                    var column = columns[i];

                    // Define managed type
                    string pythonType = pythonTypes[column].ToString();
                    var managedConverter = SupportedTypes.Values.ContainsKey(pythonType) ? SupportedTypes.Values[pythonType] : new StringConverter();

                    // Add columns
                    dataTable.Columns.Add(new DataColumn(column.ToString())
                    {
                        AllowDBNull = true,
                        DataType = managedConverter.TargetType,
                    });

                    // Fill values
                    var pythonValues = (object[])df[column].values.tolist();
                    var values = managedConverter.ConvertValues(pythonValues);

                    tableValues[i] = values;
                }

                // Fill datatable
                for (var i = 0; i < rowsCount; i++)
                {
                    var row = dataTable.NewRow();
                    for (var j = 0; j < columns.Length; j++)
                        row[j] = tableValues[j][i];
                    dataTable.Rows.Add(row);
                }
            }
            finally
            {
                // Dispose types dictionary
                if (pythonTypes != null)
                    pythonTypes.Dispose();

                // Dispose column name objects
                if (columns != null)
                    foreach (var column in columns)
                        column.Dispose();
            }

            return dataTable;
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
