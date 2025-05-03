using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64.Data;
using System.Data;
using System.Data.Common;
using System.Diagnostics;

namespace Pike.PythonClient.Test.Tests
{
    [TestClass]
    public class DbProviderFactory
    {
        string _factoryName;

        /// <summary>
        /// Initialize the test class by setting up the factory name
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            _factoryName = typeof(PythonProviderFactory).FullName;
        }
        
        /// <summary>
        /// Test whether the connection can be opened and closed.
        /// </summary>
        /// <remarks>
        /// Tests that the connection can be opened and closed. This is a basic test
        /// that the connection can be established and is a pre-requisite for all
        /// other tests.
        /// </remarks>
        [TestMethod]
        public void ConnectionCanBeOpenedAndClosed()
        {
            var builder = new PythonConnectionStringBuilder { PythonDll = SettingsMain.Default.PythonDllPath };
            var factory = DbProviderFactories.GetFactory(_factoryName);
            bool isOpened;
            using (var dbConnection = factory.CreateConnection())
            {
                Debug.Assert(dbConnection != null, nameof(dbConnection) + " != null");

                dbConnection.ConnectionString = builder.ConnectionString;
                dbConnection.Open();
                isOpened = dbConnection.State == ConnectionState.Open;
            }
            Assert.AreEqual(true, isOpened);
        }

        /// <summary>
        /// Test that the ExecuteScalar method runs the query as expected and returns the correct value.
        /// </summary>
        /// <remarks>
        /// This test makes sure that the ExecuteScalar method runs the query as expected and returns the correct value.
        /// </remarks>
        [TestMethod]
        public void TestScriptAsCommandText()
        {
            const string query = @"import pandas as pd

result = pd.DataFrame({'StringColumn': ['Pike']})";

            var builder = new PythonConnectionStringBuilder { PythonDll = SettingsMain.Default.PythonDllPath };
            var factory = DbProviderFactories.GetFactory(_factoryName);
            using (var dbConnection = factory.CreateConnection())
            {
                Debug.Assert(dbConnection != null, nameof(dbConnection) + " != null");

                dbConnection.ConnectionString = builder.ConnectionString;
                dbConnection.Open();

                using (var dbCommand = dbConnection.CreateCommand())
                {
                    dbCommand.Connection = dbConnection;
                    dbCommand.CommandText = query;

                    var value = dbCommand.ExecuteScalar();
                    Assert.AreEqual("Pike", (string)value);
                }
            }
        }
    }
}
