using Python.Runtime;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Pike.PythonClient64.ColumnConverters;
using Pike.PythonClient64.FrameReaders;

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
        /// True (default) to throw <see cref="NotSupportedException"/> for a column with unknown dtype.
        /// False to convert such columns to string
        /// </summary>
        public static bool StrictColumnTypes { get; set; } = true;

        /// <summary>
        /// Runs the specified Python script with the default execution context and returns result as a DataTable
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
        /// <param name="context">Execution context; when null, a default context is used</param>
        /// <returns>Result as a DataTable</returns>
        /// <exception cref="ArgumentException">If <paramref name="scriptText"/> is null or empty</exception>
        /// <exception cref="InvalidOperationException">If connection is not open or the result is not a supported frame</exception>
        /// <exception cref="KeyNotFoundException">If Python script does not assign the required result variable</exception>
        public static DataTable RunScript(string scriptText, PythonScriptContext context)
        {
            var executionContext = ResolveContext(context);
            var result = ExecuteScript(scriptText, executionContext, out var resultName);
            if (result == null) return new DataTable(resultName);

            // ConvertResultToDataTable takes the ownership of the result object and disposes it
            return ConvertResultToDataTable(result, resultName);
        }

        /// <summary>
        /// Resolves the execution context: the explicit one or a new default context
        /// </summary>
        /// <param name="context">Explicit context</param>
        /// <returns>Execution context</returns>
        static PythonScriptContext ResolveContext(PythonScriptContext context)
        {
            return context ?? new PythonScriptContext();
        }

        /// <summary>
        /// Executes the script and returns the raw result object
        /// </summary>
        /// <param name="scriptText">Python script text</param>
        /// <param name="executionContext">Execution context</param>
        /// <param name="resultName">Name of the result variable</param>
        /// <returns>Result object or null when it is not required and not defined</returns>
        /// <exception cref="InvalidOperationException">Thrown when the connection is not open</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the script does not assign the result variable</exception>
        static PyObject ExecuteScript(string scriptText, PythonScriptContext executionContext, out string resultName)
        {
            if (string.IsNullOrWhiteSpace(scriptText)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(scriptText));
            if (PythonDataProvider.State != ConnectionState.Open) throw new InvalidOperationException("Connection must be open");

            resultName = string.IsNullOrWhiteSpace(executionContext.ResultName)
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
                            if (executionContext.ResultRequired)
                                throw new KeyNotFoundException(
                                    $"Python script must assign result to a variable named '{resultName}'");

                            return null;
                        }

                        // The wrapper owns its own reference, so it stays valid after the scope is disposed
                        return AsPyObject(variables[resultName]);
                    }
                }
            }
        }

        /// <summary>
        /// Runs the specified Python script asynchronously with the specified execution context and returns result as a DataTable.
        /// The python script itself can't be interrupted, so the work is executed on a thread pool thread
        /// </summary>
        /// <param name="scriptText">Python script text</param>
        /// <param name="context">Execution context; when null, a default context is used</param>
        /// <param name="cancellationToken">Cancellation token, checked before the execution starts</param>
        /// <returns>Result as a DataTable</returns>
        public static Task<DataTable> RunScriptAsync(string scriptText, PythonScriptContext context = null,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() => RunScript(scriptText, context), cancellationToken);
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
        /// Converts the result object to a DataTable. pandas.DataFrame and polars.DataFrame are supported:
        /// the reader is selected by the type of the object, so polars does not have to be converted to pandas
        /// </summary>
        /// <param name="result">Result object. The method takes the ownership and disposes it</param>
        /// <param name="resultName">Result variable name used as the table name</param>
        /// <returns>A .NET DataTable containing the data of the frame</returns>
        /// <exception cref="InvalidOperationException">Thrown when the result is not a supported frame</exception>
        /// <exception cref="NotSupportedException">Thrown when a column dtype is not supported and strict mode is on</exception>
        static DataTable ConvertResultToDataTable(PyObject result, string resultName)
        {
            try
            {
                // The whole frame is read at once, so no python object has to stay alive after the conversion
                using (Py.GIL())
                    return FrameReaderFactory.Read(result, resultName);
            }
            finally
            {
                using (Py.GIL())
                    result.Dispose();
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
    }
}
