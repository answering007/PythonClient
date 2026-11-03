using System;
using System.Collections.Concurrent;
using System.Data;
using System.Linq;
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
        /// Restores the test-wide engine settings
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            PythonDataProvider.ShutdownOnLastClose = false;
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
                Assert.AreEqual(1L, Convert.ToInt64(command.ExecuteScalar()));
            }

            second.Close();
        }

        /// <summary>
        /// Closing the last connection releases the last reference but must not fail;
        /// the engine stays reusable (P0-7). pythonnet keeps the interpreter initialized when modules
        /// like pandas are imported, so the finalization is best-effort
        /// </summary>
        [TestMethod]
        public void P0_7_EngineShutsDownWhenLastConnectionClosed()
        {
            var baseline = PythonDataProvider.ReferenceCount;
            PythonDataProvider.ShutdownOnLastClose = true;
            try
            {
                var connection = TestEnvironment.CreateConnection();
                Assert.AreEqual(baseline + 1, PythonDataProvider.ReferenceCount);
                Assert.IsTrue(PythonEngine.IsInitialized);

                connection.Close();

                Assert.AreEqual(baseline, PythonDataProvider.ReferenceCount);

                // Provider must stay usable after the last close
                var reused = TestEnvironment.CreateConnection();
                Assert.AreEqual(ConnectionState.Open, reused.State);
                Assert.AreEqual(baseline + 1, PythonDataProvider.ReferenceCount);

                reused.Close();
                Assert.AreEqual(baseline, PythonDataProvider.ReferenceCount);
            }
            finally
            {
                PythonDataProvider.ShutdownOnLastClose = false;
            }
        }

        /// <summary>
        /// Closing the same connection twice must not release someone else's reference (P0-7)
        /// </summary>
        [TestMethod]
        public void P0_7_DoubleCloseReleasesSingleReference()
        {
            var baseline = PythonDataProvider.ReferenceCount;

            var first = TestEnvironment.CreateConnection();
            var second = TestEnvironment.CreateConnection();
            Assert.AreEqual(baseline + 2, PythonDataProvider.ReferenceCount);

            first.Close();
            first.Close();

            Assert.AreEqual(baseline + 1, PythonDataProvider.ReferenceCount);
            Assert.AreEqual(ConnectionState.Open, second.State);

            second.Close();

            Assert.AreEqual(baseline, PythonDataProvider.ReferenceCount);
        }

        /// <summary>
        /// The GIL must be available for every thread, not only for the thread that initialized the engine (P0-6)
        /// </summary>
        [TestMethod]
        public void P0_6_GilIsAvailableFromAnotherThread()
        {
            using (var connection = TestEnvironment.CreateConnection())
            {
                var done = new ManualResetEventSlim(false);
                var thread = new Thread(() =>
                {
                    using (Py.GIL())
                    using (var sys = Py.Import("sys"))
                    {
                        Assert.IsTrue(sys.HasAttr("path"));
                    }

                    done.Set();
                })
                {
                    IsBackground = true
                };

                thread.Start();

                Assert.IsTrue(done.Wait(TimeSpan.FromSeconds(20)), "GIL acquisition on a worker thread timed out");
            }
        }

        /// <summary>
        /// RunScript must work in a thread different from the thread that opened the connection (P0-6)
        /// </summary>
        [TestMethod]
        public void P0_6_RunScriptFromAnotherThread()
        {
            using (var connection = TestEnvironment.CreateConnection())
            {
                long result = 0;
                Exception threadException = null;

                var thread = new Thread(() =>
                {
                    try
                    {
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = Script;
                            result = Convert.ToInt64(command.ExecuteScalar());
                        }
                    }
                    catch (Exception exception)
                    {
                        threadException = exception;
                    }
                })
                {
                    IsBackground = true   // a hung interpreter must not block the test host
                };

                thread.Start();
                var finished = thread.Join(TimeSpan.FromSeconds(60));

                Assert.IsTrue(finished, "RunScript did not finish in the worker thread");
                Assert.IsNull(threadException, threadException?.ToString());
                Assert.AreEqual(1L, result);
            }
        }

        /// <summary>
        /// Parallel connections must not corrupt the interpreter (P0-6, P0-7)
        /// </summary>
        [TestMethod]
        public void P0_6_ParallelConnections()
        {
            var exceptions = new ConcurrentQueue<Exception>();

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
                catch (Exception exception)
                {
                    exceptions.Enqueue(exception);
                }
            });

            Assert.AreEqual(0, exceptions.Count,
                string.Join(Environment.NewLine, exceptions.Select(exception => exception.ToString())));
        }
    }
}
