using System;
using System.Data;
using System.Data.Common;
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

        /// <inheritdoc />
        /// <summary>
        /// Get or set python connection string
        /// </summary>
        public override string ConnectionString
        {
            get => _builder.ConnectionString;
            // Null or empty string means "no settings"
            set => _builder = string.IsNullOrWhiteSpace(value)
                ? new PythonConnectionStringBuilder()
                : PythonConnectionStringBuilder.Parse(value);
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
        /// Version of <see cref="PythonEngine"/>. Empty string when the connection is closed
        /// </summary>
        public override string ServerVersion => _isOpen ? PythonDataProvider.Version : string.Empty;

        bool _isOpen;

        /// <inheritdoc />
        /// <summary>
        /// Get the current connection state
        /// </summary>
        public override ConnectionState State => _isOpen && PythonDataProvider.State == ConnectionState.Open
            ? ConnectionState.Open
            : ConnectionState.Closed;

        /// <inheritdoc />
        /// <summary>
        /// Opens a database connection with the settings specified by the <see cref="P:Pike.PythonClient64.Data.PythonConnection.ConnectionString" />
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when PYTHONDLL is not set or the engine is initialized with another python*.dll</exception>
        public override void Open()
        {
            if (_isOpen) return;

            _builder.Validate();

            // Engine is a singleton for the process, so it can't be reloaded with another python*.dll
            if (PythonDataProvider.State == ConnectionState.Open &&
                !string.Equals(PythonDataProvider.PythonDllPath, _builder.PythonDll, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Python engine is already initialized with '{PythonDataProvider.PythonDllPath}', " +
                    $"'{_builder.PythonDll}' can't be used until all connections are closed");

            // Set python*.dll path
            PythonDataProvider.PythonDllPath = _builder.PythonDll;

            // Import path components
            PythonDataProvider.Open(_builder.PythonPathComponents);
            _isOpen = true;
        }

        /// <inheritdoc />
        /// <summary>
        /// Closes the connection to the python environment. The shared engine is released, not shut down,
        /// when other connections are still opened
        /// </summary>
        public override void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            PythonDataProvider.Close();
        }

        /// <inheritdoc />
        /// <summary>
        /// Disposable object implementation
        /// </summary>
        /// <param name="disposing">True to release both manage and unmanaged resources; False to release only unmanaged resources</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing) Close();

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
        /// Starts a database transaction. Transactions are not supported
        /// </summary>
        /// <param name="isolationLevel">A <see cref="T:System.Data.IsolationLevel" /> object</param>
        /// <returns>Never returns</returns>
        /// <exception cref="NotSupportedException">Always thrown, transactions are not supported</exception>
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            throw new NotSupportedException("Transactions are not supported by the python provider");
        }

        /// <inheritdoc />
        /// <summary>
        /// Changes the current database for an open connection. Databases are not supported
        /// </summary>
        /// <param name="databaseName">Specifies the name of the database for the connection to use</param>
        /// <exception cref="NotSupportedException">Always thrown, the python provider has no databases</exception>
        public override void ChangeDatabase(string databaseName)
        {
            throw new NotSupportedException("Database can't be changed, the python provider has no databases");
        }

        #endregion
    }
}
