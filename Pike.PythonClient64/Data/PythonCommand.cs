using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Text;

namespace Pike.PythonClient64.Data
{
    /// <inheritdoc />
    /// <summary>
    /// Python command implementation based on <see cref="DbCommand"/>
    /// </summary>
    public class PythonCommand: DbCommand
    {
        static readonly object ScriptCacheSync = new object();
        static readonly Dictionary<string, KeyValuePair<DateTime, string>> ScriptCache =
            new Dictionary<string, KeyValuePair<DateTime, string>>(StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc />
        /// <summary>
        /// Gets or sets the text command to run against the data source
        /// </summary>
        public override string CommandText { get; set; } = string.Empty;

        /// <inheritdoc />
        /// <summary>
        /// Gets or sets the wait time in seconds before terminating the attempt to execute a command.
        /// Default value is 0. Python scripts can't be interrupted reliably, so a non-zero value is not supported
        /// </summary>
        public override int CommandTimeout { get; set; } = 0;
        
        /// <inheritdoc />
        /// <summary>
        /// Indicates or specifies how the <see cref="P:Pike.PythonClient64.Data.PythonCommand.CommandText" /> property is interpreted. Only CommandType.Text is supported
        /// </summary>
        public override CommandType CommandType
        {
            get => CommandType.Text;
            set { if (value != CommandType.Text) throw new NotSupportedException("Only text command is supported"); }
        }
        
        /// <inheritdoc />
        /// <summary>
        /// Gets or sets a value indicating whether the command object should be visible in a customized interface control.
        /// Default value is false
        /// </summary>
        public override bool DesignTimeVisible { get; set; } = false;
        
        /// <inheritdoc />
        /// <summary>
        /// Gets or sets how command results are applied to the <see cref="T:System.Data.DataRow" /> when used by the Update method of a <see cref="T:System.Data.Common.DbDataAdapter" />. Default value is UpdateRowSource.None
        /// </summary>
        public override UpdateRowSource UpdatedRowSource { get; set; } = UpdateRowSource.None;

        PythonConnection _pythonConnection;
        
        /// <inheritdoc />
        /// <summary>
        /// Gets or sets the <see cref="T:Pike.PythonClient64.Data.PythonConnection" /> used by this <see cref="T:Pike.PythonClient64.Data.PythonCommand" />
        /// </summary>
        protected override DbConnection DbConnection
        {
            get => _pythonConnection;
            set
            {
                switch (value)
                {
                    case null:
                        _pythonConnection = null;
                        break;
                    case PythonConnection connection:
                        _pythonConnection = connection;
                        break;
                    default:
                        throw new ArgumentException($"Connection of type {value.GetType()} is not supported", nameof(value));
                }
            }
        }
        
        /// <inheritdoc />
        /// <summary>
        /// Gets the collection of <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> objects
        /// </summary>
        protected override DbParameterCollection DbParameterCollection { get; } = new PythonParameterCollection();
        
        /// <inheritdoc />
        /// <summary>
        /// Gets or sets the <see cref="P:Pike.PythonClient64.Data.PythonCommand.DbTransaction" /> within which this <see cref="T:System.Data.Common.DbCommand" /> object executes
        /// </summary>
        protected override DbTransaction DbTransaction { get; set; }

        /// <summary>
        /// Creates a prepared (or compiled) version of the command on the data source. Currently not implemented
        /// </summary>
        public override void Prepare()
        {
            
        }
        
        /// <summary>
        /// Attempts to cancel the execution of a <see cref="PythonCommand"/>.
        /// Not supported: python scripts can't be interrupted reliably, so the method does nothing
        /// </summary>
        public override void Cancel()
        {
        }

        /// <summary>
        /// Creates a new instance of a <see cref="PythonParameter"/> object.
        /// </summary>
        /// <returns>A <see cref="PythonParameter"/> object</returns>
        protected override DbParameter CreateDbParameter()
        {
            return new PythonParameter();
        }        

        /// <inheritdoc />
        /// <summary>
        /// Executes the command text against the connection
        /// </summary>
        /// <param name="behavior">Value of <see cref="T:System.Data.CommandBehavior" /> type.
        /// <see cref="CommandBehavior.SchemaOnly"/>, <see cref="CommandBehavior.SingleRow"/> and
        /// <see cref="CommandBehavior.CloseConnection"/> are supported</param>
        /// <returns>A <see cref="T:System.Data.DataTableReader" /> object</returns>
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            return ExecuteScript(behavior).CreateDataReader();
        }

        /// <inheritdoc />
        /// <summary>
        /// Executes a statement against a connection object.
        /// Scripts without the result variable are allowed and give 0
        /// </summary>
        /// <returns>The number of rows affected</returns>
        public override int ExecuteNonQuery()
        {
            var dataTable = ExecuteScript(CommandBehavior.Default, false);
            return dataTable.Rows.Count;
        }

        /// <inheritdoc />
        /// <summary>
        /// Executes the query and returns the first column of the first row in the result set returned by the query. All other columns and rows are ignored
        /// </summary>
        /// <returns>The first column of the first row in the result set</returns>
        public override object ExecuteScalar()
        {
            var dataTable = ExecuteScript(CommandBehavior.SingleRow);
            if (dataTable.Rows.Count == 0 || dataTable.Columns.Count == 0) return null;

            return dataTable.Rows[0][0];
        }

        /// <summary>
        /// Executes the script and returns the result table
        /// </summary>
        /// <param name="behavior">Command behavior</param>
        /// <param name="resultRequired">True to require the result variable</param>
        /// <returns>Result table</returns>
        /// <exception cref="InvalidOperationException">Thrown when the connection is not set or not open</exception>
        /// <exception cref="NotSupportedException">Thrown when a timeout or a transaction is set</exception>
        /// <exception cref="FileNotFoundException">Thrown when the script file is not found</exception>
        DataTable ExecuteScript(CommandBehavior behavior, bool resultRequired = true)
        {
            if (DbConnection == null) throw new InvalidOperationException("DbConnection can't be null");
            if (DbConnection.State != ConnectionState.Open) throw new InvalidOperationException("Connection must be open");
            if (CommandTimeout > 0)
                throw new NotSupportedException(
                    "CommandTimeout is not supported, python scripts can't be interrupted reliably");
            if (DbTransaction != null) throw new NotSupportedException("Transactions are not supported");

            var scriptText = GetScriptText();
            if (string.IsNullOrWhiteSpace(scriptText)) throw new InvalidOperationException("Python script can't be null or empty");

            try
            {
                var dataTable = PythonDataQuery.RunScript(scriptText, CreateContext(resultRequired));

                if ((behavior & CommandBehavior.SchemaOnly) != 0) return dataTable.Clone();

                if ((behavior & CommandBehavior.SingleRow) != 0 && dataTable.Rows.Count > 1)
                {
                    var singleRowTable = dataTable.Clone();
                    singleRowTable.ImportRow(dataTable.Rows[0]);
                    return singleRowTable;
                }

                return dataTable;
            }
            finally
            {
                if ((behavior & CommandBehavior.CloseConnection) != 0) _pythonConnection?.Close();
            }
        }

        /// <summary>
        /// Creates the execution context from the command state
        /// </summary>
        /// <param name="resultRequired">True to require the result variable</param>
        /// <returns>Execution context</returns>
        PythonScriptContext CreateContext(bool resultRequired)
        {
            var context = new PythonScriptContext
            {
                // Query text is passed to the script only when the connection string has no script file
                Query = _pythonConnection.UseQueryAsScript ? CommandText : null,
                ResultRequired = resultRequired
            };

            foreach (var parameter in (PythonParameterCollection)DbParameterCollection)
            {
                var pythonParameter = parameter as PythonParameter;
                if (pythonParameter == null) continue;

                context.Parameters[pythonParameter.ParameterName] = pythonParameter.Value;
            }

            return context;
        }

        /// <summary>
        /// Gets the script text: command text or the script file content.
        /// Files are read as UTF-8 (BOM is detected) and cached by the last write time
        /// </summary>
        /// <returns>Script text</returns>
        /// <exception cref="FileNotFoundException">Thrown when the script file is not found</exception>
        string GetScriptText()
        {
            if (_pythonConnection.UseQueryAsScript) return CommandText;

            var fileName = _pythonConnection.DataSource;
            if (!File.Exists(fileName)) throw new FileNotFoundException("Can't find python script file", fileName);

            var lastWriteTime = File.GetLastWriteTimeUtc(fileName);
            lock (ScriptCacheSync)
            {
                if (ScriptCache.TryGetValue(fileName, out var cached) && cached.Key == lastWriteTime)
                    return cached.Value;

                var scriptText = File.ReadAllText(fileName, Encoding.UTF8);
                ScriptCache[fileName] = new KeyValuePair<DateTime, string>(lastWriteTime, scriptText);
                return scriptText;
            }
        }
    }
}