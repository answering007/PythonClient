using System.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64;

namespace Pike.PythonClient.Test.Tests
{
    /// <summary>
    /// <see cref="PythonDataProvider"/> tests. Require the python runtime (PYTHONNET_TEST_DLL)
    /// </summary>
    [TestClass]
    [TestCategory("Python")]
    public class DataProvider
    {
        /// <summary>
        /// Skips the tests when the python runtime is not configured
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestEnvironment.SkipIfNoPython();
        }

        /// <summary>
        /// Restores the clean engine state
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            TestEnvironment.ShutdownIfOwned();
        }

        /// <summary>
        /// Provider can be opened and closed
        /// </summary>
        [TestMethod]
        public void ConnectionCanBeOpenedAndClosed()
        {
            PythonDataProvider.PythonDllPath = TestEnvironment.PythonDll;
            PythonDataProvider.Open();
            Assert.AreEqual(ConnectionState.Open, PythonDataProvider.State);
            Assert.IsFalse(string.IsNullOrEmpty(PythonDataProvider.Version));

            PythonDataProvider.Close();
            Assert.AreEqual(ConnectionState.Closed, PythonDataProvider.State);
            Assert.AreEqual(string.Empty, PythonDataProvider.Version);
        }

        /// <summary>
        /// Engine is reference counted: nested open requires the same number of close calls (P0-7)
        /// </summary>
        /// <param name="numberOfRuns">Number of open/close pairs</param>
        [TestMethod]
        [DataRow(5)]
        public void ConnectionCanBeOpenedAndClosedMultipleTimes(int numberOfRuns)
        {
            for (var i = 0; i < numberOfRuns; i++)
            {
                PythonDataProvider.PythonDllPath = TestEnvironment.PythonDll;
                PythonDataProvider.Open();
                Assert.AreEqual(ConnectionState.Open, PythonDataProvider.State);

                PythonDataProvider.Close();
                Assert.AreEqual(ConnectionState.Closed, PythonDataProvider.State);
            }
        }

        /// <summary>
        /// Nested open keeps the engine alive until the last close (P0-7)
        /// </summary>
        [TestMethod]
        public void NestedOpenKeepsEngineAlive()
        {
            PythonDataProvider.PythonDllPath = TestEnvironment.PythonDll;
            PythonDataProvider.Open();
            PythonDataProvider.Open();

            PythonDataProvider.Close();
            Assert.AreEqual(ConnectionState.Open, PythonDataProvider.State);

            PythonDataProvider.Close();
            Assert.AreEqual(ConnectionState.Closed, PythonDataProvider.State);
        }

        /// <summary>
        /// Default path components are directories that exist (P1-4)
        /// </summary>
        [TestMethod]
        public void DefaultPathComponentsExist()
        {
            PythonDataProvider.PythonDllPath = TestEnvironment.PythonDll;

            foreach (var pathComponent in PythonDataProvider.DefaultPathComponents)
                Assert.IsTrue(System.IO.Directory.Exists(pathComponent), $"Directory '{pathComponent}' does not exist");
        }
    }
}
