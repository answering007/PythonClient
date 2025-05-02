using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64;
using System.Data;

namespace Pike.PythonClient.Test.Tests
{
    [TestClass]
    public class DataProvider
    {
        [TestMethod]
        public void ConnectionCanBeOpenedAndClosed()
        {
            PythonDataProvider.PythonDllPath = SettingsMain.Default.PythonDllPath;
            PythonDataProvider.Open();
            Assert.AreEqual(ConnectionState.Open, PythonDataProvider.State);

            PythonDataProvider.Close();
            Assert.AreEqual(ConnectionState.Closed, PythonDataProvider.State);
        }

        [TestMethod]
        [DataRow(10)]
        public void ConnectionCanBeOpenedAndClosedMultipleTimes(int numberOfRuns)
        {
            for (var i = 0; i < numberOfRuns; i++)
            {
                PythonDataProvider.PythonDllPath = SettingsMain.Default.PythonDllPath;
                PythonDataProvider.Open();
                Assert.AreEqual(ConnectionState.Open, PythonDataProvider.State);

                PythonDataProvider.Close();
                Assert.AreEqual(ConnectionState.Closed, PythonDataProvider.State);
            }
        }
    }
}
