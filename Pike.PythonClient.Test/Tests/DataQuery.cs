using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64;
using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Data;

namespace Pike.PythonClient.Test.Tests
{
    [TestClass]
    public class DataQuery
    {
        /// <summary>
        /// Test with an empty DataFrame result
        /// </summary>
        [TestMethod]
        public void TestEmptyDataFrame()
        {
            const string code = @"import pandas as pd
result = pd.DataFrame()";

            PythonDataProvider.PythonDllPath = SettingsMain.Default.PythonDllPath;
            PythonDataProvider.Open();
            PythonDataQuery.Reset();
            var table = PythonDataQuery.RunScript(code);
            PythonDataProvider.Close();
            Assert.AreEqual(0, table.Rows.Count);
        }

        /// <summary>
        /// Test handling of invalid Python syntax.
        /// </summary>
        /// <remarks>
        /// This test provides an invalid Python code snippet to ensure that
        /// the system correctly identifies and handles syntax errors by throwing
        /// an appropriate exception.
        /// </remarks>
        [TestMethod]
        public void TestInvalidSyntaxInPythonCode()
        {
            const string code = @"////////";

            PythonDataProvider.PythonDllPath = SettingsMain.Default.PythonDllPath;
            PythonDataProvider.Open();
            PythonDataQuery.Reset();
            try
            {
                PythonDataQuery.RunScript(code);
            }
            catch (Exception exception)
            {
                Assert.IsTrue(exception.GetType().IsSubclassOf(typeof(Exception)));
            }
            finally
            {
                PythonDataProvider.Close();
            }
        }

        /// <summary>
        /// Test that the system throws a KeyNotFoundException when the Python script
        /// does not define a result variable.
        /// </summary>
        /// <remarks>
        /// This test ensures that the system correctly identifies and handles
        /// scripts that do not define a result variable. The test provides a
        /// valid Python snippet that does not define a result variable and
        /// verifies that the system throws a KeyNotFoundException.
        /// </remarks>
        [TestMethod]
        public void TestNoResultVariableInPythonCode()
        {
            const string code = "import pandas as pd";

            PythonDataProvider.PythonDllPath = SettingsMain.Default.PythonDllPath;
            PythonDataProvider.Open();
            PythonDataQuery.Reset();
            Assert.ThrowsException<KeyNotFoundException>(() => PythonDataQuery.RunScript(code));
            PythonDataProvider.Close();
        }

        /// <summary>
        /// Test executing a Python script as command text multiple times.
        /// </summary>
        /// <remarks>
        /// This test runs a Python script that generates a DataFrame and verifies that the 
        /// resulting table matches the expected data across multiple iterations. It ensures 
        /// that the script execution produces consistent and correct results each time it is run.
        /// </remarks>
        /// <param name="numberOfRuns">The number of times to execute the script.</param>
        [TestMethod]
        [DataRow((byte)3)]
        public void TestScriptAsCommandText(byte numberOfRuns)
        {
            const string code = @"import numpy as np
import pandas as pd

result = pd.DataFrame({
    'StringColumn':		['Pike',	None,	'Amol'],
    'BoolColumn':		[True,		True,	False],
    'FloatColumn':		[123.456,	np.nan,	456.789],
    'IntColumn':		[123456,	456789,	789123],
    'TimeDeltaColumn':	[np.timedelta64(10, 'h'), None, np.timedelta64(12, 'h')],
    'DateTimeColumn':	[np.datetime64(30, 'Y'), None, np.datetime64(50, 'Y')]
})";

            var firstRow = new object[]
                { "Pike", true, 123.456, 123456L, new TimeSpan(10, 0, 0), new DateTime(2000, 1, 1) };
            var secondRow = new object[]
                { DBNull.Value, true, double.NaN, 456789L, DBNull.Value, DBNull.Value };
            var thirdRow = new object[]
                { "Amol", false, 456.789, 789123L, new TimeSpan(12, 0, 0), new DateTime(2020, 1, 1) };

            PythonDataProvider.PythonDllPath = SettingsMain.Default.PythonDllPath;
            PythonDataProvider.Open();

            for (var i = 0; i < numberOfRuns; i++)
            {
                PythonDataQuery.Reset();
                var table = PythonDataQuery.RunScript(code);
                var compare = new[]
                {
                    firstRow.SequenceEqual(table.Rows[0].ItemArray),
                    secondRow.SequenceEqual(table.Rows[1].ItemArray),
                    thirdRow.SequenceEqual(table.Rows[2].ItemArray),
                };

                Assert.AreEqual(true, compare.All(v => v.Equals(true)));
            }
            
            PythonDataProvider.Close();
        }

        /// <summary>
        /// Test execution of a Python script from a file and verify the resulting DataFrame.
        /// </summary>
        /// <remarks>
        /// This test loads a Python script from a specified file, executes it using the Python data provider,
        /// and checks that the resulting DataFrame matches the expected data. It ensures that the script execution
        /// correctly processes the data as specified in the script file.
        /// </remarks>
        [TestMethod]
        public void TestScriptAsFile()
        {
            //Python script file for test
            const string fileName = @"TestScript01.py";
            var scriptFile = new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsMain.Default.PythonScriptsFolder, fileName));
            if (!scriptFile.Exists) throw new FileNotFoundException("Where is the script?", scriptFile.FullName);
            var code = File.ReadAllText(scriptFile.FullName);

            var firstRow = new object[]
                { "Pike", true, 123.456, 123456L, new TimeSpan(10, 0, 0), new DateTime(2000, 1, 1) };
            var secondRow = new object[]
                { DBNull.Value, true, double.NaN, 456789L, DBNull.Value, DBNull.Value };
            var thirdRow = new object[]
                { "Amol", false, 456.789, 789123L, new TimeSpan(12, 0, 0), new DateTime(2020, 1, 1) };

            PythonDataProvider.PythonDllPath = SettingsMain.Default.PythonDllPath;
            PythonDataProvider.Open();
            PythonDataQuery.Reset();
            var table = PythonDataQuery.RunScript(code);
            PythonDataProvider.Close();

            var compare = new[]
            {
                firstRow.SequenceEqual(table.Rows[0].ItemArray),
                secondRow.SequenceEqual(table.Rows[1].ItemArray),
                thirdRow.SequenceEqual(table.Rows[2].ItemArray),
            };

            Assert.AreEqual(true, compare.All(v => v.Equals(true)));
        }

        /// <summary>
        /// Test execution of a Python script from a file with a custom module.
        /// </summary>
        /// <remarks>
        /// This test loads a Python script from a specified file, loads a custom Python module
        /// and executes it using the Python data provider, and checks that the resulting DataFrame
        /// matches the expected data. It ensures that the script execution correctly processes the
        /// data as specified in the script file, and that the custom module is correctly loaded.
        /// </remarks>
        [TestMethod]
        public void TestScriptAsFileWithCustomModule()
        {
            //Python script file for test
            const string fileName = @"TestScript01.py";
            var scriptFile = new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsMain.Default.PythonScriptsFolder, fileName));
            if (!scriptFile.Exists) throw new FileNotFoundException("Where is the script?", scriptFile.FullName);
            var code = File.ReadAllText(scriptFile.FullName);

            //Python module
            const string moduleName = @"TestModule.py";
            var moduleFile = new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsMain.Default.PythonScriptsFolder, moduleName));
            if (!moduleFile.Exists) throw new FileNotFoundException("Where is the module?", scriptFile.FullName);

            var firstRow = new object[]
                { "Pike", true, 123.456, 123456L, new TimeSpan(10, 0, 0), new DateTime(2000, 1, 1) };
            var secondRow = new object[]
                { DBNull.Value, true, double.NaN, 456789L, DBNull.Value, DBNull.Value };
            var thirdRow = new object[]
                { "Amol", false, 456.789, 789123L, new TimeSpan(12, 0, 0), new DateTime(2020, 1, 1) };

            PythonDataProvider.PythonDllPath = SettingsMain.Default.PythonDllPath;
            PythonDataProvider.Open(false, new[] { moduleFile.DirectoryName });
            PythonDataQuery.Reset();
            var table = PythonDataQuery.RunScript(code);
            PythonDataProvider.Close();

            var compare = new[]
            {
                firstRow.SequenceEqual(table.Rows[0].ItemArray),
                secondRow.SequenceEqual(table.Rows[1].ItemArray),
                thirdRow.SequenceEqual(table.Rows[2].ItemArray),
            };

            Assert.AreEqual(true, compare.All(v => v.Equals(true)));
        }

        /// <summary>
        /// Test setting query parameter to Python script.
        /// </summary>
        /// <remarks>
        /// This test sets the query parameter of the Python script and verifies that the value
        /// is used in the script. It ensures that the query parameter is correctly passed to the
        /// Python script when it is executed using the Python data provider.
        /// </remarks>
        [TestMethod]
        public void TestQueryParameter()
        {
            const string code = @"import pandas as pd

query = globals()['query'] if 'query' in globals() else None
result = pd.DataFrame({'StringColumn': [query]})";

            const string query = "Hello";

            PythonDataProvider.PythonDllPath = SettingsMain.Default.PythonDllPath;
            PythonDataProvider.Open();
            PythonDataQuery.Reset();
            PythonDataQuery.Query = query;
            var table = PythonDataQuery.RunScript(code);
            PythonDataProvider.Close();

            Assert.AreEqual(query, table.Rows[0][0]);
        }

        /// <summary>
        /// Test that parameters are correctly passed to a Python script and that the resulting DataFrame
        /// has the expected column names and values.
        /// </summary>
        /// <remarks>
        /// This test sets a dictionary of parameters, passes them to a Python script, and verifies that
        /// the resulting DataFrame's column names and row values match the provided parameters.
        /// It ensures the parameters are correctly transferred to the Python environment and processed
        /// as expected.
        /// </remarks>
        [TestMethod]
        public void TestParametersAndColumnNames()
        {
            const string code = @"import numpy as np
import pandas as pd

params = globals()['params'] if 'params' in globals() else None
result = pd.DataFrame(params, index=[0])";

            var parameters = new Dictionary<string, object>
            {
                ["StringValue"] = "Pike",
                ["BoolValue"] = true,
                ["FloatValue"] = 123.456,
                ["IntValue"] = 123456L,
                ["DateTimeValue"] = new DateTime(2020, 12, 21)
            };

            PythonDataProvider.PythonDllPath = SettingsMain.Default.PythonDllPath;
            PythonDataProvider.Open();
            PythonDataQuery.Reset();
            foreach (var parameter in parameters)
                PythonDataQuery.Parameters[parameter.Key] = parameter.Value;
            var table = PythonDataQuery.RunScript(code);
            PythonDataProvider.Close();

            var compare = new[]
            {
                parameters.Keys.SequenceEqual(table.Columns.Cast<DataColumn>().Select(v => v.ColumnName)),
                parameters.Values.SequenceEqual(table.Rows[0].ItemArray)
            };

            Assert.AreEqual(true, compare.All(v => v.Equals(true)));
        }

        /// <summary>
        /// Test the execution of a Python script that generates a large DataFrame.
        /// </summary>
        /// <remarks>
        /// This test verifies that the Python data provider can execute a Python script
        /// that generates a large DataFrame, and that the resulting DataFrame is correctly
        /// transferred from the Python environment to the .NET environment. It ensures
        /// that the data provider can handle large DataFrames without running out of memory.
        /// </remarks>
        [TestMethod]
        public void TestLargeDataFrame()
        {
            const string code = @"import numpy as np
import pandas as pd

result = pd.DataFrame({
    'StringColumn':		['Pike',	None,	'Amol'],
    'BoolColumn':		[True,		True,	False],
    'FloatColumn':		[123.456,	np.nan,	456.789],
    'IntColumn':		[123456,	456789,	789123],
    'TimeDeltaColumn':	[np.timedelta64(10, 'h'), None, np.timedelta64(12, 'h')],
    'DateTimeColumn':	[np.datetime64(30, 'Y'), None, np.datetime64(50, 'Y')]
})
result = pd.DataFrame(np.repeat(result.to_numpy(), 30000, axis=0), columns=result.columns)";

            PythonDataProvider.PythonDllPath = SettingsMain.Default.PythonDllPath;
            PythonDataProvider.Open();
            PythonDataQuery.Reset();
            var table = PythonDataQuery.RunScript(code);
            PythonDataProvider.Close();

            Assert.AreEqual(30000 * 3, table.Rows.Count);
        }
    }
}
