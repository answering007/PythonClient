using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using Python.Runtime;

namespace Pike.PythonClient64.Data
{
    /// <inheritdoc />
    /// <summary>
    /// Python command implementation based on <see cref="DbCommand"/>
    /// </summary>
    public class PythonCommand: DbCommand
    {
        const string QueryKey = "query";
        const string ResultKey = "result";

        /// <inheritdoc />
        /// <summary>
        /// Gets or sets the text command to run against the data source
        /// </summary>
        public override string CommandText { get; set; } = string.Empty;
        /// <inheritdoc />
        /// <summary>
        /// Gets or sets the wait time in seconds before terminating the attempt to execute a command and generating an error. Default value is 0
        /// </summary>
        public override int CommandTimeout { get; set; } = 0;
        /// <inheritdoc />
        /// <summary>
        /// Indicates or specifies how the <see cref="P:Pike.PythonClient64.Data.PythonCommand.CommandText" /> property is interpreted. Only CommandType.Text is supported
        /// </summary>
        public override CommandType CommandType
        {
            get => CommandType.Text;
            set { if (value != CommandType.Text) throw new NotSupportedException(); }
        }
        /// <inheritdoc />
        /// <summary>
        /// Gets or sets a value indicating whether the command object should be visible in a customized interface control
        /// </summary>
        public override bool DesignTimeVisible { get; set; } = true;
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
                if (value == null) throw new ArgumentNullException(nameof(value));
                if (value is PythonConnection connection)
                    _pythonConnection = connection;
                else
                    throw new ArgumentException($"Connection of type {value.GetType()} is not supported", nameof(value));
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
        /// Attempts to cancel the execution of a <see cref="PythonCommand"/>. Currently not implemented
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

        void MarshalParameters(PyDict pyDict, PythonParameterCollection parameters)
        {
            using (var utilities = new PythonUtils())
            {
                foreach (var parameter in parameters.Values)
                {
                    switch (parameter.DbType)
                    {
                        case DbType.Boolean:
                            pyDict.SetItem(parameter.ParameterName, utilities.GetBool((bool)parameter.Value));
                            break;
                        case DbType.DateTime:
                            pyDict.SetItem(parameter.ParameterName, utilities.GetDateTime((DateTime)parameter.Value));
                            break;
                        case DbType.Double:
                            pyDict.SetItem(parameter.ParameterName, new PyFloat((double)parameter.Value));
                            break;
                        case DbType.Int32:
                            pyDict.SetItem(parameter.ParameterName, new PyInt((int)parameter.Value));
                            break;
                        case DbType.Int64:
                            pyDict.SetItem(parameter.ParameterName, new PyLong((long)parameter.Value));
                            break;
                        case DbType.String:
                            pyDict.SetItem(parameter.ParameterName, new PyString((string)parameter.Value));
                            break;
                        default:
                            throw new SystemException("Unknown data type");
                    }
                }
            }
        }

        /// <inheritdoc />
        /// <summary>
        /// Executes the command text against the connection
        /// </summary>
        /// <param name="behavior">Value of <see cref="T:System.Data.CommandBehavior" /> type</param>
        /// <returns>A <see cref="T:System.Data.DataTableReader" /> object</returns>
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            if (DbConnection == null) throw new InvalidOperationException("DbConnection can't be null");
            if (_pythonConnection.State != ConnectionState.Open) throw new InvalidOperationException("Connection must be open");

            var scriptText = CommandText;
            if (!_pythonConnection.UseQueryAsScript)
            {
                if (!File.Exists(_pythonConnection.DataSource)) throw new FileNotFoundException("Can't find python script file", _pythonConnection.DataSource);

                scriptText = File.ReadAllText(_pythonConnection.DataSource ?? throw new InvalidOperationException());
            }
            if (string.IsNullOrWhiteSpace(scriptText)) throw new InvalidOperationException("Python script can't be null or empty");

            return FillDataTable(scriptText, _pythonConnection.UseQueryAsScript).CreateDataReader();
        }

        DataTable FillDataTable(string scriptText, bool useQueryAsScript)
        {
            using (var variables = _pythonConnection.Scope.Variables())
            {
                if (!useQueryAsScript)
                    variables[QueryKey] = new PyString(CommandText);

                var parameters = (PythonParameterCollection)DbParameterCollection;

                using (var pyDictionary = new PyDict())
                {
                    MarshalParameters(pyDictionary, parameters);
                    variables[PythonParameterCollection.PythonName] = pyDictionary;
                    _pythonConnection.Scope.Exec(scriptText);

                    if (!variables.HasKey(ResultKey)) throw new KeyNotFoundException($"Unable to found [{ResultKey}] variable");

                    var dataFrame = variables[ResultKey];
                    return PythonUtils.DeserializeTable(dataFrame);
                }
            }
        }

        /// <inheritdoc />
        /// <summary>
        /// Executes a statement against a connection object
        /// </summary>
        /// <returns>The number of rows affected</returns>
        public override int ExecuteNonQuery()
        {
            using (var reader = ExecuteReader())
            {
                if (!reader.HasRows) return 0;

                var count = 0;
                while (reader.Read())
                    count++;
                return count;
            }
        }

        /// <inheritdoc />
        /// <summary>
        /// Executes the query and returns the first column of the first row in the result set returned by the query. All other columns and rows are ignored
        /// </summary>
        /// <returns>The first column of the first row in the result set</returns>
        public override object ExecuteScalar()
        {
            using (var reader = ExecuteReader())
            {
                if (!reader.HasRows) return null;

                reader.Read();
                return reader[0];
            }
        }
    }
}