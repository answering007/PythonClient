using System;
using System.Data;
using System.Data.Common;

namespace Pike.PythonClient64.Data
{
    /// <inheritdoc />
    /// <summary>
    /// Python connection with <see cref="T:System.Data.Common.DbConnection" /> implementation
    /// </summary>
    public class PythonConnection: DbConnection
    {
        string _pythonHome;
        string _path;
        string _pythonPath;

        PythonConnectionStringBuilder _builder = new PythonConnectionStringBuilder();

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
        /// Not used
        /// </summary>
        public override string Database => string.Empty;
        
        /// <inheritdoc />
        /// <summary>
        /// Not used
        /// </summary>
        public override string ServerVersion => string.Empty;

        ConnectionState _state = ConnectionState.Closed;
        /// <inheritdoc />
        /// <summary>
        /// Get the current connection state
        /// </summary>
        public override ConnectionState State => _state;

        void BackupAndSetEnvironmentVariables()
        {
            //Backup
            _pythonHome = Environment.GetEnvironmentVariable(PythonConnectionStringBuilder.PythonHomeKey,
                EnvironmentVariableTarget.Process);
            _path = Environment.GetEnvironmentVariable(PythonConnectionStringBuilder.PathKey,
                EnvironmentVariableTarget.Process);
            _pythonPath = Environment.GetEnvironmentVariable(PythonConnectionStringBuilder.PythonPathKey,
                EnvironmentVariableTarget.Process);

            //Set
            Environment.SetEnvironmentVariable(PythonConnectionStringBuilder.PythonHomeKey, _builder.PythonHome, EnvironmentVariableTarget.Process);
            if (_builder.ContainsKey(PythonConnectionStringBuilder.PathKey))
                Environment.SetEnvironmentVariable(PythonConnectionStringBuilder.PathKey, _builder.Path, EnvironmentVariableTarget.Process);
            if (_builder.ContainsKey(PythonConnectionStringBuilder.PythonPathKey))
                Environment.SetEnvironmentVariable(PythonConnectionStringBuilder.PythonPathKey, _builder.PythonPath, EnvironmentVariableTarget.Process);
        }

        void RestoreEnvironmentVariables()
        {
            //Restore
            if (_pythonHome != null)
                Environment.SetEnvironmentVariable(PythonConnectionStringBuilder.PythonHomeKey, _pythonHome, EnvironmentVariableTarget.Process);
            if (_path != null)
                Environment.SetEnvironmentVariable(PythonConnectionStringBuilder.PathKey, _path, EnvironmentVariableTarget.Process);
            if (_pythonPath != null)
                Environment.SetEnvironmentVariable(PythonConnectionStringBuilder.PythonPathKey, _pythonPath, EnvironmentVariableTarget.Process);

            //Reset
            _pythonHome = null;
            _path = null;
            _pythonPath = null;
        }

        /// <inheritdoc />
        /// <summary>
        /// Opens a database connection with the settings specified by the <see cref="P:Pike.PythonClient64.Data.PythonConnection.ConnectionString" />
        /// </summary>
        public override void Open()
        {
            BackupAndSetEnvironmentVariables();
            _state = ConnectionState.Open;
        }

        /// <inheritdoc />
        /// <summary>
        /// Closes the connection to the python environment
        /// </summary>
        public override void Close()
        {
            RestoreEnvironmentVariables();
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
