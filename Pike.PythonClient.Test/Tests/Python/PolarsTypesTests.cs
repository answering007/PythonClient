using System;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64;
using Pike.PythonClient64.Data;

namespace Pike.PythonClient.Test.Tests.Python
{
    /// <summary>
    /// polars.DataFrame result tests: the frame is read by the provider without converting it to pandas.
    /// The tests are inconclusive when polars is not installed in the python environment
    /// </summary>
    [TestClass]
    [TestCategory("Python")]
    public class PolarsTypesTests
    {
        /// <summary>
        /// Skips the tests when python or polars is not available
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestEnvironment.SkipIfNoPython();
            TestEnvironment.SkipIfNoPolars();
        }

        /// <summary>
        /// Restores the shared state
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            PythonDataQuery.StrictColumnTypes = true;
            TestEnvironment.ShutdownIfOwned();
        }

        /// <summary>
        /// Plain polars types are converted to the expected managed types, missing values become DBNull
        /// </summary>
        [TestMethod]
        public void PolarsCoreTypes()
        {
            const string code = @"import polars as pl

result = pl.DataFrame({
    'Int8': pl.Series([1, 2], dtype=pl.Int8),
    'UInt32': pl.Series([1, 2], dtype=pl.UInt32),
    'Int64': pl.Series([1, None], dtype=pl.Int64),
    'Float32': pl.Series([1.5, None], dtype=pl.Float32),
    'Float64': pl.Series([1.5, None], dtype=pl.Float64),
    'Flag': pl.Series([True, None], dtype=pl.Boolean),
    'Text': ['a', None],
})";

            var table = PythonDataQuery.RunScript(code);

            Assert.AreEqual(typeof(int), table.Columns["Int8"].DataType);
            Assert.AreEqual(typeof(int), table.Columns["UInt32"].DataType);
            Assert.AreEqual(typeof(long), table.Columns["Int64"].DataType);
            Assert.AreEqual(typeof(double), table.Columns["Float32"].DataType);
            Assert.AreEqual(typeof(double), table.Columns["Float64"].DataType);
            Assert.AreEqual(typeof(bool), table.Columns["Flag"].DataType);
            Assert.AreEqual(typeof(string), table.Columns["Text"].DataType);

            Assert.AreEqual(2, table.Rows.Count);
            Assert.AreEqual(1, table.Rows[0]["Int8"]);
            Assert.AreEqual(2, table.Rows[1]["Int8"]);
            Assert.AreEqual(1L, table.Rows[0]["Int64"]);
            Assert.AreEqual(1.5d, table.Rows[0]["Float64"]);
            Assert.AreEqual(true, table.Rows[0]["Flag"]);
            Assert.AreEqual("a", table.Rows[0]["Text"]);

            Assert.AreEqual(DBNull.Value, table.Rows[1]["Int64"]);
            Assert.AreEqual(DBNull.Value, table.Rows[1]["Float32"]);
            Assert.AreEqual(DBNull.Value, table.Rows[1]["Float64"]);
            Assert.AreEqual(DBNull.Value, table.Rows[1]["Flag"]);
            Assert.AreEqual(DBNull.Value, table.Rows[1]["Text"]);
        }

        /// <summary>
        /// Date/Datetime/Duration/Time are read from the physical representation: all time units, dates before
        /// 1970 and time zone aware values are supported
        /// </summary>
        [TestMethod]
        public void PolarsTemporalTypes()
        {
            const string code = @"import datetime as dt
import polars as pl

result = pl.DataFrame({
    'Date': pl.Series([dt.date(1970, 1, 2), None], dtype=pl.Date),
    'DatetimeNs': pl.Series([dt.datetime(1960, 1, 1), None], dtype=pl.Datetime('ns')),
    'DatetimeUs': pl.Series([dt.datetime(2020, 1, 1, 12, 30, 15, 123456), None], dtype=pl.Datetime('us')),
    'DatetimeMs': pl.Series([dt.datetime(2020, 1, 1, 12, 30, 15, 123000), None], dtype=pl.Datetime('ms')),
    'Duration': pl.Series([dt.timedelta(milliseconds=1500), None], dtype=pl.Duration('ms')),
    'Time': pl.Series([dt.time(0, 0, 1), None], dtype=pl.Time),
    'Utc': pl.Series([dt.datetime(2020, 1, 1, 12, 0, tzinfo=dt.timezone.utc), None],
                     dtype=pl.Datetime('us', 'UTC')),
})";

            var table = PythonDataQuery.RunScript(code);

            foreach (var name in new[] { "Date", "DatetimeNs", "DatetimeUs", "DatetimeMs", "Utc" })
                Assert.AreEqual(typeof(DateTime), table.Columns[name].DataType, name);
            Assert.AreEqual(typeof(TimeSpan), table.Columns["Duration"].DataType);
            Assert.AreEqual(typeof(TimeSpan), table.Columns["Time"].DataType);

            Assert.AreEqual(new DateTime(1970, 1, 2), table.Rows[0]["Date"]);
            Assert.AreEqual(new DateTime(1960, 1, 1), table.Rows[0]["DatetimeNs"]);
            Assert.AreEqual(new DateTime(2020, 1, 1, 12, 30, 15).AddTicks(1234560), table.Rows[0]["DatetimeUs"]);
            Assert.AreEqual(new DateTime(2020, 1, 1, 12, 30, 15).AddTicks(1230000), table.Rows[0]["DatetimeMs"]);
            Assert.AreEqual(TimeSpan.FromMilliseconds(1500), table.Rows[0]["Duration"]);
            Assert.AreEqual(TimeSpan.FromSeconds(1), table.Rows[0]["Time"]);
            Assert.AreEqual(new DateTime(2020, 1, 1, 12, 0, 0), table.Rows[0]["Utc"]);
            // DateTime.Kind is not preserved by DataColumn: the value is the UTC clock time

            foreach (var name in new[] { "Date", "DatetimeNs", "DatetimeUs", "DatetimeMs", "Utc", "Duration", "Time" })
                Assert.AreEqual(DBNull.Value, table.Rows[1][name], name);
        }

        /// <summary>
        /// Categorical and Enum values are read as strings, not as their physical codes
        /// </summary>
        [TestMethod]
        public void PolarsCategoricalAndEnum()
        {
            const string code = @"import polars as pl

result = pl.DataFrame({
    'Cat': pl.Series(['a', None], dtype=pl.Categorical),
    'Enum': pl.Series(['a', None], dtype=pl.Enum(['a', 'b'])),
    'Text': pl.Series(['x', None], dtype=pl.String),
})";

            var table = PythonDataQuery.RunScript(code);

            Assert.AreEqual(typeof(string), table.Columns["Cat"].DataType);
            Assert.AreEqual(typeof(string), table.Columns["Enum"].DataType);
            Assert.AreEqual(typeof(string), table.Columns["Text"].DataType);

            Assert.AreEqual("a", table.Rows[0]["Cat"]);
            Assert.AreEqual("a", table.Rows[0]["Enum"]);
            Assert.AreEqual("x", table.Rows[0]["Text"]);
            Assert.AreEqual(DBNull.Value, table.Rows[1]["Cat"]);
            Assert.AreEqual(DBNull.Value, table.Rows[1]["Enum"]);
            Assert.AreEqual(DBNull.Value, table.Rows[1]["Text"]);
        }

        /// <summary>
        /// polars null and NaN are converted to DBNull, as the pandas path does
        /// </summary>
        [TestMethod]
        public void PolarsNullsAndNan()
        {
            const string code = @"import polars as pl

result = pl.DataFrame({
    'Float': [1.5, float('nan'), None],
    'Text': ['a', None, 'b'],
})";

            var table = PythonDataQuery.RunScript(code);

            Assert.AreEqual(3, table.Rows.Count);
            Assert.AreEqual(1.5d, table.Rows[0]["Float"]);
            Assert.AreEqual(DBNull.Value, table.Rows[1]["Float"]);
            Assert.AreEqual(DBNull.Value, table.Rows[2]["Float"]);
            Assert.AreEqual("b", table.Rows[2]["Text"]);
        }

        /// <summary>
        /// A polars.Null column is exposed as a string column where every value is DBNull
        /// </summary>
        [TestMethod]
        public void PolarsNullColumn()
        {
            const string code = @"import polars as pl
result = pl.DataFrame({'Empty': pl.Series([None, None], dtype=pl.Null)})";

            var table = PythonDataQuery.RunScript(code);

            Assert.AreEqual(typeof(string), table.Columns["Empty"].DataType);
            Assert.AreEqual(2, table.Rows.Count);
            Assert.AreEqual(DBNull.Value, table.Rows[0]["Empty"]);
            Assert.AreEqual(DBNull.Value, table.Rows[1]["Empty"]);
        }

        /// <summary>
        /// Decimal is converted to double, Binary to a byte array
        /// </summary>
        [TestMethod]
        public void PolarsDecimalAndBinary()
        {
            const string code = @"import polars as pl

result = pl.DataFrame({
    'Decimal': pl.Series(['1.23', None]).cast(pl.Decimal(38, 2)),
    'Binary': pl.Series([b'ab', None], dtype=pl.Binary),
})";

            var table = PythonDataQuery.RunScript(code);

            Assert.AreEqual(typeof(double), table.Columns["Decimal"].DataType);
            Assert.AreEqual(1.23d, table.Rows[0]["Decimal"]);
            Assert.AreEqual(DBNull.Value, table.Rows[1]["Decimal"]);

            Assert.AreEqual(typeof(byte[]), table.Columns["Binary"].DataType);
            CollectionAssert.AreEqual(new byte[] { 97, 98 }, (byte[])table.Rows[0]["Binary"]);
            Assert.AreEqual(DBNull.Value, table.Rows[1]["Binary"]);
        }

        /// <summary>
        /// Nested types are rejected in strict mode and converted to strings in non-strict mode
        /// </summary>
        [TestMethod]
        public void PolarsNestedStrictAndFallback()
        {
            const string code = @"import polars as pl
result = pl.DataFrame({'Items': [[1, 2], [3]]})";

            var exception = Assert.ThrowsException<NotSupportedException>(() => PythonDataQuery.RunScript(code));
            StringAssert.Contains(exception.Message, "unsupported dtype");
            StringAssert.Contains(exception.Message, "Items");

            PythonDataQuery.StrictColumnTypes = false;

            var table = PythonDataQuery.RunScript(code);

            Assert.AreEqual(typeof(string), table.Columns["Items"].DataType);
            Assert.AreEqual(2, table.Rows.Count);
            StringAssert.Contains((string)table.Rows[0]["Items"], "1");
            StringAssert.Contains((string)table.Rows[1]["Items"], "3");
        }

        /// <summary>
        /// UInt64 values above long.MaxValue have no .NET representation and become DBNull
        /// </summary>
        [TestMethod]
        public void PolarsUInt64Overflow()
        {
            const string code = @"import polars as pl
result = pl.DataFrame({'Value': pl.Series([2**64 - 1], dtype=pl.UInt64)})";

            var table = PythonDataQuery.RunScript(code);

            Assert.AreEqual(typeof(long), table.Columns["Value"].DataType);
            Assert.AreEqual(DBNull.Value, table.Rows[0]["Value"]);
        }

        /// <summary>
        /// An empty frame keeps the schema, a frame without columns gives an empty table
        /// </summary>
        [TestMethod]
        public void PolarsEmptyFrame()
        {
            var table = PythonDataQuery.RunScript(
                "import polars as pl\nresult = pl.DataFrame(schema={'A': pl.Int64, 'B': pl.String})");

            Assert.AreEqual(2, table.Columns.Count);
            Assert.AreEqual(0, table.Rows.Count);
            Assert.AreEqual(typeof(long), table.Columns["A"].DataType);
            Assert.AreEqual(typeof(string), table.Columns["B"].DataType);

            var empty = PythonDataQuery.RunScript("import polars as pl\nresult = pl.DataFrame()");

            Assert.AreEqual(0, empty.Columns.Count);
            Assert.AreEqual(0, empty.Rows.Count);
        }

        /// <summary>
        /// Invalid DataColumn characters are replaced and colliding names are uniquified
        /// </summary>
        [TestMethod]
        public void PolarsColumnNames()
        {
            const string code = @"import polars as pl
result = pl.DataFrame({'a.b': [1], 'a_b': [2], 'a[b]': [3]})";

            var table = PythonDataQuery.RunScript(code);
            var names = table.Columns.Cast<DataColumn>().Select(column => column.ColumnName).ToArray();

            CollectionAssert.AreEqual(new[] { "a_b", "a_b_2", "a(b)" }, names);
        }

        /// <summary>
        /// The ADO.NET layer works with a polars result the same way as with a pandas one
        /// </summary>
        [TestMethod]
        public void PolarsAdoParity()
        {
            const string code = @"import polars as pl
result = pl.DataFrame({'A': [1, 2], 'B': ['x', 'y']})";

            using (var connection = TestEnvironment.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = code;

                using (var reader = command.ExecuteReader(CommandBehavior.SchemaOnly))
                {
                    Assert.AreEqual(2, reader.FieldCount);
                    Assert.IsFalse(reader.Read());
                }

                using (var reader = command.ExecuteReader(CommandBehavior.SingleRow))
                {
                    Assert.IsTrue(reader.Read());
                    Assert.AreEqual(1L, reader.GetInt64(0));
                    Assert.AreEqual("x", reader.GetString(1));
                    Assert.IsFalse(reader.Read());
                }

                Assert.AreEqual(1L, command.ExecuteScalar());
                Assert.AreEqual(2, command.ExecuteNonQuery());
            }
        }

        /// <summary>
        /// A polars.LazyFrame must be materialized by the script: the provider rejects it with a hint
        /// </summary>
        [TestMethod]
        public void PolarsLazyFrameIsRejected()
        {
            const string code = @"import polars as pl
result = pl.DataFrame({'A': [1]}).lazy()";

            var exception = Assert.ThrowsException<InvalidOperationException>(() => PythonDataQuery.RunScript(code));

            StringAssert.Contains(exception.Message, "LazyFrame");
            StringAssert.Contains(exception.Message, "collect");
        }

        /// <summary>
        /// A result that is not a frame is rejected with a clear message
        /// </summary>
        [TestMethod]
        public void PolarsResultMustBeFrame()
        {
            var exception = Assert.ThrowsException<InvalidOperationException>(
                () => PythonDataQuery.RunScript("result = [1, 2, 3]"));

            StringAssert.Contains(exception.Message, "pandas.DataFrame or polars.DataFrame");
        }

        /// <summary>
        /// pandas and polars results can be used in the same process without any shared state
        /// </summary>
        [TestMethod]
        public void PandasAndPolarsInOneProcess()
        {
            TestEnvironment.EnsureProviderOpen();

            var pandas = PythonDataQuery.RunScript("import pandas as pd\nresult = pd.DataFrame({'A': [1]})");
            var polars = PythonDataQuery.RunScript("import polars as pl\nresult = pl.DataFrame({'A': [2]})");
            var pandasAgain = PythonDataQuery.RunScript("import pandas as pd\nresult = pd.DataFrame({'A': [3]})");

            Assert.AreEqual(1L, Convert.ToInt64(pandas.Rows[0]["A"]));
            Assert.AreEqual(2L, polars.Rows[0]["A"]);
            Assert.AreEqual(3L, Convert.ToInt64(pandasAgain.Rows[0]["A"]));
        }

        /// <summary>
        /// The script file mode works with polars: the Excel table payload is passed as 'query' and applied
        /// with the 'params' value (Power Query 'table to script' scenario)
        /// </summary>
        [TestMethod]
        public void PolarsScriptFileAndVariables()
        {
            const string json =
                "{\"No\":[1,2,3],\"Name\":[\"Olivia\",\"Ava\",\"Isabella\"]," +
                "\"Value\":[0.0,0.6931471805599453,1.0986122886681098]}";
            const double expectedValue = 9.010913347279289; // 0.6931471805599453 * 13

            var builder = TestEnvironment.CreateBuilder(GetScriptFile("TestScript04.py"));
            using (var connection = new PythonConnection { ConnectionString = builder.ConnectionString })
            {
                connection.Open();

                var table = Execute(connection, CreateCompressedPayload(json),
                    new PythonParameter { ParameterName = "factor", Value = 13 });

                Assert.AreEqual(3, table.Rows.Count);
                CollectionAssert.AreEqual(
                    new[] { "No", "Name", "Value" },
                    table.Columns.Cast<DataColumn>().Select(column => column.ColumnName).ToArray());
                Assert.AreEqual("Ava", table.Rows[1]["Name"]);
                Assert.AreEqual(expectedValue, Convert.ToDouble(table.Rows[1]["Value"]), 1e-9);
            }
        }

        /// <summary>
        /// The script file result without a query: types and missing values are converted
        /// </summary>
        [TestMethod]
        public void PolarsScriptFileDefaultResult()
        {
            var builder = TestEnvironment.CreateBuilder(GetScriptFile("TestScript04.py"));
            using (var connection = new PythonConnection { ConnectionString = builder.ConnectionString })
            {
                connection.Open();

                var table = Execute(connection, string.Empty);

                Assert.AreEqual(5, table.Columns.Count);
                Assert.AreEqual(3, table.Rows.Count);
                Assert.AreEqual(typeof(DateTime), table.Columns["Created"].DataType);
                Assert.AreEqual(new DateTime(2020, 1, 1), table.Rows[0]["Created"]);
                Assert.AreEqual(DBNull.Value, table.Rows[1]["Created"]);
                Assert.AreEqual(new DateTime(1960, 1, 1), table.Rows[2]["Created"]);
                Assert.AreEqual(DBNull.Value, table.Rows[1]["Flag"]);
            }
        }

        /// <summary>
        /// A large frame is read without errors and without the row-wise python calls
        /// </summary>
        [TestMethod]
        public void PolarsLargeFrame()
        {
            const string code = @"import polars as pl

index = pl.int_range(0, 100000, eager=True)
result = pl.DataFrame({
    'Id': index,
    'Value': index * 2,
    'Flag': index % 2 == 0,
})";

            var table = PythonDataQuery.RunScript(code);

            Assert.AreEqual(100000, table.Rows.Count);
            Assert.AreEqual(0L, table.Rows[0]["Id"]);
            Assert.AreEqual(199998L, table.Rows[99999]["Value"]);
            Assert.AreEqual(true, table.Rows[0]["Flag"]);
            Assert.AreEqual(false, table.Rows[1]["Flag"]);
        }

        /// <summary>
        /// Executes the command text with the specified parameters and loads the result into a new table
        /// </summary>
        /// <param name="connection">Opened connection</param>
        /// <param name="commandText">Command text (script text when the connection has no script file)</param>
        /// <param name="parameters">Command parameters</param>
        /// <returns>Loaded table</returns>
        static DataTable Execute(PythonConnection connection, string commandText,
            params PythonParameter[] parameters)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = commandText;
                foreach (var parameter in parameters) command.Parameters.Add(parameter);

                var dataTable = new DataTable();
                using (var reader = command.ExecuteReader()) dataTable.Load(reader);

                return dataTable;
            }
        }

        /// <summary>
        /// Returns the full path to the test python script copied to the output folder
        /// </summary>
        /// <param name="fileName">Script file name</param>
        /// <returns>Full path to the script</returns>
        static string GetScriptFile(string fileName)
        {
            var scriptFile = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                SettingsMain.Default.PythonScriptsFolder,
                fileName);

            if (!File.Exists(scriptFile)) throw new FileNotFoundException("Where is the script?", scriptFile);

            return scriptFile;
        }

        /// <summary>
        /// Returns the gzip compressed and base64 encoded JSON, the payload Power Query creates for an Excel table
        /// (<c>Binary.Compress(Json.FromValue(...))</c> + <c>Binary.ToText</c>)
        /// </summary>
        /// <param name="json">JSON text of the table</param>
        /// <returns>Base64 text of the compressed JSON</returns>
        static string CreateCompressedPayload(string json)
        {
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionMode.Compress, true))
                using (var writer = new StreamWriter(gzip, new UTF8Encoding(false)))
                    writer.Write(json);

                return Convert.ToBase64String(output.ToArray());
            }
        }




    }
}
