using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Pike.PythonClient.Test.Tests
{
    [TestClass]
    public class UnitTest1
    {
        private readonly object _locker = new object();

        /// <summary>
        /// Gets or sets the test context which provides
        /// information about and functionality for the current test run.
        /// </summary>
        public TestContext TestContext { get; set; }

        private int Test1()
        {
            lock (_locker)
            {
                return 1 + 2;
            }
        }

        private int Test2()
        {
            lock (_locker)
            {
                return 3 + 4;
            }
        }

        [TestMethod]
        public void TestMethod1()
        {
            TestContext.WriteLine(Test1().ToString());
            TestContext.WriteLine(Test2().ToString());
        }
    }
}
