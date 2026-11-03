using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64;

namespace Pike.PythonClient.Test.Tests.Unit
{
    /// <summary>
    /// Unit tests for <see cref="PythonScriptContext"/> and the names of the python script variables.
    /// The tests do not require Python runtime
    /// </summary>
    [TestClass]
    [TestCategory("Unit")]
    public class ScriptContextUnit
    {
        /// <summary>
        /// The default context passes no query text, no parameters and requires the 'result' variable
        /// </summary>
        [TestMethod]
        public void ContextHasDocumentedDefaults()
        {
            var context = new PythonScriptContext();

            Assert.IsNull(context.Query);
            Assert.IsNotNull(context.Parameters);
            Assert.AreEqual(0, context.Parameters.Count);
            Assert.AreEqual(PythonDataQuery.ResultKey, context.ResultName);
            Assert.IsTrue(context.ResultRequired);
        }

        /// <summary>
        /// The 'query' and 'params' variable names are a public contract with python scripts
        /// </summary>
        [TestMethod]
        public void VariableNamesAreStableContract()
        {
            Assert.AreEqual("query", PythonDataQuery.QueryKey);
            Assert.AreEqual("params", PythonDataQuery.PythonParametersKey);
            Assert.AreEqual("result", PythonDataQuery.ResultKey);
        }

        /// <summary>
        /// Null and DBNull parameter values are accepted by the context and kept under their names
        /// </summary>
        [TestMethod]
        public void ParametersAcceptNullAndDbNull()
        {
            var context = new PythonScriptContext();

            context.Parameters["null"] = null;
            context.Parameters["dbnull"] = DBNull.Value;
            context.Parameters["query"] = "value";

            Assert.AreEqual(3, context.Parameters.Count);
            Assert.IsNull(context.Parameters["null"]);
            Assert.AreEqual(DBNull.Value, context.Parameters["dbnull"]);
            Assert.AreEqual("value", context.Parameters["query"]);
        }
    }
}
