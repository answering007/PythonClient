using System;
using System.Data;
using System.Data.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64.Data;

namespace Pike.PythonClient.Test.Tests
{
    /// <summary>
    /// <see cref="PythonProviderFactory"/> tests. Require the provider to be registered in machine.config
    /// and the python runtime (PYTHONNET_TEST_DLL)
    /// </summary>
    [TestClass]
    [TestCategory("Python")]
    public class DbProviderFactory
    {
        string _factoryName;

        /// <summary>
        /// Python and provider registration checks
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestEnvironment.SkipIfNoPython();
            _factoryName = typeof(PythonProviderFactory).FullName;

            // The provider must be registered in machine.config by the setup
            try
            {
                DbProviderFactories.GetFactory(_factoryName);
            }
            catch (ArgumentException exception)
            {
                Assert.Inconclusive($"Provider '{_factoryName}' is not registered in machine.config: {exception.Message}");
            }
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
        /// Connection can be created by the factory and opened
        /// </summary>
        [TestMethod]
        public void ConnectionCanBeOpenedAndClosed()
        {
            var factory = DbProviderFactories.GetFactory(_factoryName);
            using (var dbConnection = factory.CreateConnection())
            {
                if (dbConnection == null) throw new InvalidOperationException("'dbConnection' can't be null");

                dbConnection.ConnectionString = TestEnvironment.CreateBuilder().ConnectionString;
                dbConnection.Open();

                Assert.AreEqual(ConnectionState.Open, dbConnection.State);
            }
        }

        /// <summary>
        /// ExecuteScalar runs the script and returns the first value
        /// </summary>
        [TestMethod]
        public void TestScriptAsCommandText()
        {
            const string query = @"import pandas as pd

result = pd.DataFrame({'StringColumn': ['Pike']})";

            var factory = DbProviderFactories.GetFactory(_factoryName);
            using (var dbConnection = factory.CreateConnection())
            {
                if (dbConnection == null) throw new InvalidOperationException("'dbConnection' can't be null");

                dbConnection.ConnectionString = TestEnvironment.CreateBuilder().ConnectionString;
                dbConnection.Open();

                using (var dbCommand = dbConnection.CreateCommand())
                {
                    dbCommand.CommandText = query;

                    Assert.AreEqual("Pike", dbCommand.ExecuteScalar());
                }
            }
        }

        /// <summary>
        /// Factory creates parameters with the expected default state (P1-2)
        /// </summary>
        [TestMethod]
        public void FactoryCreatesParameter()
        {
            var factory = DbProviderFactories.GetFactory(_factoryName);
            var parameter = factory.CreateParameter();

            Assert.IsInstanceOfType(parameter, typeof(PythonParameter));
            Assert.IsTrue(parameter.IsNullable);
        }
    }
}
