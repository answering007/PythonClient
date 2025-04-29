using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Pike.PythonClient64.Data;

namespace Pike.PythonClient64
{
    public class PythonQueryBuilder
    {
        public string PythonDllPath { get; private set; }

        public IList<string> PathComponents { get; } = new List<string>();

        public IEnumerable<string> DefaultPathComponents
        {
            get
            {
                if (string.IsNullOrWhiteSpace(PythonDllPath)) return new string[] { };
                var pythonDll = new FileInfo(PythonDllPath);
                if (!pythonDll.Exists) throw new FileNotFoundException("Can't find python*.dll file", PythonDllPath);
                if (pythonDll.DirectoryName == null)
                    throw new DirectoryNotFoundException("Directory name of python*.dll file can't be null");

                //PATH components
                var lib = Path.Combine(pythonDll.DirectoryName, "Lib");
                var dlls = Path.Combine(pythonDll.DirectoryName, "DLLs");
                var packages = Path.Combine(lib, "site-packages");
                var libraryBin = Path.Combine(pythonDll.DirectoryName, "Library", "bin");

                return new[] { lib, dlls, packages, libraryBin };
            }
        }

        public string CommandText { get; private set; }

        public IDictionary<string, object> Parameters { get; } = new Dictionary<string, object>();

        public string ScriptFile { get; private set; }

        public PythonQueryBuilder SetPythonDllPath(string pathToPythonDll)
        {
            if (string.IsNullOrWhiteSpace(pathToPythonDll)) throw new ArgumentException($"'{nameof(pathToPythonDll)}' cannot be null or whitespace.", nameof(pathToPythonDll));
            if (!File.Exists(pathToPythonDll)) throw new FileNotFoundException("Can't find python*.dll file", pathToPythonDll);

            PythonDllPath = pathToPythonDll;
            return this;
        }
        
        public PythonQueryBuilder SetPathComponents(IEnumerable<string> pathComponents, bool clearBeforeAdd = false)
        {
            if (pathComponents == null) throw new ArgumentNullException(nameof(pathComponents));

            if (clearBeforeAdd)
                PathComponents.Clear();

            foreach (var component in pathComponents)
                if (!string.IsNullOrWhiteSpace(component))
                    PathComponents.Add(component);

            return this;
        }

        public PythonQueryBuilder SetPathComponents(bool clearBeforeAdd = false)
        {
            return SetPathComponents(DefaultPathComponents, clearBeforeAdd);
        }

        public PythonQueryBuilder SetPythonCode(string pythonCode)
        {
            if (string.IsNullOrWhiteSpace(pythonCode)) throw new ArgumentException($"'{nameof(pythonCode)}' cannot be null or whitespace.", nameof(pythonCode));

            CommandText = pythonCode;

            return this;
        }

        public PythonQueryBuilder SetParameters(IDictionary<string, object> parameters)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            Parameters.Clear();
            foreach (var parameter in parameters)
                Parameters[parameter.Key] = parameter.Value;

            return this;
        }

        public PythonQueryBuilder SetScriptPath(string scriptPath)
        {
            if (!string.IsNullOrWhiteSpace(scriptPath))
            {
                if (!File.Exists(scriptPath)) throw new FileNotFoundException("Can't find script file", scriptPath);
                ScriptFile = scriptPath;
            }
            else
                ScriptFile = string.Empty;

            return this;
        }

        public void WithCurrentConnection(
            IEnumerable<Tuple<string, IDictionary<string, object>, Action<DataTable>>> commandsData)
        {
            if (commandsData == null) throw new ArgumentNullException(nameof(commandsData));

            // Create connection string builder
            var builder = new PythonConnectionStringBuilder
            {
                PythonDll = PythonDllPath,
                PythonPath = string.Join(";", PathComponents)
            };
            if (!string.IsNullOrWhiteSpace(ScriptFile))
                builder.File = ScriptFile;

            // Create connection
            using (var connection = new PythonConnection())
            {
                connection.ConnectionString = builder.ConnectionString;
                connection.Open();

                foreach (var tuple in commandsData)
                {
                    // Create command
                    using (var command = new PythonCommand())
                    {
                        command.Connection = connection;
                        command.CommandText = tuple.Item1;

                        //Set query parameters. It will be passed to python "params" variable
                        if (tuple.Item2 != null)
                            foreach (var parameter in tuple.Item2)
                                command.Parameters.Add(new PythonParameter
                                    { ParameterName = parameter.Key, Value = parameter.Value });

                        /*
                         * Python script must have "result" variable of type pandas DataFrame.
                         * This variable data will be transferred to DbDataReader
                         */
                        var dataTable = new DataTable("result");
                        using (var reader = command.ExecuteReader())
                            dataTable.Load(reader);
                        tuple.Item3(dataTable);
                    }
                }   
            }
        }

        public DataTable GetData()
        {
            DataTable result = null;
            var tuple = new Tuple<string, IDictionary<string, object>, Action<DataTable>>(CommandText, Parameters,
                table => result = table);
            WithCurrentConnection(new []{ tuple });
            return result;
        }
    }
}
