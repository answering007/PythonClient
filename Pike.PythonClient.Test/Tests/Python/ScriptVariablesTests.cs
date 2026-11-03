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
    /// Tests of the python script variables exposed by the provider: 'query' (command text passed to the script)
    /// and 'params' (command parameters). Require the python runtime (PYTHONNET_TEST_DLL)
    /// </summary>
    [TestClass]
    [TestCategory("Python")]
    public class ScriptVariablesTests
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
        /// Writes the script to a temporary UTF-8 file without BOM
        /// </summary>
        /// <param name="scriptText">Script text</param>
        /// <returns>Full path to the script file</returns>
        static string CreateScriptFile(string scriptText)
        {
            var scriptFile = Path.Combine(Path.GetTempPath(), $"pike_variables_{Guid.NewGuid():N}.py");
            File.WriteAllText(scriptFile, scriptText, new UTF8Encoding(false));

            return scriptFile;
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

        /// <summary>
        /// Command text is passed to the script as the 'query' variable when the connection has no script file
        /// </summary>
        [TestMethod]
        public void QueryVariableContainsCommandText()
        {
            const string script = @"import pandas as pd
result = pd.DataFrame({'Query': [globals()['query']]})";

            using (var connection = TestEnvironment.CreateConnection())
            {
                Assert.IsTrue(connection.UseQueryAsScript);

                var table = Execute(connection, script);

                Assert.AreEqual(script, table.Rows[0]["Query"]);
            }
        }

        /// <summary>
        /// Command text is passed to the script as 'query' when the connection uses a script file
        /// (Power Query passes the compressed Excel table there)
        /// </summary>
        [TestMethod]
        public void QueryVariableContainsCommandTextForScriptFile()
        {
            const string script = @"import pandas as pd
result = pd.DataFrame({'Query': [globals().get('query')], 'HasQuery': ['query' in globals()]})";
            const string commandText = "payload for the script file";

            var scriptFile = CreateScriptFile(script);
            try
            {
                var builder = TestEnvironment.CreateBuilder(scriptFile);
                using (var connection = new PythonConnection { ConnectionString = builder.ConnectionString })
                {
                    connection.Open();
                    Assert.IsFalse(connection.UseQueryAsScript);

                    var table = Execute(connection, commandText);

                    Assert.AreEqual(true, table.Rows[0]["HasQuery"]);
                    Assert.AreEqual(commandText, table.Rows[0]["Query"]);
                }
            }
            finally
            {
                File.Delete(scriptFile);
            }
        }

        /// <summary>
        /// Empty command text means "no query": the 'query' variable is not defined, the script file still runs
        /// </summary>
        [TestMethod]
        public void QueryVariableIsAbsentForEmptyCommandText()
        {
            const string script = @"import pandas as pd
result = pd.DataFrame({'HasQuery': ['query' in globals()]})";

            var scriptFile = CreateScriptFile(script);
            try
            {
                var builder = TestEnvironment.CreateBuilder(scriptFile);
                using (var connection = new PythonConnection { ConnectionString = builder.ConnectionString })
                {
                    connection.Open();

                    var table = Execute(connection, string.Empty);

                    Assert.AreEqual(false, table.Rows[0]["HasQuery"]);
                }
            }
            finally
            {
                File.Delete(scriptFile);
            }
        }

        /// <summary>
        /// No 'query' variable is defined when the default execution context has no query text
        /// </summary>
        [TestMethod]
        public void QueryVariableIsAbsentWithoutContext()
        {
            TestEnvironment.EnsureProviderOpen();

            const string script = @"import pandas as pd
result = pd.DataFrame({'HasQuery': ['query' in globals()]})";

            var table = PythonDataQuery.RunScript(script);

            Assert.AreEqual(false, table.Rows[0]["HasQuery"]);
        }

        /// <summary>
        /// The 'params' dictionary is always defined: an empty dict is passed when the command has no parameters
        /// </summary>
        [TestMethod]
        public void ParamsVariableIsAlwaysDefined()
        {
            const string script = @"import pandas as pd
params = globals()['params']
result = pd.DataFrame({'Type': [type(params).__name__], 'Count': [len(params)]})";

            using (var connection = TestEnvironment.CreateConnection())
            {
                var withoutParameters = Execute(connection, script);
                Assert.AreEqual("dict", withoutParameters.Rows[0]["Type"]);
                Assert.AreEqual(0, Convert.ToInt32(withoutParameters.Rows[0]["Count"]));

                var withParameters = Execute(connection, script,
                    new PythonParameter { ParameterName = "first", Value = 1 },
                    new PythonParameter { ParameterName = "second", Value = "two" });

                Assert.AreEqual("dict", withParameters.Rows[0]["Type"]);
                Assert.AreEqual(2, Convert.ToInt32(withParameters.Rows[0]["Count"]));
            }
        }

        /// <summary>
        /// Every command parameter reaches the 'params' dictionary; '@' and ':' prefixes are trimmed
        /// </summary>
        [TestMethod]
        public void ParamsVariableContainsAllParameters()
        {
            const string script = @"import pandas as pd
params = globals()['params']
result = pd.DataFrame({
    'Keys': [','.join(sorted(params))],
    'Id': [params['id']],
    'Name': [params['name']],
    'Flag': [params['flag']]
})";

            using (var connection = TestEnvironment.CreateConnection())
            {
                var table = Execute(connection, script,
                    new PythonParameter { ParameterName = "@id", Value = 7 },
                    new PythonParameter { ParameterName = ":name", Value = "Pike" },
                    new PythonParameter { ParameterName = "flag", Value = true });

                Assert.AreEqual("flag,id,name", table.Rows[0]["Keys"]);
                Assert.AreEqual(7L, Convert.ToInt64(table.Rows[0]["Id"]));
                Assert.AreEqual("Pike", table.Rows[0]["Name"]);
                Assert.AreEqual(true, table.Rows[0]["Flag"]);
            }
        }

        /// <summary>
        /// A parameter named 'query' stays in the 'params' dictionary and does not replace the 'query' variable
        /// </summary>
        [TestMethod]
        public void ParameterNamedQueryDoesNotOverrideQueryVariable()
        {
            const string script = @"import pandas as pd
params = globals()['params']
result = pd.DataFrame({'Query': [globals()['query']], 'Param': [params['query']]})";

            using (var connection = TestEnvironment.CreateConnection())
            {
                var table = Execute(connection, script,
                    new PythonParameter { ParameterName = "query", Value = "from-parameter" });

                Assert.AreEqual(script, table.Rows[0]["Query"]);
                Assert.AreEqual("from-parameter", table.Rows[0]["Param"]);
            }
        }

        /// <summary>
        /// Parameters of the previous execution do not leak into the next one
        /// </summary>
        [TestMethod]
        public void ParamsDoNotLeakBetweenExecutions()
        {
            const string script = @"import pandas as pd
params = globals()['params']
result = pd.DataFrame({'HasAlpha': ['alpha' in params], 'HasBeta': ['beta' in params]})";

            using (var connection = TestEnvironment.CreateConnection())
            {
                var first = Execute(connection, script,
                    new PythonParameter { ParameterName = "alpha", Value = 1 });
                Assert.AreEqual(true, first.Rows[0]["HasAlpha"]);
                Assert.AreEqual(false, first.Rows[0]["HasBeta"]);

                var second = Execute(connection, script,
                    new PythonParameter { ParameterName = "beta", Value = 2 });
                Assert.AreEqual(false, second.Rows[0]["HasAlpha"]);
                Assert.AreEqual(true, second.Rows[0]["HasBeta"]);
            }
        }

        /// <summary>
        /// 'query' and 'params' can be used together: the query text filters the frame and the parameters tune it
        /// </summary>
        [TestMethod]
        public void QueryAndParamsWorkTogether()
        {
            const string script = @"import pandas as pd
params = globals()['params']
frame = pd.DataFrame({'Value': [1, 2, 3]})
result = frame.query(globals()['query']).head(params['limit'])";

            TestEnvironment.EnsureProviderOpen();

            var context = new PythonScriptContext { Query = "Value > 1" };
            context.Parameters["limit"] = 1;

            var table = PythonDataQuery.RunScript(script, context);

            Assert.AreEqual(1, table.Rows.Count);
            Assert.AreEqual(2L, Convert.ToInt64(table.Rows[0]["Value"]));
        }

        /// <summary>
        /// The 'Transfer table to script' Power Query scenario (PowerQuery - 04 workbook): the compressed Excel table
        /// is passed as the command text and decoded by the script file (TestScript03.py)
        /// </summary>
        [TestMethod]
        public void ExcelTablePayloadReachesScriptFile()
        {
            const string json =
                "{\"No\":[1,2,3],\"Name\":[\"Olivia\",\"Ava\",\"Isabella\"]," +
                "\"Value\":[0.0,0.6931471805599453,1.0986122886681098]}";
            const double expectedValue = 9.010913347279289; // 0.6931471805599453 * 13

            var builder = TestEnvironment.CreateBuilder(GetScriptFile("TestScript03.py"));
            using (var connection = new PythonConnection { ConnectionString = builder.ConnectionString })
            {
                connection.Open();

                var table = Execute(connection, CreateCompressedPayload(json));

                Assert.AreEqual(3, table.Rows.Count);
                CollectionAssert.AreEqual(
                    new[] { "No", "Name", "Value" },
                    table.Columns.Cast<DataColumn>().Select(column => column.ColumnName).ToArray());
                Assert.AreEqual(1L, Convert.ToInt64(table.Rows[0]["No"]));
                Assert.AreEqual("Ava", table.Rows[1]["Name"]);
                Assert.AreEqual(expectedValue, Convert.ToDouble(table.Rows[1]["Value"]), 1e-9);
            }
        }
    }
}
