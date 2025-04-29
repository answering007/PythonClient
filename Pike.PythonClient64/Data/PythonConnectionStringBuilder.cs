using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using Newtonsoft.Json.Serialization;

namespace Pike.PythonClient64.Data
{
    /// <inheritdoc />
    /// <summary>
    /// Represent connection string builder to <see cref="T:Pike.PythonClient64.Data.PythonConnection" />
    /// </summary>
    public class PythonConnectionStringBuilder: DbConnectionStringBuilder
    {
        const string PythonDllKey = "PYTHONDLL";
        const string PythonPathKey = "PYTHONPATH";
        const string FileKey = "FILE";

        static readonly string[] KeyConstants = { PythonDllKey, PythonPathKey, FileKey };

        /// <inheritdoc />
        /// <summary>
        /// Collection of keys
        /// </summary>
        public override ICollection Keys => KeyConstants.ToArray();

        /// <inheritdoc />
        /// <summary>
        /// Get the value for the specific key
        /// </summary>
        /// <param name="keyword">Name of the parameter</param>
        /// <returns>Value of the parameter</returns>
        public override object this[string keyword]
        {
            get
            {
                if (string.IsNullOrWhiteSpace(keyword)) throw new ArgumentException("keyword can't be null or empty");
                if (!KeyConstants.Contains(keyword))
                    throw new KeyNotFoundException(
                        $"Given keyword is not supported. Supported keyword are: {string.Join(",", KeyConstants)}");
                return base[keyword];
            }
            set
            {
                if (string.IsNullOrWhiteSpace(keyword)) throw new ArgumentException("keyword can't be null or empty");
                if (!KeyConstants.Contains(keyword))
                    throw new KeyNotFoundException(
                        $"Given keyword is not supported. Supported keyword are: {string.Join(",", KeyConstants)}");
                base[keyword] = value;
            }
        }

        /// <summary>
        /// Represent PythonDll full path. Typical value is ../python38.dll (Windows)
        /// </summary>
        public string PythonDll
        {
            get => ContainsKey(PythonDllKey) ? this[PythonDllKey] as string : null;
            set => this[PythonDllKey] = value;
        }

        /// <summary>
        /// Represent PATH variable
        /// </summary>
        public string PythonPath
        {
            get => ContainsKey(PythonPathKey) ? this[PythonPathKey] as string : null;
            set => this[PythonPathKey] = value;
        }

        /// <summary>
        /// Get PATH components
        /// </summary>
        public string[] PythonPathComponents => string.IsNullOrWhiteSpace(PythonPath)
            ? new string[] { }
            : PythonPath.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries);

        /// <summary>
        /// Full path to python script file
        /// </summary>
        public string File
        {
            get => ContainsKey(FileKey) ? this[FileKey] as string : null;
            set => this[FileKey] = value;
        }

        /// <summary>
        /// Parse <see cref="PythonConnection"/> connection string
        /// </summary>
        /// <param name="connectionString">String to parse</param>
        /// <returns>New instance of <see cref="PythonConnectionStringBuilder"/></returns>
        public static PythonConnectionStringBuilder Parse(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) throw new ArgumentException("Value can't be null or empty", nameof(connectionString));

            var comparableConnectionString = connectionString.ToUpperInvariant();

            const string equalSymbol = "=";
            var existedKeys = KeyConstants.Select(k => k + equalSymbol)
                .Where(comparableConnectionString.Contains)
                .Select(k =>
                    new KeyValuePair<string, int>(k,
                        comparableConnectionString.IndexOf(k, StringComparison.Ordinal)))
                .OrderBy(p => p.Value)
                .ToArray();

            var rst = new PythonConnectionStringBuilder();
            for (var i = 0; i < existedKeys.Length; i++)
            {
                var startIndex = existedKeys[i].Value;
                var endIndex = i == existedKeys.Length - 1 ? comparableConnectionString.Length : existedKeys[i + 1].Value;
                var length = endIndex - startIndex;
                var name = existedKeys[i].Key.Replace(equalSymbol, string.Empty);
                var value = comparableConnectionString.Substring(startIndex, length)
                    .Replace(existedKeys[i].Key, string.Empty)
                    .TrimEnd(';')
                    .Trim('"');

                rst[name] = value;
            }

            return rst;
        }
    }
}