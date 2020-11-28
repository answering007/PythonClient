# PythonClient
Ado.net provider that uses python script as the datasource. This project is based on Python 3.7 x64 and [pythonnet](https://github.com/pythonnet/pythonnet)
## Quik example
### C#
```C#
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
```
### Python script
```Python
query_text = globals()['query'] if 'query' in globals() else None
print("Query text is:", query_text)

query_params = globals()['params'] if 'params' in globals() else None
print("Query parameters:", query_params)

import numpy as np
import pandas as pd

result = pd.DataFrame(
	[['Pike', True, 99.0, 78, np.timedelta64(10, 'h'), np.datetime64(30, 'Y')],
	[None, True, 56.1, 88, np.timedelta64(11, 'h'), np.datetime64(31, 'Y')],
	['Amol', False, 73.2, 45, np.timedelta64(12, 'h'), np.datetime64(40, 'Y')],
	['Lini', False, 69.3, 87, np.timedelta64(13, 'h'), np.datetime64(33, 'Y')]],
	columns=['name', 'physics', 'chemistry','algebra','timedelta', 'datetime'])
```
