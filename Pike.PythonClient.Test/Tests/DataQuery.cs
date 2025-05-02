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
        [TestMethod]
        public void TestEmptyDataFrame()
        {
            const string code = @"import pandas as pd
result = pd.DataFrame()";

            PythonDataProvider.PythonDllPath = SettingsMain.Default.PythonDllPath;
            PythonDataProvider.Open();
            var table = PythonDataQuery.RunScript(code);
            PythonDataProvider.Close();
            Assert.AreEqual(0, table.Rows.Count);
        }

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
    }
}
