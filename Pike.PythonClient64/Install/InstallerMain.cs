using System;
using System.Collections;
using System.ComponentModel;
using System.Configuration.Install;

namespace Pike.PythonClient64.Install
{
    /// <inheritdoc />
    /// <summary>
    /// Main installer for Python data providers
    /// </summary>
    [RunInstaller(true)]
    public partial class InstallerMain : Installer
    {
        readonly DbProviderInstallation _dbInstallation= new DbProviderInstallation();

        /// <summary>
        /// Create an instance of <see cref="InstallerMain"/>
        /// </summary>
        public InstallerMain()
        {
            System.Diagnostics.Debugger.Break();
            InitializeComponent();
        }

        /// <inheritdoc />
        /// <summary>
        /// Completes the install transaction
        /// </summary>
        /// <param name="savedState">An <see cref="T:System.Collections.IDictionary" /> that contains the state of the computer after all the installers in the collection have run</param>
        public override void Commit(IDictionary savedState)
        {
            System.Diagnostics.Debugger.Break();

            base.Commit(savedState);
            try
            {
                _dbInstallation.RegisterProvider(DataProviderInstallationBase.Machine64Config);
            }
            catch (Exception exception)
            {
                throw new InstallException("Unable to register .Net providers", exception);
            }
        }

        void Remove()
        {
            _dbInstallation.UnregisterProvider(DataProviderInstallationBase.Machine64Config);
        }

        /// <inheritdoc />
        /// <summary>
        /// Restores the pre-installation state of the computer
        /// </summary>
        /// <param name="savedState">An <see cref="T:System.Collections.IDictionary" /> that contains the state of the computer after all the installers in the collection have run</param>
        public override void Rollback(IDictionary savedState)
        {
            base.Rollback(savedState);
            try
            {
                Remove();
            }
            catch (Exception exception)
            {
                throw new InstallException("Unable to unregister .Net providers", exception);
            }
        }

        /// <inheritdoc />
        /// <summary>
        /// Removes an installation
        /// </summary>
        /// <param name="savedState">An <see cref="T:System.Collections.IDictionary" /> that contains the state of the computer after all the installers in the collection have run</param>
        public override void Uninstall(IDictionary savedState)
        {
            base.Uninstall(savedState);
            try
            {
                Remove();
            }
            catch (Exception exception)
            {
                throw new InstallException("Unable to unregister .Net providers", exception);
            }
        }
    }
}
