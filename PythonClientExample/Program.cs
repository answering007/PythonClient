using System;
using System.Data;
using System.IO;
using System.Linq;
using Pike.PythonClient64.Data;

namespace PythonClientExample
{
    class Program
    {
        static void PrintDataTable(DataTable dataTable)
        {
            var columnLine = string.Join("\t", dataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
            Console.WriteLine(columnLine);
            foreach (var row in dataTable.Rows.Cast<DataRow>())
            {
                var rowLine = string.Join("\t", row.ItemArray);
                Console.WriteLine(rowLine);
            }
        }

        static void Main()
        {
            //TestScript01();
            //TestScript03();
            //TestScript04();
            //TestScript05();
        }

        /// <summary>
        /// Basic anaconda test
        /// </summary>
        public static void TestScript01()
        {
            //Python script file for test
            const string fileName = @"TestScript01.py";
            var scriptFile = new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName));
            if (!scriptFile.Exists) throw new FileNotFoundException("Where is the script?", scriptFile.FullName);

            //Setup python environment
            const string pythonHome = @"C:\Users\Pike\anaconda3";   //<-- Replace it with your own path to anaconda
            //Compose PATH environment variable
            var lib = Path.Combine(pythonHome, "Lib");
            var dlls = Path.Combine(pythonHome, "DLLs");
            var packages = Path.Combine(lib, "site-packages");
            var libraryBin = Path.Combine(pythonHome, "Library", "bin");

            //Create connection string
            var stringBuilder = new PythonConnectionStringBuilder
            {
                File = scriptFile.FullName,
                PythonHome = pythonHome,
                Path = string.Join(";", pythonHome, lib, dlls, packages, libraryBin)
            };

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
                     * This variable data will be transfered to DbDataReader
                     */
                    using (var reader = command.ExecuteReader())
                    {
                        var datatable = new DataTable();
                        datatable.Load(reader);

                        //Print data
                        PrintDataTable(datatable);
                    }
                }
            }

            Console.WriteLine("Done!");
            Console.ReadLine();
        }

        /// <summary>
        /// Use query text as python script
        /// </summary>
        public static void TestScript03()
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
            const string pythonHome = @"C:\Users\Pike\anaconda3";   //<-- Replace it with your own path to anaconda
            //Compose PATH environment variable
            var lib = Path.Combine(pythonHome, "Lib");
            var dlls = Path.Combine(pythonHome, "DLLs");
            var packages = Path.Combine(lib, "site-packages");
            var libraryBin = Path.Combine(pythonHome, "Library", "bin");

            //Create connection string
            var stringBuilder = new PythonConnectionStringBuilder
            {
                PythonHome = pythonHome,
                Path = string.Join(";", pythonHome, lib, dlls, packages, libraryBin)
            };

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
                     * This variable data will be transfered to DbDataReader
                     */
                    using (var reader = command.ExecuteReader())
                    {
                        var datatable = new DataTable();
                        datatable.Load(reader);

                        //Print data
                        PrintDataTable(datatable);
                    }
                }
            }

            Console.WriteLine("Done!");
            Console.ReadLine();
        }

        /// <summary>
        /// Basic python test (environment variables already set by python installer)
        /// </summary>
        public static void TestScript04()
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
            const string pythonHome = @"C:\Python37";   //<-- Replace it with your own path to python 3.7

            //Create connection string
            var stringBuilder = new PythonConnectionStringBuilder   //<-- No need to set File property
            {
                PythonHome = pythonHome
            };

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
                     * This variable data will be transfered to DbDataReader
                     */
                    using (var reader = command.ExecuteReader())
                    {
                        var datatable = new DataTable();
                        datatable.Load(reader);

                        //Print data
                        PrintDataTable(datatable);
                    }
                }
            }

            Console.WriteLine("Done!");
            Console.ReadLine();
        }

        /// <summary>
        /// Use anaconda virtual environment
        /// </summary>
        public static void TestScript05()
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
            const string pythonHome = @"C:\Users\Pike\anaconda3\envs\Test";   //<-- Replace it with your own path to anaconda virtual environment
            //Compose PATH environment variable
            var lib = Path.Combine(pythonHome, "Lib");
            var dlls = Path.Combine(pythonHome, "DLLs");
            var packages = Path.Combine(lib, "site-packages");
            var libraryBin = Path.Combine(pythonHome, "Library", "bin");

            //Create connection string
            var stringBuilder = new PythonConnectionStringBuilder
            {
                PythonHome = pythonHome,
                Path = string.Join(";", pythonHome, lib, dlls, packages, libraryBin)
            };

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
                     * This variable data will be transfered to DbDataReader
                     */
                    using (var reader = command.ExecuteReader())
                    {
                        var datatable = new DataTable();
                        datatable.Load(reader);

                        //Print data
                        PrintDataTable(datatable);
                    }
                }
            }

            Console.WriteLine("Done!");
            Console.ReadLine();
        }
    }
}