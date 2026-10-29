using Pike.PythonClient64.Data;

namespace Pike.PythonClient64.Install
{
    /// <inheritdoc />
    /// <summary>
    /// Python x64 data provider installation information
    /// </summary>
    public class DbProviderInstallation: DataProviderInstallationBase
    {
        /// <summary>
        /// Create an instance of <see cref="DbProviderInstallation"/>
        /// </summary>
        public DbProviderInstallation() : base(typeof(PythonProviderFactory), "Python x64 Data Provider", ".Net Framework Data Provider for Python x64")
        {
        }
    }
}
