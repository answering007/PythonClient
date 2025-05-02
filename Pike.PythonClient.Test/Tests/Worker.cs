using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Pike.PythonClient.Test.Tests
{
    [TestClass]
    public class Worker
    {
        /// <summary>
        /// Gets or sets the test context which provides
        /// information about and functionality for the current test run.
        /// </summary>
        public TestContext TestContext { get; set; }

        [TestMethod]
        [DataRow(5)]
        public void TestMethod1(int numberOfActions)
        {
            var worker = new WorkerTask();

            var results = new int[numberOfActions];

            for (var i = 0; i < results.Length; i++)
            {
                var i1 = i;
                worker.RunAction(() =>
                {
                    results[i1] = i1;
                    //TestContext.WriteLine(results[i1].ToString());
                    TestContext.WriteLine(i1.ToString());
                    //Thread.Sleep(50);
                });
            }

            var result = results.SequenceEqual(Enumerable.Range(0, numberOfActions));
            Assert.AreEqual(true, result);
        }
    }
}
