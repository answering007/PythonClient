using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;

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

        /// <summary>
        /// Resolves supported keyword in a case-insensitive manner
        /// </summary>
        /// <param name="keyword">Keyword to resolve</param>
        /// <returns>Supported keyword spelling</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="keyword"/> is null or whitespace</exception>
        /// <exception cref="KeyNotFoundException">Thrown when <paramref name="keyword"/> is not supported</exception>
        static string ResolveKey(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword)) throw new ArgumentException("keyword can't be null or empty", nameof(keyword));

            // Framework passes keywords in lower case, so the lookup must be case-insensitive
            var key = KeyConstants.FirstOrDefault(k => string.Equals(k, keyword, StringComparison.OrdinalIgnoreCase));
            if (key == null)
                throw new KeyNotFoundException(
                    $"Given keyword is not supported. Supported keyword are: {string.Join(",", KeyConstants)}");

            return key;
        }

        /// <inheritdoc />
        /// <summary>
        /// Get the value for the specific key. Keyword lookup is case-insensitive
        /// </summary>
        /// <param name="keyword">Name of the parameter</param>
        /// <returns>Value of the parameter</returns>
        public override object this[string keyword]
        {
            get => base[ResolveKey(keyword)];
            set => base[ResolveKey(keyword)] = value;
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
        /// Validates the required connection string keys
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="PythonDll"/> is not set</exception>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(PythonDll))
                throw new InvalidOperationException($"{PythonDllKey} is required in connection string");
        }

        /// <summary>
        /// Parse <see cref="PythonConnection"/> connection string
        /// </summary>
        /// <param name="connectionString">String to parse. Value is parsed by <see cref="DbConnectionStringBuilder"/>,
        /// so values containing ';' must be quoted and value case is preserved</param>
        /// <returns>New instance of <see cref="PythonConnectionStringBuilder"/></returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="connectionString"/> is null</exception>
        /// <exception cref="KeyNotFoundException">Thrown when connection string contains unsupported keyword</exception>
        /// <exception cref="ArgumentException">Thrown when connection string has an invalid format</exception>
        public static PythonConnectionStringBuilder Parse(string connectionString)
        {
            if (connectionString == null) throw new ArgumentNullException(nameof(connectionString));

            // Base class parses, validates and quotes the string itself, so values keep their original case
            var rst = new PythonConnectionStringBuilder();
            rst.ConnectionString = connectionString;
            return rst;
        }
    }
}