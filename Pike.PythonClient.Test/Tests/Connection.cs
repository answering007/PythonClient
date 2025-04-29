using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64.Data;

namespace Pike.PythonClient.Test.Tests
{
    [TestClass]
    public class Connection
    {
        const string PathToPythonDll = @"C:\Users\Pike\anaconda3\python311.dll";

        [TestMethod]
        public void ConnectionCanBeOpenedAndClosed()
        {
            var builder = new PythonConnectionStringBuilder { PythonDll = PathToPythonDll };
            bool isOpened;
            using (var connection = new PythonConnection())
            {
                connection.ConnectionString = builder.ConnectionString;
                connection.Open();
                isOpened = connection.State == ConnectionState.Open;
            }
            Assert.AreEqual(true, isOpened);
        }

        [TestMethod]
        [DataRow((byte)20)]
        public void ConnectionCanBeOpenedAndClosedMultipleTimes(byte numberOfRuns)
        {
            var builder = new PythonConnectionStringBuilder { PythonDll = PathToPythonDll };

            var isOpened = new bool[numberOfRuns];
            for (var i = 0; i < numberOfRuns; i++)
            {
                using (var connection = new PythonConnection())
                {
                    connection.ConnectionString = builder.ConnectionString;
                    connection.Open();
                    isOpened[i] = connection.State == ConnectionState.Open;
                }
            }
            var allTrue = isOpened.All(v => v.Equals(true));
            Assert.AreEqual(true, allTrue);
        }
    }
}
