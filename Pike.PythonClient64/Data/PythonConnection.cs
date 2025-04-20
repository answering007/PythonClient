using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using Python.Runtime;

namespace Pike.PythonClient64.Data
{
    /// <inheritdoc />
    /// <summary>
    /// Python connection with <see cref="T:System.Data.Common.DbConnection" /> implementation
    /// </summary>
    public class PythonConnection: DbConnection
    {
        PythonConnectionStringBuilder _builder = new PythonConnectionStringBuilder();

        static ReadOnlyDictionary<string, Type> _supportedTypes;
        public static ReadOnlyDictionary<string, Type> SupportedTypes
        {
            get
            {
                if (_supportedTypes == null)
                {
                    IDictionary<string, Type> source = new Dictionary<string, Type>
                    {
                        ["object"] = typeof(string),
                        ["bool"] = typeof(bool),
                        ["float64"] = typeof(double),
                        ["int64"] = typeof(long),
                        ["int32"] = typeof(int),
                        ["timedelta64[ns]"] = typeof(TimeSpan),
                        ["datetime64[ns]"] = typeof(DateTime)
                    };

                    _supportedTypes = new ReadOnlyDictionary<string, Type>(source);
                }
                
                return _supportedTypes;
            }
        }


        /// <summary>
        /// Python global interpreter lock
        /// </summary>
        public Py.GILState GilState { get; private set; }

        /// <summary>
        /// Python scope
        /// </summary>
        public PyModule Module { get; private set; }

        /// <inheritdoc />
        /// <summary>
        /// Get or set python connection string
        /// </summary>
        public override string ConnectionString
        {
            get => _builder.ConnectionString;
            set => _builder = PythonConnectionStringBuilder.Parse(string.IsNullOrWhiteSpace(value) ? string.Empty : value);
        }

        /// <inheritdoc />
        /// <summary>
        /// Get python script full path
        /// </summary>
        public override string DataSource => string.IsNullOrWhiteSpace(_builder.File) ? string.Empty : _builder.File;

        /// <summary>
        /// True if <see cref="DataSource"/> property is not set and should use <see cref="PythonCommand.CommandText"/> as script
        /// </summary>
        public bool UseQueryAsScript => string.IsNullOrWhiteSpace(DataSource);

        /// <inheritdoc />
        /// <summary>
        /// Python database
        /// </summary>
        public override string Database => "Python";

        /// <inheritdoc />
        /// <summary>
        /// Version of <see cref="PythonEngine"/>
        /// </summary>
        public override string ServerVersion => PythonEngine.Version;

        ConnectionState _state = ConnectionState.Closed;
        /// <inheritdoc />
        /// <summary>
        /// Get the current connection state
        /// </summary>
        public override ConnectionState State => _state;

        public static DataTable ExecutePythonScript(string pythonDll, string pythonCode, string pythonPath = null, IDictionary<string, object> parameters = null)
        {
            if (string.IsNullOrWhiteSpace(pythonDll)) throw new ArgumentException($"'{nameof(pythonDll)}' cannot be null or whitespace.", nameof(pythonDll));
            if (!File.Exists(pythonDll)) throw new FileNotFoundException("Can'r find python*.dll file", pythonDll);
            if (string.IsNullOrWhiteSpace(pythonCode)) throw new ArgumentException($"'{nameof(pythonCode)}' cannot be null or whitespace.", nameof(pythonCode));

            // Setup python environment
            Runtime.PythonDLL = pythonDll;
            if (!string.IsNullOrWhiteSpace(pythonPath))
                PythonEngine.PythonPath = pythonPath;
            PythonEngine.Initialize();

            DataTable dataTable;
            using (Py.GIL())
            {
                // Execute the Python script
                dynamic locals = new PyDict();
                PythonEngine.Exec(pythonCode, null, locals);

                // Get the result DataFrame
                dynamic result = locals["result"] ?? throw new InvalidOperationException("Python script must assign result to a variable named 'result'");

                // Convert DataFrame to DataTable
                dataTable = ConvertDataFrameToDataTable(result);
            }
            PythonEngine.Shutdown();

            return dataTable;
        }

        static DataTable ConvertDataFrameToDataTable(dynamic df)
        {
            // Python datetime start date
            DateTime NumPyDateTime = new DateTime(1970, 1, 1);

            // DateTime converter
            Func<object, object> dateTimeConverter = obj =>
            {
                var value = (PyInt)obj;
                if (value != null)
                {
                    var ticks = value.ToInt64() / 100;
                    var pyDateTime = new DateTime(ticks);
                    return NumPyDateTime.AddTicks(pyDateTime.Ticks);
                }
                return DBNull.Value;
            };

            // TimeSpan converter
            Func<object, object> timeSpanConverter = obj =>
            {
                var value = (PyInt)obj;
                if (value != null)
                {
                    var ticks = value.ToInt64() / 100;
                    return new TimeSpan(ticks);
                }
                return DBNull.Value;
            };

            // String converter
            Func<object, object> stringConverter = obj =>
            {
                if (obj == null)
                    return DBNull.Value;
                return obj.ToString();
            };

            // Result table
            var dataTable = new DataTable();

            // Get column names
            var columns = (string[])df.columns.tolist();

            // Get column types
            var pythonTypes = df.dtypes.to_dict();

            // Get number of rows and create
            var rowsCount = (int)df.shape[0];

            // Managed values
            object[][] tableValues = new object[columns.Length][];

            // Define columns and convert data to managed values
            for (int i = 0; i < columns.Length; i++)
            {
                // Column name
                var column = columns[i];
                
                // Define managed type
                var pythonType = pythonTypes[column].ToString();
                Type managedType = SupportedTypes.ContainsKey(pythonType) ? SupportedTypes[pythonType] : typeof(string);

                // Add columns
                dataTable.Columns.Add(new DataColumn(column)
                {
                    AllowDBNull = true,
                    DataType = managedType,
                });

                // Fill values
                var pythonValues = (object[])df[column].values.tolist();
                var values = new object[rowsCount];

                if (managedType == typeof(string))
                {
                    values = pythonValues.Select(v => stringConverter(v)).ToArray();
                }
                else if (managedType == typeof(bool) || managedType == typeof(double))
                {
                    pythonValues.CopyTo(values, 0);
                }
                else if (managedType == typeof(long))
                {
                    values = pythonValues.Select(v => (object)((PyInt)v).ToInt64()).ToArray();
                }
                else if (managedType == typeof(int))
                {
                    values = pythonValues.Select(v => (object)((PyInt)v).ToInt32()).ToArray();
                }
                else if (managedType == typeof(TimeSpan))
                {
                    values = pythonValues.Select(v => timeSpanConverter(v)).ToArray();
                }
                else if (managedType == typeof(DateTime))
                {
                    values = pythonValues.Select(v => dateTimeConverter(v)).ToArray();
                }
                tableValues[i] = values;
            }

            for (int i = 0; i < rowsCount; i++)
            {
                var row = dataTable.NewRow();
                for (int j = 0; j < columns.Length; j++)
                    row[j] = tableValues[j][i];
                dataTable.Rows.Add(row);
            }

            return dataTable;
        }

        /// <inheritdoc />
        /// <summary>
        /// Opens a database connection with the settings specified by the <see cref="P:Pike.PythonClient64.Data.PythonConnection.ConnectionString" />
        /// </summary>
        public override void Open()
        {
            Runtime.PythonDLL = _builder.PythonDll;
            PythonEngine.PythonPath = _builder.PythonPath;
            PythonEngine.Initialize();

            GilState = Py.GIL();
            Module = Py.CreateScope();

            _state = ConnectionState.Open;
        }

        /// <inheritdoc />
        /// <summary>
        /// Closes the connection to the python environment
        /// </summary>
        public override void Close()
        {
            if (Module != null)
            {
                Module.Dispose();
                Module = null;
            }

            if (GilState != null)
            {
                GilState.Dispose();
                GilState = null;
            }

            PythonEngine.Shutdown();
            _state = ConnectionState.Closed;
        }

        /// <inheritdoc />
        /// <summary>
        /// Disposable object implementation
        /// </summary>
        /// <param name="disposing">True to release both manage and unmanaged resources; False to release only unmanaged resources</param>
        protected override void Dispose(bool disposing)
        {
            Close();
            base.Dispose(disposing);
        }

        /// <inheritdoc />
        /// <summary>
        /// Create new <see cref="PythonCommand"/> object
        /// </summary>
        /// <returns></returns>
        protected override DbCommand CreateDbCommand()
        {
            return new PythonCommand {Connection = this};
        }

        #region No need to implement

        /// <inheritdoc />
        /// <summary>
        /// Starts a database transaction. Currently throw <see cref="T:System.NotImplementedException" />
        /// </summary>
        /// <param name="isolationLevel">A <see cref="T:System.Data.IsolationLevel" /> object</param>
        /// <returns></returns>
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        /// <summary>
        /// Changes the current database for an open connection.  Currently throw <see cref="T:System.NotImplementedException" />
        /// </summary>
        /// <param name="databaseName">Specifies the name of the database for the connection to use</param>
        public override void ChangeDatabase(string databaseName)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
