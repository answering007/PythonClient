using System;
using System.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64.Data;

namespace Pike.PythonClient.Test.Tests.Unit
{
    /// <summary>
    /// Unit tests for the ADO.NET contracts of the provider (regression tests for P1-3, P1-4).
    /// The tests do not require Python runtime
    /// </summary>
    [TestClass]
    [TestCategory("Unit")]
    public class CommandUnit
    {
        /// <summary>
        /// Command has no connection by default and allows to reset it (P1-1/P1-3)
        /// </summary>
        [TestMethod]
        public void P1_3_ConnectionCanBeSetAndReset()
        {
            var command = new PythonCommand();
            Assert.IsNull(command.Connection);

            using (var connection = new PythonConnection())
            {
                command.Connection = connection;
                Assert.AreSame(connection, command.Connection);
            }

            command.Connection = null;
            Assert.IsNull(command.Connection);
        }

        /// <summary>
        /// Command is not visible at design time (P1-3)
        /// </summary>
        [TestMethod]
        public void P1_3_DesignTimeVisibleIsFalse()
        {
            Assert.IsFalse(new PythonCommand().DesignTimeVisible);
        }

        /// <summary>
        /// Execution without a connection is reported (P1-3)
        /// </summary>
        [TestMethod]
        public void P1_3_ExecuteWithoutConnectionThrows()
        {
            var command = new PythonCommand { CommandText = "result = None" };

            Assert.ThrowsException<InvalidOperationException>(() => command.ExecuteReader());
            Assert.ThrowsException<InvalidOperationException>(() => command.ExecuteScalar());
            Assert.ThrowsException<InvalidOperationException>(() => command.ExecuteNonQuery());
        }

        /// <summary>
        /// Execution with a closed connection is reported (P1-3)
        /// </summary>
        [TestMethod]
        public void P1_3_ExecuteWithClosedConnectionThrows()
        {
            using (var connection = new PythonConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "result = None";

                Assert.ThrowsException<InvalidOperationException>(() => command.ExecuteReader());
            }
        }

        /// <summary>
        /// Unknown connection string keyword is reported (P0-1/P1-4)
        /// </summary>
        [TestMethod]
        public void P1_4_UnknownConnectionStringKeywordThrows()
        {
            using (var connection = new PythonConnection())
            {
                Assert.ThrowsException<System.Collections.Generic.KeyNotFoundException>(
                    () => connection.ConnectionString = "UNKNOWN=1");
            }
        }

        /// <summary>
        /// Empty connection string means "no settings" and does not throw (P1-4)
        /// </summary>
        [TestMethod]
        public void P1_4_EmptyConnectionStringIsAllowed()
        {
            using (var connection = new PythonConnection())
            {
                connection.ConnectionString = null;
                Assert.AreEqual(string.Empty, connection.ConnectionString);

                connection.ConnectionString = "   ";
                Assert.AreEqual(string.Empty, connection.ConnectionString);

                connection.ConnectionString = "FILE=C:\\Scripts\\test.py";
                Assert.AreEqual(@"C:\Scripts\test.py", connection.DataSource);
                Assert.IsFalse(connection.UseQueryAsScript);

                connection.ConnectionString = string.Empty;
                Assert.IsTrue(connection.UseQueryAsScript);
            }
        }

        /// <summary>
        /// Open without PYTHONDLL is reported before the engine is touched (P1-4)
        /// </summary>
        [TestMethod]
        public void P1_4_OpenWithoutPythonDllThrows()
        {
            using (var connection = new PythonConnection())
            {
                Assert.ThrowsException<InvalidOperationException>(() => connection.Open());
                Assert.AreEqual(ConnectionState.Closed, connection.State);
            }
        }

        /// <summary>
        /// Transactions are not supported and must be reported explicitly (P1-4)
        /// </summary>
        [TestMethod]
        public void P1_4_TransactionsAreNotSupported()
        {
            using (var connection = new PythonConnection())
                Assert.ThrowsException<NotSupportedException>(() => connection.BeginTransaction());
        }

        /// <summary>
        /// Database can't be changed (P1-4)
        /// </summary>
        [TestMethod]
        public void P1_4_ChangeDatabaseIsNotSupported()
        {
            using (var connection = new PythonConnection())
                Assert.ThrowsException<NotSupportedException>(() => connection.ChangeDatabase("other"));
        }

        /// <summary>
        /// Server version is empty while the connection is closed (P1-4)
        /// </summary>
        [TestMethod]
        public void P1_4_ServerVersionIsEmptyWhenClosed()
        {
            using (var connection = new PythonConnection())
                Assert.AreEqual(string.Empty, connection.ServerVersion);
        }

        /// <summary>
        /// Double close and dispose do not throw (P0-7)
        /// </summary>
        [TestMethod]
        public void P0_7_CloseIsIdempotent()
        {
            var connection = new PythonConnection();
            connection.Close();
            connection.Close();
            connection.Dispose();
            connection.Dispose();

            Assert.AreEqual(ConnectionState.Closed, connection.State);
        }
    }
}
