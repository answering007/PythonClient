using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64;
using Python.Runtime;

namespace Pike.PythonClient.Test.Tests
{
    /// <summary>
    /// <see cref="PythonDataQuery"/> tests. Require the python runtime (PYTHONNET_TEST_DLL)
    /// </summary>
    [TestClass]
    [TestCategory("Python")]
    public class DataQuery
    {
        /// <summary>
        /// Opens the provider once per process (the engine is a process-wide singleton)
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestEnvironment.SkipIfNoPython();
            TestEnvironment.EnsureProviderOpen();
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
        /// Runs the script with the optional context
        /// </summary>
        /// <param name="scriptText">Script text</param>
        /// <param name="context">Execution context</param>
        /// <returns>Result table</returns>
        static DataTable Run(string scriptText, PythonScriptContext context = null)
        {
            return PythonDataQuery.RunScript(scriptText, context);
        }

        /// <summary>
        /// Empty DataFrame gives an empty table
        /// </summary>
        [TestMethod]
        public void TestEmptyDataFrame()
        {
            var table = Run("import pandas as pd\nresult = pd.DataFrame()");

            Assert.AreEqual(0, table.Rows.Count);
            Assert.AreEqual(0, table.Columns.Count);
        }

        /// <summary>
        /// Invalid python syntax must be reported, not swallowed
        /// </summary>
        [TestMethod]
        public void TestInvalidSyntaxInPythonCode()
        {
            Assert.ThrowsException<PythonException>(() => Run("////////"));
        }

        /// <summary>
        /// Missing result variable must be reported
        /// </summary>
        [TestMethod]
        public void TestNoResultVariableInPythonCode()
        {
            Assert.ThrowsException<KeyNotFoundException>(() => Run("import pandas as pd"));
        }

        /// <summary>
        /// Result variable can be optional (used by ExecuteNonQuery)
        /// </summary>
        [TestMethod]
        public void TestResultRequiredCanBeDisabled()
        {
            var context = new PythonScriptContext { ResultRequired = false };

            var table = Run("import pandas as pd", context);

            Assert.AreEqual(0, table.Rows.Count);
        }

        /// <summary>
        /// Custom result variable name is supported
        /// </summary>
        [TestMethod]
        public void TestCustomResultName()
        {
            var context = new PythonScriptContext { ResultName = "data" };

            var table = Run("import pandas as pd\ndata = pd.DataFrame({'Value': [42]})", context);

            Assert.AreEqual(42L, Convert.ToInt64(table.Rows[0][0]));
        }

        /// <summary>
        /// Non-DataFrame result must be reported with a clear error
        /// </summary>
        [TestMethod]
        public void TestNonDataFrameResultThrows()
        {
            Assert.ThrowsException<InvalidOperationException>(() => Run("result = [1, 2, 3]"));
        }

        /// <summary>
        /// Script is executed multiple times with the same result
        /// </summary>
        /// <param name="numberOfRuns">Number of runs</param>
        [TestMethod]
        [DataRow((byte)3)]
        public void TestScriptAsCommandText(byte numberOfRuns)
        {
            const string code = @"import pandas as pd
result = pd.DataFrame({'Value': [1, 2, 3]})";

            for (var i = 0; i < numberOfRuns; i++)
            {
                var table = Run(code);

                Assert.AreEqual(3, table.Rows.Count);
                Assert.AreEqual(3L, Convert.ToInt64(table.Rows[2][0]));
            }
        }

        /// <summary>
        /// Query text is passed to the script as the 'query' variable
        /// </summary>
        [TestMethod]
        public void TestQueryTextIsPassedToScript()
        {
            const string code = @"import pandas as pd
query_text = globals()['query'] if 'query' in globals() else None
result = pd.DataFrame({'Query': [query_text]})";

            var table = Run(code, new PythonScriptContext { Query = "Hello from test" });

            Assert.AreEqual("Hello from test", table.Rows[0][0]);
        }

        /// <summary>
        /// Parameters are passed to the script as the 'params' dictionary
        /// </summary>
        [TestMethod]
        public void TestParametersAndColumnNames()
        {
            const string code = @"import pandas as pd
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

            var context = new PythonScriptContext();
            foreach (var parameter in parameters)
                context.Parameters[parameter.Key] = parameter.Value;

            var table = Run(code, context);

            CollectionAssert.AreEqual(
                parameters.Keys.ToArray(),
                table.Columns.Cast<DataColumn>().Select(column => column.ColumnName).ToArray());
            Assert.AreEqual("Pike", table.Rows[0]["StringValue"]);
            Assert.AreEqual(true, table.Rows[0]["BoolValue"]);
            Assert.AreEqual(123.456, table.Rows[0]["FloatValue"]);
            Assert.AreEqual(123456L, Convert.ToInt64(table.Rows[0]["IntValue"]));
            Assert.AreEqual(new DateTime(2020, 12, 21), table.Rows[0]["DateTimeValue"]);
        }

        /// <summary>
        /// Null and DBNull parameters are transferred as None
        /// </summary>
        [TestMethod]
        public void TestNullParametersBecomeNone()
        {
            const string code = @"import pandas as pd
params = globals()['params']
result = pd.DataFrame({'NullValue': [params['null']], 'DbNullValue': [params['dbnull']]})";

            var context = new PythonScriptContext();
            context.Parameters["null"] = null;
            context.Parameters["dbnull"] = DBNull.Value;

            var table = Run(code, context);

            Assert.AreEqual(DBNull.Value, table.Rows[0]["NullValue"]);
            Assert.AreEqual(DBNull.Value, table.Rows[0]["DbNullValue"]);
        }

        /// <summary>
        /// Large DataFrame is transferred without errors
        /// </summary>
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

            var table = Run(code);

            Assert.AreEqual(90000, table.Rows.Count);
        }
    }
}
