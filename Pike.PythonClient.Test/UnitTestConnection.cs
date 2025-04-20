using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64.Data;
using System;
using System.Data;
using System.IO;

namespace Pike.PythonClient.Test
{
    [TestClass]
    public class UnitTestConnection
    {
        [TestMethod]
        public void TestMethod1()
        {
            //Python script file for test
            const string fileName = @"TestScript01.py";
            var scriptFile = new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName));
            if (!scriptFile.Exists) throw new FileNotFoundException("Where is the script?", scriptFile.FullName);

            //Setup python environment
            var pythonDll = new FileInfo(@"C:\Users\Pike\anaconda3\python311.dll");   //<-- Replace it with your own path to python.dll
            if (!pythonDll.Exists) throw new FileNotFoundException("Can'r find python*.dll file", pythonDll.FullName);

            //Compose PATH variable
            var lib = Path.Combine(pythonDll.DirectoryName, "Lib");
            var dlls = Path.Combine(pythonDll.DirectoryName, "DLLs");
            var packages = Path.Combine(lib, "site-packages");
            var libraryBin = Path.Combine(pythonDll.DirectoryName, "Library", "bin");
            var pythonPath = string.Join(";", lib, dlls, packages, libraryBin);

            var datatable = PythonConnection.ExecutePythonScript(pythonDll.FullName, File.ReadAllText(scriptFile.FullName), pythonPath);
            var firstValue = datatable.Rows[0][0].ToString();
            Assert.AreEqual("Pike", firstValue);
        }
    }
}
