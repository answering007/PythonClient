using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64.Data;

namespace Pike.PythonClient.Test.Tests
{
    [TestClass]
    public class Connection
    {
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
