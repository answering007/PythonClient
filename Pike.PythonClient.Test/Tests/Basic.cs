using System;
using System.Data;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64.Data;

namespace Pike.PythonClient.Test.Tests
{
    /// <summary>
    /// Basic provider usage tests. Require the python runtime (PYTHONNET_TEST_DLL)
    /// </summary>
    [TestClass]
    [TestCategory("Python")]
    public class Basic
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
        /// Returns the full path to the test python script
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
        /// Opens the connection. When the script file name is specified, the connection uses it as the script
        /// </summary>
        /// <param name="fileName">Script file name or null/empty to use the command text as a script</param>
        /// <param name="addScriptFolderToPath">True to add the script directory to sys.path (needed for modules)</param>
        /// <returns>Opened connection</returns>
        static PythonConnection OpenConnection(string fileName = null, bool addScriptFolderToPath = false)
        {
            var builder = TestEnvironment.CreateBuilder();
            if (!string.IsNullOrEmpty(fileName))
            {
                var scriptFile = GetScriptFile(fileName);
                builder.File = scriptFile;

                if (addScriptFolderToPath)
                    builder.PythonPath = string.Join(";", builder.PythonPath, Path.GetDirectoryName(scriptFile));
            }

            var connection = new PythonConnection { ConnectionString = builder.ConnectionString };
            connection.Open();

            return connection;
        }

        /// <summary>
        /// Adds the parameters used by the test scripts
        /// </summary>
        /// <param name="command">Target command</param>
        static void AddTestParameters(System.Data.Common.DbCommand command)
        {
            command.Parameters.Add(new PythonParameter { ParameterName = "bool", Value = true });
            command.Parameters.Add(new PythonParameter { ParameterName = "dt", Value = DateTime.Today });
            command.Parameters.Add(new PythonParameter { ParameterName = "double", Value = 1235.0 });
            command.Parameters.Add(new PythonParameter { ParameterName = "int", Value = 789 });
            command.Parameters.Add(new PythonParameter { ParameterName = "long", Value = 1024L });
            command.Parameters.Add(new PythonParameter { ParameterName = "string", Value = "String parameter" });
        }

        /// <summary>
        /// Executes the command text and loads the result into a new table
        /// </summary>
        /// <param name="connection">Opened connection</param>
        /// <param name="commandText">Command text (query text for the script)</param>
        /// <param name="singleRow">True to read the first row only</param>
        /// <returns>Loaded table</returns>
        static DataTable Execute(PythonConnection connection, string commandText, bool singleRow = false)
        {
            var dataTable = new DataTable();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = commandText;
                AddTestParameters(command);

                var behavior = singleRow ? CommandBehavior.SingleRow : CommandBehavior.Default;
                using (var reader = command.ExecuteReader(behavior)) dataTable.Load(reader);
            }

            return dataTable;
        }

        /// <summary>
        /// Script from the file with the query text and parameters
        /// </summary>
        [TestMethod]
        public void ScriptFromFileTest()
        {
            using (var connection = OpenConnection("TestScript01.py"))
            {
                var dataTable = Execute(connection, "Hello from ADO.Net!");

                Assert.AreEqual(6, dataTable.Columns.Count);
                Assert.AreEqual(3, dataTable.Rows.Count);
                Assert.AreEqual("Pike", dataTable.Rows[0][0].ToString());
            }
        }

        /// <summary>
        /// Script from the file that imports an external module
        /// </summary>
        [TestMethod]
        public void ScriptFromFileWithModuleTest()
        {
            using (var connection = OpenConnection("TestScript02.py", true))
            {
                var dataTable = Execute(connection, "Hello from ADO.Net!");

                Assert.AreEqual("test", dataTable.Rows[0][6].ToString());
            }
        }

        /// <summary>
        /// Script from the command text
        /// </summary>
        [TestMethod]
        public void ScriptFromCommandText()
        {
            const string scriptText = @"import pandas as pd

query_text = globals()['query'] if 'query' in globals() else None
query_params = globals()['params'] if 'params' in globals() else None

result = pd.DataFrame(
	[[True, 99.0],
	[True, 56.1],
	[False, 73.2],
	[False, 69.3]])";

            using (var connection = OpenConnection())
            {
                var dataTable = Execute(connection, scriptText);

                Assert.AreEqual(4, dataTable.Rows.Count);
                Assert.AreEqual(true, dataTable.Rows[0][0]);
            }
        }

        /// <summary>
        /// Provider works with the runtime configured by the environment variable
        /// </summary>
        [TestMethod]
        public void CustomEnvironmentTest()
        {
            const string scriptText = @"import pandas as pd

result = pd.DataFrame(
	[[True, 99.0],
	[True, 56.1],
	[False, 73.2],
	[False, 69.3]])";

            using (var connection = OpenConnection())
            {
                var dataTable = Execute(connection, scriptText);

                Assert.AreEqual(true, dataTable.Rows[0][0]);
            }
        }
    }
}
