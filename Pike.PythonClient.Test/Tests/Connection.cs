using System;
using System.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64.Data;

namespace Pike.PythonClient.Test.Tests
{
    /// <summary>
    /// Connection tests. Require the python runtime (PYTHONNET_TEST_DLL)
    /// </summary>
    [TestClass]
    [TestCategory("Python")]
    public class Connection
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
        /// Connection can be opened and closed
        /// </summary>
        [TestMethod]
        public void ConnectionCanBeOpenedAndClosed()
        {
            using (var connection = new PythonConnection { ConnectionString = TestEnvironment.CreateBuilder().ConnectionString })
            {
                connection.Open();
                Assert.AreEqual(ConnectionState.Open, connection.State);
                Assert.IsFalse(string.IsNullOrEmpty(connection.ServerVersion));

                connection.Close();
                Assert.AreEqual(ConnectionState.Closed, connection.State);
                Assert.AreEqual(string.Empty, connection.ServerVersion);
            }
        }

        /// <summary>
        /// Connection string is preserved after the round trip (P0-1)
        /// </summary>
        [TestMethod]
        public void ConnectionStringIsPreserved()
        {
            var builder = TestEnvironment.CreateBuilder();
            using (var connection = new PythonConnection { ConnectionString = builder.ConnectionString })
            {
                var parsed = PythonConnectionStringBuilder.Parse(connection.ConnectionString);
                Assert.AreEqual(builder.PythonDll, parsed.PythonDll);
            }
        }

        /// <summary>
        /// Two connections share the engine, closing one keeps the second usable (P0-7)
        /// </summary>
        [TestMethod]
        public void SecondConnectionKeepsWorkingAfterFirstIsClosed()
        {
            var first = TestEnvironment.CreateConnection();
            var second = TestEnvironment.CreateConnection();

            first.Close();

            using (var command = second.CreateCommand())
            {
                command.CommandText = "import pandas as pd\nresult = pd.DataFrame({'Value': [7]})";

                var table = new DataTable();
                using (var reader = command.ExecuteReader()) table.Load(reader);

                Assert.AreEqual(7L, Convert.ToInt64(table.Rows[0][0]));
            }

            second.Close();
        }
    }
}
