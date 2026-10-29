using System;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64.Data;

namespace Pike.PythonClient.Test.Tests.Python
{
    /// <summary>
    /// Streaming reader tests (P1-3). Require the python runtime (PYTHONNET_TEST_DLL)
    /// </summary>
    [TestClass]
    [TestCategory("Python")]
    public class StreamingTests
    {
        const string Script = @"import pandas as pd
result = pd.DataFrame({
    'Id': range(2500),
    'Text': ['row'] * 2500,
    'Value': [1.5] * 2500
})";

        const string NullsScript = @"import pandas as pd
result = pd.DataFrame({
    'Int64Nullable': pd.array([1, pd.NA], dtype='Int64'),
    'DateTime': pd.to_datetime(['2020-01-01', None])
})";

        /// <summary>
        /// Skips the tests when the python runtime is not configured
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestEnvironment.SkipIfNoPython();
        }

        /// <summary>
        /// Restores the engine state
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            TestEnvironment.ShutdownIfOwned();
        }

        static PythonCommand CreateStreamingCommand(PythonConnection connection, int chunkSize = 1000)
        {
            var command = (PythonCommand)connection.CreateCommand();
            command.CommandText = Script;
            command.Streaming = true;
            command.StreamingChunkSize = chunkSize;

            return command;
        }

        /// <summary>
        /// Streaming reader returns all rows across several chunks (P1-3)
        /// </summary>
        [TestMethod]
        public void P1_3_StreamingReaderReturnsAllRows()
        {
            using (var connection = TestEnvironment.CreateConnection())
            using (var command = CreateStreamingCommand(connection, 1000))
            using (var reader = command.ExecuteReader())
            {
                Assert.IsInstanceOfType(reader, typeof(PythonDataReader));
                Assert.AreEqual(3, reader.FieldCount);
                Assert.IsTrue(reader.HasRows);

                var count = 0;
                long lastId = 0;
                while (reader.Read())
                {
                    lastId = reader.GetInt64(0);
                    count++;
                }

                Assert.AreEqual(2500, count);
                Assert.AreEqual(2499L, lastId);
            }
        }

        /// <summary>
        /// Streaming reader exposes names, ordinals and managed types (P1-3)
        /// </summary>
        [TestMethod]
        public void P1_3_StreamingReaderSchema()
        {
            using (var connection = TestEnvironment.CreateConnection())
            using (var command = CreateStreamingCommand(connection))
            using (var reader = command.ExecuteReader())
            {
                Assert.AreEqual("Id", reader.GetName(0));
                Assert.AreEqual("Text", reader.GetName(1));
                Assert.AreEqual(0, reader.GetOrdinal("id"));
                Assert.AreEqual(typeof(long), reader.GetFieldType(0));
                Assert.AreEqual(typeof(string), reader.GetFieldType(1));
                Assert.AreEqual(typeof(double), reader.GetFieldType(2));

                var schema = reader.GetSchemaTable();
                Assert.AreEqual(3, schema.Rows.Count);
                Assert.AreEqual("Id", schema.Rows[0]["ColumnName"]);
                Assert.AreEqual("int64", reader.GetDataTypeName(0));
            }
        }

        /// <summary>
        /// DataTable.Load works with the streaming reader (P1-3)
        /// </summary>
        [TestMethod]
        public void P1_3_StreamingReaderCanBeLoadedIntoDataTable()
        {
            using (var connection = TestEnvironment.CreateConnection())
            using (var command = CreateStreamingCommand(connection, 500))
            using (var reader = command.ExecuteReader())
            {
                var table = new DataTable();
                table.Load(reader);

                Assert.AreEqual(2500, table.Rows.Count);
                Assert.AreEqual(3, table.Columns.Count);
                Assert.AreEqual(2499L, table.Rows[2499]["Id"]);
            }
        }

        /// <summary>
        /// Missing values are reported through IsDBNull in streaming mode (P1-3)
        /// </summary>
        [TestMethod]
        public void P1_3_StreamingReaderHandlesNulls()
        {
            using (var connection = TestEnvironment.CreateConnection())
            using (var command = (PythonCommand)connection.CreateCommand())
            {
                command.CommandText = NullsScript;
                command.Streaming = true;
                command.StreamingChunkSize = 1;

                using (var reader = command.ExecuteReader())
                {
                    Assert.IsTrue(reader.Read());
                    Assert.IsFalse(reader.IsDBNull(0));
                    Assert.IsFalse(reader.IsDBNull(1));
                    Assert.AreEqual(new DateTime(2020, 1, 1), reader.GetDateTime(1));

                    Assert.IsTrue(reader.Read());
                    Assert.IsTrue(reader.IsDBNull(0));
                    Assert.IsTrue(reader.IsDBNull(1));
                    Assert.AreEqual(DBNull.Value, reader.GetValue(1));

                    Assert.IsFalse(reader.Read());
                }
            }
        }

        /// <summary>
        /// SchemaOnly returns the schema without rows (P1-3)
        /// </summary>
        [TestMethod]
        public void P1_3_StreamingSchemaOnlyReturnsNoRows()
        {
            using (var connection = TestEnvironment.CreateConnection())
            using (var command = CreateStreamingCommand(connection))
            using (var reader = command.ExecuteReader(CommandBehavior.SchemaOnly))
            {
                Assert.AreEqual(3, reader.FieldCount);
                Assert.IsFalse(reader.HasRows);
                Assert.IsFalse(reader.Read());
            }
        }

        /// <summary>
        /// SingleRow limits the streaming reader to one row (P1-3)
        /// </summary>
        [TestMethod]
        public void P1_3_StreamingSingleRowReturnsOneRow()
        {
            using (var connection = TestEnvironment.CreateConnection())
            using (var command = CreateStreamingCommand(connection, 100))
            using (var reader = command.ExecuteReader(CommandBehavior.SingleRow))
            {
                Assert.IsTrue(reader.Read());
                Assert.AreEqual(0L, reader.GetInt64(0));
                Assert.IsFalse(reader.Read());
            }
        }

        /// <summary>
        /// CloseConnection closes the connection when the streaming reader is closed (P1-3)
        /// </summary>
        [TestMethod]
        public void P1_3_StreamingCloseConnectionClosesConnection()
        {
            using (var connection = TestEnvironment.CreateConnection())
            {
                using (var command = CreateStreamingCommand(connection))
                using (var reader = command.ExecuteReader(CommandBehavior.CloseConnection))
                    while (reader.Read())
                    {
                    }

                Assert.AreEqual(ConnectionState.Closed, connection.State);
            }
        }
    }
}
