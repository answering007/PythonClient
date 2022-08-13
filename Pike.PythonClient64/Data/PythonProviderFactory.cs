using System.Data.Common;

namespace Pike.PythonClient64.Data
{
    /// <inheritdoc />
    /// <summary>
    /// The <see cref="T:System.Data.Common.DbProviderFactory" /> implementation
    /// </summary>
    public class PythonProviderFactory: DbProviderFactory
    {
        /// <summary>
        /// Instance of <see cref="PythonProviderFactory"/>
        /// </summary>
        public static readonly PythonProviderFactory Instance = new PythonProviderFactory();

        /// <inheritdoc />
        /// <summary>
        /// Specifies whether the specific <see cref="T:Pike.PythonClient64.Data.PythonProviderFactory" /> supports the <see cref="T:System.Data.Common.DbDataSourceEnumerator" /> class. Current value is false
        /// </summary>
        public override bool CanCreateDataSourceEnumerator => false;

        /// <inheritdoc />
        /// <summary>
        /// Returns a new instance of the provider's class that implements the <see cref="T:Pike.PythonClient64.Data.PythonCommand" /> class
        /// </summary>
        /// <returns>A new instance of <see cref="T:Pike.PythonClient64.Data.PythonCommand" /></returns>
        public override DbCommand CreateCommand()
        {
            return new PythonCommand();
        }

        /// <inheritdoc />
        /// <summary>
        /// Returns a new instance of the provider's class that implements the <see cref="T:Pike.PythonClient64.Data.PythonConnection" /> class
        /// </summary>
        /// <returns>A new instance of <see cref="T:Pike.PythonClient64.Data.PythonConnection" /></returns>
        public override DbConnection CreateConnection()
        {
            return new PythonConnection();
        }

        /// <inheritdoc />
        /// <summary>
        /// Returns a new instance of the provider's class that implements the <see cref="T:Pike.PythonClient64.Data.PythonConnectionStringBuilder" /> class
        /// </summary>
        /// <returns>A new instance of <see cref="T:Pike.PythonClient64.Data.PythonConnectionStringBuilder" /></returns>
        public override DbConnectionStringBuilder CreateConnectionStringBuilder()
        {
            return new PythonConnectionStringBuilder();
        }

        /// <inheritdoc />
        /// <summary>
        /// Returns a new instance of the provider's class that implements the <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> class
        /// </summary>
        /// <returns>A new instance of <see cref="T:Pike.PythonClient64.Data.PythonParameter" /></returns>
        public override DbParameter CreateParameter()
        {
            return new PythonParameter();
        }
    }
}