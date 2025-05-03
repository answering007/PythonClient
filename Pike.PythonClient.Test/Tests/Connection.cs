using System.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64.Data;

namespace Pike.PythonClient.Test.Tests
{
    [TestClass]
    public class Connection
    {
        /// <summary>
        /// Test whether the connection can be opened and closed.
        /// </summary>
        /// <remarks>
        /// Tests that the connection can be opened and closed. This is a basic test
        /// that the connection can be established and is a pre-requisite for all
        /// other tests.
        /// </remarks>
        [TestMethod]
        public void ConnectionCanBeOpenedAndClosed()
        {
            var builder = new PythonConnectionStringBuilder { PythonDll = SettingsMain.Default.PythonDllPath };
            bool isOpened;
            using (var connection = new PythonConnection())
            {
                connection.ConnectionString = builder.ConnectionString;
                connection.Open();
                isOpened = connection.State == ConnectionState.Open;
            }
            Assert.AreEqual(true, isOpened);
        }
    }
}
