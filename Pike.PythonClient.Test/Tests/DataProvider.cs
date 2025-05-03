using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64;
using System.Data;

namespace Pike.PythonClient.Test.Tests
{
    [TestClass]
    public class DataProvider
    {
        /// <summary>
        /// Test that the connection can be opened and closed.
        /// </summary>
        /// <remarks>
        /// Tests that the connection can be opened and closed. This is a basic test
        /// that the connection can be established and is a pre-requisite for all
        /// other tests.
        /// </remarks>
        [TestMethod]
        public void ConnectionCanBeOpenedAndClosed()
        {
            PythonDataProvider.PythonDllPath = SettingsMain.Default.PythonDllPath;
            PythonDataProvider.Open();
            Assert.AreEqual(ConnectionState.Open, PythonDataProvider.State);

            PythonDataProvider.Close();
            Assert.AreEqual(ConnectionState.Closed, PythonDataProvider.State);
        }

        /// <summary>
        /// Test that the connection can be opened and closed multiple times.
        /// </summary>
        /// <remarks>
        /// Tests that the connection can be opened and closed multiple times. This
        /// is a basic test that the connection can be established and is a pre-requisite
        /// for all other tests.
        /// </remarks>
        /// <param name="numberOfRuns">The number of times to run the test.</param>
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
