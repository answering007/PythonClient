using System;
using System.Data;
using System.Data.Common;
using System.Threading;
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
        dynamic _sys;

        public PythonConnection()
        {
            ConnectionName = Guid.NewGuid().ToString();
            Logger.Log.Debug("PythonConnection created: " + ConnectionName);
            Logger.Log.Debug("Thread: " + Thread.CurrentThread.ManagedThreadId);
        }

        public string ConnectionName { get; }

        /// <summary>
        /// Python global interpreter lock
        /// </summary>
        public Py.GILState GilState { get; private set; }

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
        
        /// <inheritdoc />
        /// <summary>
        /// Opens a database connection with the settings specified by the <see cref="P:Pike.PythonClient64.Data.PythonConnection.ConnectionString" />
        /// </summary>
        public override void Open()
        {
            try
            {
                Logger.Log.Debug("PythonConnection.Open: " + ConnectionName);
                Logger.Log.Debug("Thread: " + Thread.CurrentThread.ManagedThreadId);
                Logger.Log.Debug("PythonConnection.ConnectionState = " + State);
                if (State == ConnectionState.Open) return;
                
                Logger.Log.Debug("_builder.PythonDll = " + _builder.PythonDll);
                //PythonEngine.Shutdown();
                Logger.Log.Debug("PythonConnection.Open: PythonEngine.Shutdown()");
                Runtime.PythonDLL = _builder.PythonDll;
                Logger.Log.Debug("Runtime.PythonDLL = " + Runtime.PythonDLL);
                PythonEngine.Initialize();
                Logger.Log.Debug("PythonEngine.Initialize();");

                GilState = Py.GIL();
                Logger.Log.Debug("GilState = Py.GIL();");

                // Import path components
                _sys = Py.Import("sys");
                Logger.Log.Debug("_sys = Py.Import(\"sys\");");
                foreach (var pathComponent in _builder.PythonPathComponents)
                    _sys.path.append(pathComponent);

                _state = ConnectionState.Open;
                Logger.Log.Debug("_state = ConnectionState.Open;");
            }
            catch (Exception exception)
            {
                //Logger.Log.Debug("PythonConnection.Open Exception: " + exception.Message + "StackTrace" + exception.StackTrace);
                Logger.Log.Debug("PythonConnection.Open Exception: " + exception);
                throw;
            }
        }

        /// <inheritdoc />
        /// <summary>
        /// Closes the connection to the python environment
        /// </summary>
        public override void Close()
        {
            try
            {
                Logger.Log.Debug("PythonConnection.Close: " + ConnectionName);
                Logger.Log.Debug("Thread: " + Thread.CurrentThread.ManagedThreadId);
                Logger.Log.Debug("PythonConnection.ConnectionState = " + State);
                if (State == ConnectionState.Closed) return;
                
                if (_sys != null)
                {
                    _sys.Dispose();
                    _sys = null;
                    Logger.Log.Debug("_sys.Dispose();");
                }

                if (GilState != null)
                {
                    GilState.Dispose();
                    GilState = null;
                    Logger.Log.Debug("GilState.Dispose();");
                }

                PythonEngine.Shutdown();
                Logger.Log.Debug("PythonEngine.Shutdown();");
                _state = ConnectionState.Closed;
                Logger.Log.Debug("_state = ConnectionState.Closed;");
            }
            catch (Exception exception)
            {
                //Logger.Log.Debug("PythonConnection.Close Exception: " + exception.Message + "StackTrace" + exception.StackTrace);
                Logger.Log.Debug("PythonConnection.Close Exception: " + exception);
                throw;
            }
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
        /// Starts a database transaction. Currently, throw <see cref="T:System.NotImplementedException" />
        /// </summary>
        /// <param name="isolationLevel">A <see cref="T:System.Data.IsolationLevel" /> object</param>
        /// <returns></returns>
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        /// <summary>
        /// Changes the current database for an open connection. Currently, throw <see cref="T:System.NotImplementedException" />
        /// </summary>
        /// <param name="databaseName">Specifies the name of the database for the connection to use</param>
        public override void ChangeDatabase(string databaseName)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
