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
        /// <summary>
        /// PATH environment variable name
        /// </summary>
        public const string PathKey = "PATH";
        /// <summary>
        /// PYTHONHOME environment variable name
        /// </summary>
        public const string PythonHomeKey = "PYTHONHOME";
        /// <summary>
        /// PYTHONPATH environment variable name
        /// </summary>
        public const string PythonPathKey = "PYTHONPATH";

        const string FileKey = "FILE";

        static readonly string[] KeyConstans = {PathKey, PythonHomeKey, PythonPathKey, FileKey};

        /// <inheritdoc />
        /// <summary>
        /// Collection of keys
        /// </summary>
        public override ICollection Keys => KeyConstans.ToArray();

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
                if (!KeyConstans.Contains(keyword))
                    throw new KeyNotFoundException(
                        $"Given keyword is not supported. Supported keyword are: {string.Join(",", KeyConstans)}");
                return base[keyword];
            }
            set
            {
                if (string.IsNullOrWhiteSpace(keyword)) throw new ArgumentException("keyword can't be null or empty");
                if (!KeyConstans.Contains(keyword))
                    throw new KeyNotFoundException(
                        $"Given keyword is not supported. Supported keyword are: {string.Join(",", KeyConstans)}");
                base[keyword] = value ?? throw new ArgumentException(nameof(value));
            }
        }

        /// <summary>
        /// Represent PATH environment variable
        /// </summary>
        public string Path
        {
            get => ContainsKey(PathKey) ? this[PathKey] as string : null;
            set => this[PathKey] = value;
        }

        /// <summary>
        /// Represent PYTHONPATH environment variable
        /// </summary>
        public string PythonPath
        {
            get => ContainsKey(PythonPathKey) ? this[PythonPathKey] as string : null;
            set => this[PythonPathKey] = value;
        }

        /// <summary>
        /// Represent PYTHONHOME environment variable
        /// </summary>
        public string PythonHome
        {
            get => ContainsKey(PythonHomeKey) ? this[PythonHomeKey] as string : null;
            set => this[PythonHomeKey] = value;
        }

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
            var existedKeys = KeyConstans.Select(k => k + equalSymbol)
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