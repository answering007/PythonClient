using System.Data;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64;

namespace Pike.PythonClient.Test.Tests
{
    /// <summary>
    /// <see cref="PythonDataProvider"/> tests. Require the python runtime (PYTHONNET_TEST_DLL).
    /// The engine is a process-wide singleton, so the tests assert the reference counter transitions
    /// instead of the global connection state
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
        /// Provider can be opened and closed, the reference counter is balanced
        /// </summary>
        [TestMethod]
        public void ConnectionCanBeOpenedAndClosed()
        {
            var baseline = PythonDataProvider.ReferenceCount;
            PythonDataProvider.PythonDllPath = TestEnvironment.PythonDll;

            PythonDataProvider.Open();
            Assert.AreEqual(ConnectionState.Open, PythonDataProvider.State);
            Assert.AreEqual(baseline + 1, PythonDataProvider.ReferenceCount);
            Assert.IsFalse(string.IsNullOrEmpty(PythonDataProvider.Version));

            PythonDataProvider.Close();
            Assert.AreEqual(baseline, PythonDataProvider.ReferenceCount);
        }

        /// <summary>
        /// Engine is reference counted: each open/close pair keeps the counter balanced (P0-7)
        /// </summary>
        /// <param name="numberOfRuns">Number of open/close pairs</param>
        [TestMethod]
        [DataRow(5)]
        public void ConnectionCanBeOpenedAndClosedMultipleTimes(int numberOfRuns)
        {
            var baseline = PythonDataProvider.ReferenceCount;
            PythonDataProvider.PythonDllPath = TestEnvironment.PythonDll;

            for (var i = 0; i < numberOfRuns; i++)
            {
                PythonDataProvider.Open();
                Assert.AreEqual(baseline + 1, PythonDataProvider.ReferenceCount);

                PythonDataProvider.Close();
                Assert.AreEqual(baseline, PythonDataProvider.ReferenceCount);
            }
        }

        /// <summary>
        /// Nested open keeps the engine alive until the last close (P0-7)
        /// </summary>
        [TestMethod]
        public void NestedOpenKeepsEngineAlive()
        {
            var baseline = PythonDataProvider.ReferenceCount;
            PythonDataProvider.PythonDllPath = TestEnvironment.PythonDll;

            PythonDataProvider.Open();
            PythonDataProvider.Open();
            Assert.AreEqual(baseline + 2, PythonDataProvider.ReferenceCount);

            PythonDataProvider.Close();
            Assert.AreEqual(baseline + 1, PythonDataProvider.ReferenceCount);
            Assert.AreEqual(ConnectionState.Open, PythonDataProvider.State);

            PythonDataProvider.Close();
            Assert.AreEqual(baseline, PythonDataProvider.ReferenceCount);
        }

        /// <summary>
        /// Default path components are directories that exist (P1-4)
        /// </summary>
        [TestMethod]
        public void DefaultPathComponentsExist()
        {
            PythonDataProvider.PythonDllPath = TestEnvironment.PythonDll;

            foreach (var pathComponent in PythonDataProvider.DefaultPathComponents)
                Assert.IsTrue(Directory.Exists(pathComponent), $"Directory '{pathComponent}' does not exist");
        }
    }
}
