using System;
using System.Data;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64.Data;

namespace Pike.PythonClient.Test.Tests.Python
{
    /// <summary>
    /// <see cref="PythonCommand"/> tests. Require the python runtime (PYTHONNET_TEST_DLL)
    /// </summary>
    [TestClass]
    [TestCategory("Python")]
    public class CommandTests
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
        /// CommandBehavior.SchemaOnly returns the schema without rows (P1-3)
        /// </summary>
        [TestMethod]
        public void P1_3_SchemaOnlyReturnsNoRows()
        {
            using (var connection = TestEnvironment.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = Script;

                var table = new DataTable();
                using (var reader = command.ExecuteReader(CommandBehavior.SchemaOnly)) table.Load(reader);

                Assert.AreEqual(1, table.Columns.Count);
                Assert.AreEqual(0, table.Rows.Count);
            }
        }

        /// <summary>
        /// CommandBehavior.SingleRow returns the first row only (P1-3)
        /// </summary>
        [TestMethod]
        public void P1_3_SingleRowReturnsSingleRow()
        {
            using (var connection = TestEnvironment.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = Script;

                var table = new DataTable();
                using (var reader = command.ExecuteReader(CommandBehavior.SingleRow)) table.Load(reader);

                Assert.AreEqual(1, table.Rows.Count);
                Assert.AreEqual(1L, Convert.ToInt64(table.Rows[0][0]));
            }
        }

        /// <summary>
        /// CommandBehavior.CloseConnection closes the connection after reading (P1-3)
        /// </summary>
        [TestMethod]
        public void P1_3_CloseConnectionClosesConnection()
        {
            using (var connection = TestEnvironment.CreateConnection())
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = Script;
                    using (var reader = command.ExecuteReader(CommandBehavior.CloseConnection))
                        while (reader.Read())
                        {
                        }
                }

                Assert.AreEqual(ConnectionState.Closed, connection.State);
            }
        }

        /// <summary>
        /// ExecuteNonQuery allows scripts without the result variable (P1-3)
        /// </summary>
        [TestMethod]
        public void P1_3_ExecuteNonQueryWithoutResultGivesZero()
        {
            using (var connection = TestEnvironment.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "import pandas as pd\nvalue = 1";

                Assert.AreEqual(0, command.ExecuteNonQuery());
            }
        }

        /// <summary>
        /// ExecuteNonQuery returns the number of result rows (P1-3)
        /// </summary>
        [TestMethod]
        public void P1_3_ExecuteNonQueryReturnsRowCount()
        {
            using (var connection = TestEnvironment.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = Script;

                Assert.AreEqual(3, command.ExecuteNonQuery());
            }
        }

        /// <summary>
        /// CommandTimeout is not supported and must be reported explicitly (P1-3)
        /// </summary>
        [TestMethod]
        public void P1_3_CommandTimeoutThrowsNotSupported()
        {
            using (var connection = TestEnvironment.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = Script;
                command.CommandTimeout = 30;

                Assert.ThrowsException<NotSupportedException>(() => command.ExecuteReader());
            }
        }

        /// <summary>
        /// Script file is read as UTF-8, unicode content is preserved (P1-3)
        /// </summary>
        [TestMethod]
        public void P1_3_ScriptFileIsReadAsUtf8()
        {
            const string expected = "\u0421\u043A\u0440\u0438\u043F\u0442 \u0443\u0441\u043F\u0435\u0448\u0435\u043D";
            var scriptFile = Path.Combine(Path.GetTempPath(), $"pike_test_{Guid.NewGuid():N}.py");
            File.WriteAllText(
                scriptFile,
                $"import pandas as pd\nresult = pd.DataFrame({{'Text': ['{expected}']}})\n",
                new UTF8Encoding(false));

            try
            {
                var builder = TestEnvironment.CreateBuilder(scriptFile);
                using (var connection = new PythonConnection { ConnectionString = builder.ConnectionString })
                {
                    connection.Open();
                    using (var command = connection.CreateCommand())
                    using (var reader = command.ExecuteReader())
                    {
                        Assert.IsTrue(reader.Read());
                        Assert.AreEqual(expected, reader[0]);
                    }
                }
            }
            finally
            {
                File.Delete(scriptFile);
            }
        }

        /// <summary>
        /// '@' and ':' parameter prefixes are trimmed (P1-5)
        /// </summary>
        [TestMethod]
        public void P1_5_ParameterPrefixesAreTrimmed()
        {
            const string script = @"import pandas as pd
params = globals()['params']
result = pd.DataFrame({'At': [params['at']], 'Colon': [params['colon']]})";

            using (var connection = TestEnvironment.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = script;
                command.Parameters.Add(new PythonParameter { ParameterName = "@at", Value = "at-value" });
                command.Parameters.Add(new PythonParameter { ParameterName = ":colon", Value = "colon-value" });

                var table = new DataTable();
                using (var reader = command.ExecuteReader()) table.Load(reader);

                Assert.AreEqual("at-value", table.Rows[0][0]);
                Assert.AreEqual("colon-value", table.Rows[0][1]);
            }
        }

        /// <summary>
        /// Parameter values of the extended types reach python (P1-5)
        /// </summary>
        [TestMethod]
        public void P1_5_ExtendedParameterTypesAreTransferred()
        {
            const string script = @"import pandas as pd
params = globals()['params']
result = pd.DataFrame({
    'Decimal': [str(params['decimal_value'])],
    'Guid': [params['guid']],
    'TimeSpan': [str(params['span'])],
    'DateTimeUtc': [str(params['utc'])]
})";

            var guid = Guid.NewGuid();
            using (var connection = TestEnvironment.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = script;
                command.Parameters.Add(new PythonParameter { ParameterName = "decimal_value", Value = 12.5m });
                command.Parameters.Add(new PythonParameter { ParameterName = "guid", Value = guid });
                command.Parameters.Add(new PythonParameter { ParameterName = "span", Value = TimeSpan.FromHours(1) });
                command.Parameters.Add(new PythonParameter
                {
                    ParameterName = "utc",
                    Value = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                });

                var table = new DataTable();
                using (var reader = command.ExecuteReader()) table.Load(reader);

                Assert.AreEqual("12.5", table.Rows[0]["Decimal"].ToString());
                Assert.AreEqual(guid.ToString(), table.Rows[0]["Guid"].ToString());
                StringAssert.Contains(table.Rows[0]["TimeSpan"].ToString(), "3600000000000");
            }
        }
    }
}
