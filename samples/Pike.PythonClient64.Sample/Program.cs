using System;
using System.Data.Common;
using System.IO;
using System.Threading.Tasks;
using Pike.PythonClient64.Data;

namespace Pike.PythonClient64.Sample
{
    /// <summary>
    /// Minimal .NET Framework 4.8 host sample: runs a Python script through the provider (asynchronously) and prints
    /// the result table, then reads the same result in streaming mode.
    /// Environment variables:
    ///   PYTHONNET_SAMPLE_DLL  - full path to python3xx.dll (PYTHONNET_TEST_DLL is used as a fallback)
    ///   PYTHONNET_SAMPLE_PATH - extra sys.path entries separated by ';' (PYTHONPATH is used as a fallback)
    /// </summary>
    internal static class Program
    {
        const string Script = @"import numpy as np
import pandas as pd

params = globals()['params'] if 'params' in globals() else {}
limit = params.get('limit', 3)

result = pd.DataFrame({
    'StringColumn': ['Pike', None, 'Amol'][:limit],
    'BoolColumn': [True, False, True][:limit],
    'FloatColumn': [123.456, np.nan, 456.789][:limit],
    'IntColumn': [123456, 456789, 789123][:limit],
    'DateTimeColumn': pd.to_datetime(['2020-01-01', None, '2021-06-15'])[:limit]
})";

        static async Task<int> Main()
        {
            var pythonDll = GetSetting("PYTHONNET_SAMPLE_DLL", "PYTHONNET_TEST_DLL");
            if (string.IsNullOrWhiteSpace(pythonDll) || !File.Exists(pythonDll))
            {
                Console.Error.WriteLine(
                    "python3xx.dll is not found: set PYTHONNET_SAMPLE_DLL (or PYTHONNET_TEST_DLL) to the full path");
                return 2;
            }

            var builder = new PythonConnectionStringBuilder { PythonDll = pythonDll };
            var pythonPath = GetSetting("PYTHONNET_SAMPLE_PATH", "PYTHONPATH");
            if (!string.IsNullOrWhiteSpace(pythonPath)) builder.PythonPath = pythonPath;

            try
            {
                using (var connection = new PythonConnection { ConnectionString = builder.ConnectionString })
                {
                    // Asynchronous API: the python work is executed on a thread pool thread
                    await connection.OpenAsync().ConfigureAwait(false);

                    Console.WriteLine($"Provider  : {typeof(Program).Assembly.GetName().Name}");
                    Console.WriteLine($"Python    : {connection.ServerVersion}");
                    Console.WriteLine($"Framework : {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
                    Console.WriteLine();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = Script;
                        command.Parameters.Add(new PythonParameter { ParameterName = "limit", Value = 3 });

                        using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                            Print(reader);
                    }

                    // Streaming API: the DataFrame is read in chunks, it is not materialized into a DataTable
                    using (var streamingCommand = (PythonCommand)connection.CreateCommand())
                    {
                        streamingCommand.CommandText = Script;
                        streamingCommand.Streaming = true;
                        streamingCommand.StreamingChunkSize = 2;
                        streamingCommand.Parameters.Add(new PythonParameter { ParameterName = "limit", Value = 3 });

                        using (var reader = await streamingCommand.ExecuteReaderAsync().ConfigureAwait(false))
                        {
                            var rows = 0;
                            while (await reader.ReadAsync().ConfigureAwait(false)) rows++;

                            Console.WriteLine();
                            Console.WriteLine($"Streaming reader: {rows} rows, {reader.FieldCount} columns");
                        }
                    }
                }

                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        static string GetSetting(string primaryVariable, string fallbackVariable)
        {
            var value = Environment.GetEnvironmentVariable(primaryVariable);
            return string.IsNullOrWhiteSpace(value) ? Environment.GetEnvironmentVariable(fallbackVariable) : value;
        }

        static void Print(DbDataReader reader)
        {
            for (var i = 0; i < reader.FieldCount; i++)
                Console.Write($"{reader.GetName(i)} [{reader.GetFieldType(i).Name}]\t");
            Console.WriteLine();

            while (reader.Read())
            {
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.GetValue(i);
                    Console.Write($"{(value == DBNull.Value ? "<null>" : value)}\t");
                }

                Console.WriteLine();
            }
        }
    }
}
