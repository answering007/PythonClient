using System;
using System.Data;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64.Data;

namespace Pike.PythonClient.Test.Tests
{
    [TestClass]
    public class Basic
    {
        /// <summary>
        /// Basic test
        /// </summary>
        [TestMethod]
        public void ScriptFromFileTest()
        {
            //Python script file for test
            const string fileName = @"TestScript01.py";
            var scriptFile = new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsMain.Default.PythonScriptsFolder, fileName));
            if (!scriptFile.Exists) throw new FileNotFoundException("Where is the script?", scriptFile.FullName);

            //Setup python environment
            var pythonDll = new FileInfo(SettingsMain.Default.PythonDllPath);
            if (!pythonDll.Exists) throw new FileNotFoundException("Can't find python*.dll file", pythonDll.FullName);
            if (pythonDll.DirectoryName == null)
                throw new DirectoryNotFoundException("Directory name of python*.dll file can't be null");

            //Compose PATH variable
            var lib = Path.Combine(pythonDll.DirectoryName, "Lib");
            var dlls = Path.Combine(pythonDll.DirectoryName, "DLLs");
            var packages = Path.Combine(lib, "site-packages");
            var libraryBin = Path.Combine(pythonDll.DirectoryName, "Library", "bin");

            //Create connection string
            var stringBuilder = new PythonConnectionStringBuilder
            {
                File = scriptFile.FullName,
                PythonDll = pythonDll.FullName,
                PythonPath = string.Join(";", lib, dlls, packages, libraryBin)
            };

            var datatable = new DataTable();
            using (var connection = new PythonConnection())
            {
                connection.ConnectionString = stringBuilder.ConnectionString;
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    //Set query command text. It will be passed to python "query" variable
                    command.CommandText = "Hello from ADO.Net!";

                    //Set query parameters. It will be passed to python "params" variable
                    command.Parameters.Add(new PythonParameter { ParameterName = "bool", Value = true });
                    command.Parameters.Add(new PythonParameter { ParameterName = "dt", Value = DateTime.Today });
                    command.Parameters.Add(new PythonParameter { ParameterName = "double", Value = 1235.0 });
                    command.Parameters.Add(new PythonParameter { ParameterName = "int", Value = 789 });
                    command.Parameters.Add(new PythonParameter { ParameterName = "long", Value = 1024L });
                    command.Parameters.Add(new PythonParameter { ParameterName = "string", Value = "String parameter" });

                    /*
                     * Python script must have "result" variable of type pandas DataFrame.
                     * This variable data will be transferred to DbDataReader
                     */
                    using (var reader = command.ExecuteReader())
                        datatable.Load(reader);
                }
            }

            var firstValue = datatable.Rows[0][0].ToString();
            Assert.AreEqual("Pike", firstValue);
        }

        /// <summary>
        /// Basic test with external module
        /// </summary>
        [TestMethod]
        public void ScriptFromFileWithModuleTest()
        {
            //Python script file for test
            const string fileName = @"TestScript02.py";
            var scriptFile = new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsMain.Default.PythonScriptsFolder, fileName));
            if (!scriptFile.Exists) throw new FileNotFoundException("Where is the script?", scriptFile.FullName);

            //Python module
            const string moduleName = @"TestModule.py";
            var moduleFile = new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsMain.Default.PythonScriptsFolder, moduleName));
            if (!moduleFile.Exists) throw new FileNotFoundException("Where is the module?", moduleFile.FullName);

            //Setup python environment
            var pythonDll = new FileInfo(SettingsMain.Default.PythonDllPath);
            if (!pythonDll.Exists) throw new FileNotFoundException("Can't find python*.dll file", pythonDll.FullName);
            if (pythonDll.DirectoryName == null)
                throw new DirectoryNotFoundException("Directory name of python*.dll file can't be null");

            //Compose PATH variable
            var lib = Path.Combine(pythonDll.DirectoryName, "Lib");
            var dlls = Path.Combine(pythonDll.DirectoryName, "DLLs");
            var packages = Path.Combine(lib, "site-packages");
            var libraryBin = Path.Combine(pythonDll.DirectoryName, "Library", "bin");

            //Create connection string
            var stringBuilder = new PythonConnectionStringBuilder
            {
                File = scriptFile.FullName,
                PythonDll = pythonDll.FullName,
                PythonPath = string.Join(";", lib, dlls, packages, libraryBin, scriptFile.DirectoryName, moduleFile.DirectoryName)
            };

            var datatable = new DataTable();
            using (var connection = new PythonConnection())
            {
                connection.ConnectionString = stringBuilder.ConnectionString;
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    //Set query command text. It will be passed to python "query" variable
                    command.CommandText = "Hello from ADO.Net!";

                    //Set query parameters. It will be passed to python "params" variable
                    command.Parameters.Add(new PythonParameter { ParameterName = "bool", Value = true });
                    command.Parameters.Add(new PythonParameter { ParameterName = "dt", Value = DateTime.Today });
                    command.Parameters.Add(new PythonParameter { ParameterName = "double", Value = 1235.0 });
                    command.Parameters.Add(new PythonParameter { ParameterName = "int", Value = 789 });
                    command.Parameters.Add(new PythonParameter { ParameterName = "long", Value = 1024L });
                    command.Parameters.Add(new PythonParameter { ParameterName = "string", Value = "String parameter" });

                    /*
                     * Python script must have "result" variable of type pandas DataFrame.
                     * This variable data will be transferred to DbDataReader
                     */
                    using (var reader = command.ExecuteReader())
                        datatable.Load(reader);
                }
            }

            var tableValue = datatable.Rows[0][6].ToString();
            Assert.AreEqual("test", tableValue);
        }

        /// <summary>
        /// Use query text as python script
        /// </summary>
        [TestMethod]
        public void ScriptFromCommandText()
        {
            //Python script text
            const string scriptText = @"import pandas as pd

query_text = globals()['query'] if 'query' in globals() else None
query_params = globals()['params'] if 'params' in globals() else None

result = pd.DataFrame(
	[[True, 99.0],
	[True, 56.1],
	[False, 73.2],
	[False, 69.3]])";

            //Setup python environment
            var pythonDll = new FileInfo(SettingsMain.Default.PythonDllPath);
            if (!pythonDll.Exists) throw new FileNotFoundException("Can't find python*.dll file", pythonDll.FullName);
            if (pythonDll.DirectoryName == null)
                throw new DirectoryNotFoundException("Directory name of python*.dll file can't be null");

            //Compose PATH variable
            var lib = Path.Combine(pythonDll.DirectoryName, "Lib");
            var dlls = Path.Combine(pythonDll.DirectoryName, "DLLs");
            var packages = Path.Combine(lib, "site-packages");
            var libraryBin = Path.Combine(pythonDll.DirectoryName, "Library", "bin");

            //Create connection string
            var stringBuilder = new PythonConnectionStringBuilder
            {
                PythonDll = pythonDll.FullName,
                PythonPath = string.Join(";", lib, dlls, packages, libraryBin)
            };

            var datatable = new DataTable();
            using (var connection = new PythonConnection())
            {
                connection.ConnectionString = stringBuilder.ConnectionString;
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    //In this case there is no "query" global variable in python
                    command.CommandText = scriptText;

                    //Set query parameters. It will be passed to python "params" variable
                    command.Parameters.Add(new PythonParameter { ParameterName = "bool", Value = true });
                    command.Parameters.Add(new PythonParameter { ParameterName = "dt", Value = DateTime.Today });
                    command.Parameters.Add(new PythonParameter { ParameterName = "double", Value = 1235.0 });
                    command.Parameters.Add(new PythonParameter { ParameterName = "int", Value = 789 });
                    command.Parameters.Add(new PythonParameter { ParameterName = "long", Value = 1024L });
                    command.Parameters.Add(new PythonParameter { ParameterName = "string", Value = "String parameter" });

                    /*
                     * Python script must have "result" variable of type pandas DataFrame.
                     * This variable data will be transferred to DbDataReader
                     */
                    using (var reader = command.ExecuteReader())
                        datatable.Load(reader);
                }
            }

            var tableValue = (bool)datatable.Rows[0][0];
            Assert.AreEqual(true, tableValue);
        }

        /// <summary>
        /// Use virtual environment
        /// </summary>
        /// <exception cref="FileNotFoundException"></exception>
        [TestMethod]
        public void CustomEnvironmentTest()
        {
            //Python script text
            const string scriptText = @"import pandas as pd

query_text = globals()['query'] if 'query' in globals() else None
print('Query text is:', query_text)

query_params = globals()['params'] if 'params' in globals() else None
print('Query parameters:', query_params)

result = pd.DataFrame(
	[[True, 99.0],
	[True, 56.1],
	[False, 73.2],
	[False, 69.3]])";

            //Setup python environment
            var pythonDll = new FileInfo(SettingsMain.Default.EnvPythonDllPath);
            if (!pythonDll.Exists) throw new FileNotFoundException("Can't find python*.dll file", pythonDll.FullName);
            if (pythonDll.DirectoryName == null)
                throw new DirectoryNotFoundException("Directory name of python*.dll file can't be null");

            //Compose PATH variable
            var lib = Path.Combine(pythonDll.DirectoryName, "Lib");
            var dlls = Path.Combine(pythonDll.DirectoryName, "DLLs");
            var packages = Path.Combine(lib, "site-packages");
            var libraryBin = Path.Combine(pythonDll.DirectoryName, "Library", "bin");

            //Create connection string
            var stringBuilder = new PythonConnectionStringBuilder
            {
                PythonDll = pythonDll.FullName,
                PythonPath = string.Join(";", lib, dlls, packages, libraryBin)
            };

            var datatable = new DataTable();
            using (var connection = new PythonConnection())
            {
                connection.ConnectionString = stringBuilder.ConnectionString;
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    //In this case there is no "query" global variable in python
                    command.CommandText = scriptText;

                    //Set query parameters. It will be passed to python "params" variable
                    command.Parameters.Add(new PythonParameter { ParameterName = "bool", Value = true });
                    command.Parameters.Add(new PythonParameter { ParameterName = "dt", Value = DateTime.Today });
                    command.Parameters.Add(new PythonParameter { ParameterName = "double", Value = 1235.0 });
                    command.Parameters.Add(new PythonParameter { ParameterName = "int", Value = 789 });
                    command.Parameters.Add(new PythonParameter { ParameterName = "long", Value = 1024L });
                    command.Parameters.Add(new PythonParameter { ParameterName = "string", Value = "String parameter" });

                    /*
                     * Python script must have "result" variable of type pandas DataFrame.
                     * This variable data will be transferred to DbDataReader
                     */
                    using (var reader = command.ExecuteReader())
                        datatable.Load(reader);
                }
            }

            var tableValue = (bool)datatable.Rows[0][0];
            Assert.AreEqual(true, tableValue);
        }
    }
}
