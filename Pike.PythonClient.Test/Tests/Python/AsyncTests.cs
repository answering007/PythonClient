using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64;
using Pike.PythonClient64.Data;

namespace Pike.PythonClient.Test.Tests.Python
{
    /// <summary>
    /// Asynchronous API tests. Require the python runtime (PYTHONNET_TEST_DLL)
    /// </summary>
    [TestClass]
    [TestCategory("Python")]
    public class AsyncTests
    {
        const string Script = @"import pandas as pd
result = pd.DataFrame({'Value': [1, 2, 3]})";

        /// <summary>
        /// Skips the tests when the python runtime is not configured
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestEnvironment.SkipIfNoPython();
        }

        /// <summary>
        /// Restores the test-wide engine settings
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            TestEnvironment.ShutdownIfOwned();
        }

        /// <summary>
        /// Connection can be opened asynchronously (P1-4)
        /// </summary>
        [TestMethod]
        public async Task OpenAsyncOpensConnection()
        {
            var builder = TestEnvironment.CreateBuilder();
            using (var connection = new PythonConnection { ConnectionString = builder.ConnectionString })
            {
                await connection.OpenAsync();

                Assert.AreEqual(ConnectionState.Open, connection.State);
                Assert.IsFalse(string.IsNullOrEmpty(connection.ServerVersion));
            }
        }

        /// <summary>
        /// ExecuteReaderAsync returns the result table (P1-3)
        /// </summary>
        [TestMethod]
        public async Task ExecuteReaderAsyncReturnsTable()
        {
            using (var connection = TestEnvironment.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = Script;

                var table = new DataTable();
                using (var reader = await command.ExecuteReaderAsync()) table.Load(reader);

                Assert.AreEqual(3, table.Rows.Count);
                Assert.AreEqual(3L, Convert.ToInt64(table.Rows[2][0]));
            }
        }

        /// <summary>
        /// ExecuteScalarAsync returns the first value (P1-3)
        /// </summary>
        [TestMethod]
        public async Task ExecuteScalarAsyncReturnsValue()
        {
            using (var connection = TestEnvironment.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = Script;

                var value = await command.ExecuteScalarAsync();

                Assert.AreEqual(1L, Convert.ToInt64(value));
            }
        }

        /// <summary>
        /// ExecuteNonQueryAsync returns the number of rows (P1-3)
        /// </summary>
        [TestMethod]
        public async Task ExecuteNonQueryAsyncReturnsRowCount()
        {
            using (var connection = TestEnvironment.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = Script;

                Assert.AreEqual(3, await command.ExecuteNonQueryAsync());
            }
        }

        /// <summary>
        /// RunScriptAsync returns the result table (P1-5)
        /// </summary>
        [TestMethod]
        public async Task RunScriptAsyncReturnsTable()
        {
            TestEnvironment.EnsureProviderOpen();

            var context = new PythonScriptContext { Query = "async query" };

            var table = await PythonDataQuery.RunScriptAsync(
                "import pandas as pd\nresult = pd.DataFrame({'Query': [globals()['query']]})", context);

            Assert.AreEqual("async query", table.Rows[0][0]);
        }

        /// <summary>
        /// Cancelled token prevents the execution (P1-3)
        /// </summary>
        [TestMethod]
        public async Task CancelledTokenPreventsExecution()
        {
            using (var connection = TestEnvironment.CreateConnection())
            using (var command = connection.CreateCommand())
            using (var cancellation = new CancellationTokenSource())
            {
                command.CommandText = Script;
                cancellation.Cancel();

                await Assert.ThrowsExceptionAsync<TaskCanceledException>(
                    () => command.ExecuteScalarAsync(cancellation.Token));
            }
        }
    }
}
