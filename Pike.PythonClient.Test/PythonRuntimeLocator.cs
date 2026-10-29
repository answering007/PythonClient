using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Pike.PythonClient.Test
{
    /// <summary>
    /// Locates the python runtime for the integration tests. Sources in priority order:
    /// <c>PYTHONNET_TEST_DLL</c> (CI, scripts or a developer), <c>PYTHONNET_TEST_VENV</c> (explicit
    /// environment folder) and the local <c>.venv</c> of the repository.
    /// Discovery is best-effort: an unavailable or incomplete environment is ignored, so the tests
    /// report themselves as inconclusive instead of failing
    /// </summary>
    static class PythonRuntimeLocator
    {
        /// <summary>
        /// Environment variable with the full path to python*.dll
        /// </summary>
        public const string PythonDllVariable = "PYTHONNET_TEST_DLL";

        /// <summary>
        /// Environment variable with the virtual environment folder (overrides the local .venv)
        /// </summary>
        public const string EnvironmentVariable = "PYTHONNET_TEST_VENV";

        /// <summary>
        /// Default virtual environment folder at the repository root
        /// </summary>
        public const string EnvironmentDirectory = ".venv";

        /// <summary>
        /// File that marks a virtual environment
        /// </summary>
        const string EnvironmentConfigFile = "pyvenv.cfg";

        /// <summary>
        /// Packages imported by the integration tests
        /// </summary>
        static readonly string[] RequiredPackages = { "numpy", "pandas" };

        /// <summary>
        /// Pattern of the python runtime library name
        /// </summary>
        static readonly Regex PythonDllName = new Regex(@"^python3\d+\.dll$", RegexOptions.IgnoreCase);

        /// <summary>
        /// Full path to python*.dll or null when the runtime is not configured
        /// </summary>
        public static string PythonDll { get; }

        /// <summary>
        /// Full path to the site-packages folder or null
        /// </summary>
        public static string SitePackages { get; }

        /// <summary>
        /// Base prefix of the interpreter (pyvenv.cfg 'home'), diagnostics only
        /// </summary>
        public static string PythonHome { get; }

        /// <summary>
        /// Interpreter version from pyvenv.cfg ('3.11'), diagnostics only
        /// </summary>
        public static string PythonVersion { get; }

        /// <summary>
        /// Versions of the packages imported by the tests ('numpy 2.4.6, pandas 3.1.0'), diagnostics only
        /// </summary>
        public static string Packages { get; }

        /// <summary>
        /// Where <see cref="PythonDll"/> comes from: the variable name or the environment folder
        /// </summary>
        public static string Source { get; }

        static PythonRuntimeLocator()
        {
            try
            {
                var pythonDll = Environment.GetEnvironmentVariable(PythonDllVariable);
                if (!string.IsNullOrWhiteSpace(pythonDll))
                {
                    PythonDll = pythonDll;
                    Source = PythonDllVariable;

                    return;
                }

                var environmentPath = FindEnvironmentPath();
                if (environmentPath == null ||
                    !TryReadEnvironment(environmentPath, out var dll, out var packages, out var home)) return;

                PythonDll = dll;
                SitePackages = packages;
                PythonHome = home;
                PythonVersion = ReadConfigValue(Path.Combine(environmentPath, EnvironmentConfigFile), "version_info");
                Packages = DescribePackages(packages);
                Source = environmentPath;
            }
            catch (Exception)
            {
                // Discovery is best-effort, see the type summary
            }
        }

        /// <summary>
        /// Environment folder from <see cref="EnvironmentVariable"/> or the repository .venv
        /// </summary>
        /// <returns>Full path to the virtual environment or null</returns>
        static string FindEnvironmentPath()
        {
            var explicitPath = Environment.GetEnvironmentVariable(EnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(explicitPath))
                return File.Exists(Path.Combine(explicitPath, EnvironmentConfigFile)) ? explicitPath : null;

            var root = FindRepositoryRoot();
            if (root == null) return null;

            var local = Path.Combine(root, EnvironmentDirectory);

            return File.Exists(Path.Combine(local, EnvironmentConfigFile)) ? local : null;
        }

        /// <summary>
        /// Repository root: the closest folder that contains the solution file
        /// </summary>
        /// <returns>Full path to the repository root or null</returns>
        static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "PythonClient.sln"))) return directory.FullName;
                directory = directory.Parent;
            }

            return null;
        }

        /// <summary>
        /// Reads the python runtime location and the site-packages folder of the virtual environment
        /// </summary>
        /// <param name="environmentPath">Virtual environment folder</param>
        /// <param name="pythonDll">Full path to python*.dll</param>
        /// <param name="sitePackages">Full path to the site-packages folder</param>
        /// <param name="pythonHome">Base prefix of the interpreter (pyvenv.cfg 'home')</param>
        /// <returns>True when the environment contains the runtime and the required packages</returns>
        static bool TryReadEnvironment(string environmentPath, out string pythonDll, out string sitePackages,
            out string pythonHome)
        {
            pythonDll = null;
            sitePackages = null;
            pythonHome = null;

            var config = Path.Combine(environmentPath, EnvironmentConfigFile);
            var home = ReadConfigValue(config, "home");
            if (string.IsNullOrWhiteSpace(home) || !Directory.Exists(home)) return false;

            var version = ReadConfigValue(config, "version_info");
            if (!string.IsNullOrWhiteSpace(version))
            {
                var expected = Path.Combine(home, "python" + version.Replace(".", string.Empty) + ".dll");
                if (File.Exists(expected)) pythonDll = expected;
            }

            if (pythonDll == null)
                pythonDll = Directory.GetFiles(home, "python3*.dll")
                    .FirstOrDefault(file => PythonDllName.IsMatch(Path.GetFileName(file)));

            if (pythonDll == null) return false;

            var packages = Path.Combine(environmentPath, "Lib", "site-packages");
            if (!Directory.Exists(packages)) return false;
            if (RequiredPackages.Any(name => !Directory.Exists(Path.Combine(packages, name)))) return false;

            sitePackages = packages;
            pythonHome = home;

            return true;
        }

        /// <summary>
        /// Versions of the required packages from the site-packages metadata
        /// </summary>
        /// <param name="sitePackages">Full path to the site-packages folder</param>
        /// <returns>'name version' pairs or null</returns>
        static string DescribePackages(string sitePackages)
        {
            var versions = RequiredPackages
                .Select(name => $"{name} {FindPackageVersion(sitePackages, name)}")
                .ToArray();

            return string.Join(", ", versions);
        }

        /// <summary>
        /// Version of the installed package from its dist-info folder
        /// </summary>
        /// <param name="sitePackages">Full path to the site-packages folder</param>
        /// <param name="name">Package name</param>
        /// <returns>Version or 'unknown'</returns>
        static string FindPackageVersion(string sitePackages, string name)
        {
            const string suffix = ".dist-info";

            var prefix = name + "-";
            var info = Directory.GetDirectories(sitePackages, prefix + "*" + suffix)
                .Select(folder => Path.GetFileName(folder))
                .FirstOrDefault(folder => folder != null && folder.Length > prefix.Length + suffix.Length);

            return info == null
                ? "unknown"
                : info.Substring(prefix.Length, info.Length - prefix.Length - suffix.Length);
        }

        /// <summary>
        /// Reads the 'key = value' entry of pyvenv.cfg
        /// </summary>
        /// <param name="configFile">Full path to pyvenv.cfg</param>
        /// <param name="key">Key to read</param>
        /// <returns>Trimmed value or null</returns>
        static string ReadConfigValue(string configFile, string key)
        {
            if (!File.Exists(configFile)) return null;

            foreach (var line in File.ReadAllLines(configFile))
            {
                var separator = line.IndexOf('=');
                if (separator <= 0) continue;
                if (!string.Equals(line.Substring(0, separator).Trim(), key, StringComparison.OrdinalIgnoreCase)) continue;

                return line.Substring(separator + 1).Trim();
            }

            return null;
        }
    }
}
