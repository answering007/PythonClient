using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64;
using Python.Runtime;

namespace Pike.PythonClient.Test.Tests.Python
{
    /// <summary>
    /// Engine lifecycle and GIL tests (regression tests for P0-6, P0-7).
    /// Requires the python runtime (<c>PYTHONNET_TEST_DLL</c>); the tests are inconclusive without it
    /// </summary>
    [TestClass]
    [TestCategory("Python")]
    public class EngineLifecycleTests
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
        /// Restores the clean engine state
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            TestEnvironment.ShutdownIfOwned();
        }

        /// <summary>
        /// Closing one connection must not shut the engine down while another connection is opened (P0-7)
        /// </summary>
        [TestMethod]
        public void P0_7_ClosingOneConnectionKeepsEngineForAnother()
        {
            var first = TestEnvironment.CreateConnection();
            var second = TestEnvironment.CreateConnection();

            first.Close();

            Assert.AreEqual(ConnectionState.Closed, first.State);
            Assert.AreEqual(ConnectionState.Open, second.State);

            using (var command = second.CreateCommand())
            {
                command.CommandText = Script;
                Assert.AreEqual(1, command.ExecuteScalar());
            }

            second.Close();
        }

        /// <summary>
        /// Engine must be shut down when the last connection is closed (P0-7)
        /// </summary>
        [TestMethod]
        public void P0_7_EngineShutsDownWhenLastConnectionClosed()
        {
            var connection = TestEnvironment.CreateConnection();
            Assert.IsTrue(PythonEngine.IsInitialized);

            connection.Close();

            Assert.IsFalse(PythonEngine.IsInitialized);
            Assert.AreEqual(ConnectionState.Closed, PythonDataProvider.State);
        }

        /// <summary>
        /// Closing the same connection twice must not release someone else's reference (P0-7)
        /// </summary>
        [TestMethod]
        public void P0_7_DoubleCloseReleasesSingleReference()
        {
            var first = TestEnvironment.CreateConnection();
            var second = TestEnvironment.CreateConnection();

            first.Close();
            first.Close();

            Assert.AreEqual(ConnectionState.Open, second.State);

            second.Close();

            Assert.IsFalse(PythonEngine.IsInitialized);
        }

        /// <summary>
        /// RunScript must work in a thread different from the thread that opened the connection (P0-6)
        /// </summary>
        [TestMethod]
        public void P0_6_RunScriptFromAnotherThread()
        {
            using (var connection = TestEnvironment.CreateConnection())
            {
                var result = 0;

                var thread = new Thread(() =>
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = Script;
                        result = (int)command.ExecuteScalar();
                    }
                });
                thread.Start();
                thread.Join(TimeSpan.FromSeconds(60));

                Assert.AreEqual(1, result);
            }
        }

        /// <summary>
        /// Parallel connections must not corrupt the interpreter (P0-6, P0-7)
        /// </summary>
        [TestMethod]
        public void P0_6_ParallelConnections()
        {
            var exceptions = 0;

            Parallel.For(0, 4, _ =>
            {
                try
                {
                    using (var connection = TestEnvironment.CreateConnection())
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = Script;
                        command.ExecuteScalar();
                    }
                }
                catch (Exception)
                {
                    Interlocked.Increment(ref exceptions);
                }
            });

            Assert.AreEqual(0, exceptions);
        }
    }
}
