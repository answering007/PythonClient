using System;
using System.Data;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64;
using Pike.PythonClient64.Data;

namespace Pike.PythonClient.Test
{
    /// <summary>
    /// Test environment helpers: python runtime location and connection string creation.
    /// The runtime is taken from the PYTHONNET_TEST_DLL environment variable, the local .venv
    /// of the repository or the settings value (in that order), see <see cref="PythonRuntimeLocator"/>
    /// </summary>
    static class TestEnvironment
    {
        const string PythonDllVariable = PythonRuntimeLocator.PythonDllVariable;

        static TestEnvironment()
        {
            // The interpreter is initialized once per test run: re-initialization of pythonnet with
            // numpy/pandas imported is slow and not reliable, so the engine lives until the assembly cleanup
            PythonDataProvider.ShutdownOnLastClose = false;
            ApplyLocalEnvironment();
        }

        /// <summary>
        /// Full path to python*.dll
        /// </summary>
        public static string PythonDll =>
            Environment.GetEnvironmentVariable(PythonDllVariable) ?? SettingsMain.Default.PythonDllPath;

        /// <summary>
        /// Where the python runtime comes from: the PYTHONNET_TEST_DLL variable, the local virtual
        /// environment folder or null when nothing is found (diagnostics)
        /// </summary>
        public static string RuntimeSource => PythonRuntimeLocator.Source;

        /// <summary>
        /// True if the python runtime is available
        /// </summary>
        public static bool IsPythonAvailable => !string.IsNullOrWhiteSpace(PythonDll) && File.Exists(PythonDll);

        /// <summary>
        /// Uses the local virtual environment when the python runtime is not configured explicitly.
        /// pythonnet reads PYTHONPATH during initialization, so the variable is set before the first
        /// <see cref="PythonDataProvider.Open(System.Collections.Generic.IEnumerable{string})"/> call
        /// </summary>
        static void ApplyLocalEnvironment()
        {
            var source = PythonRuntimeLocator.Source;
            if (source == null || string.Equals(source, PythonRuntimeLocator.PythonDllVariable, StringComparison.Ordinal))
                return;

            Environment.SetEnvironmentVariable(PythonRuntimeLocator.PythonDllVariable, PythonRuntimeLocator.PythonDll);

            var packages = PythonRuntimeLocator.SitePackages;
            if (string.IsNullOrWhiteSpace(packages)) return;

            var existing = Environment.GetEnvironmentVariable("PYTHONPATH");
            if (existing != null && existing.IndexOf(packages, StringComparison.OrdinalIgnoreCase) >= 0) return;

            Environment.SetEnvironmentVariable("PYTHONPATH",
                string.IsNullOrWhiteSpace(existing) ? packages : existing.TrimEnd(';') + ";" + packages);
        }

        /// <summary>
        /// Marks the test as inconclusive when the python runtime is not configured
        /// </summary>
        public static void SkipIfNoPython()
        {
            if (!IsPythonAvailable)
                Assert.Inconclusive(
                    $"Python runtime is not configured (set {PythonDllVariable} or {PythonRuntimeLocator.EnvironmentVariable}, " +
                    "or create the local environment with scripts\\test\\Initialize-PythonEnvironment.ps1)");
        }

        /// <summary>
        /// Creates connection string builder with the configured python runtime and default path components
        /// </summary>
        /// <param name="scriptFile">Optional python script file</param>
        /// <returns>Connection string builder</returns>
        public static PythonConnectionStringBuilder CreateBuilder(string scriptFile = null)
        {
            var builder = new PythonConnectionStringBuilder { PythonDll = PythonDll };
            if (!string.IsNullOrEmpty(scriptFile)) builder.File = scriptFile;

            PythonDataProvider.PythonDllPath = PythonDll;
            var components = PythonDataProvider.DefaultPathComponents.ToArray();
            if (components.Length > 0) builder.PythonPath = string.Join(";", components);

            return builder;
        }

        /// <summary>
        /// Creates and opens the connection with the configured python runtime
        /// </summary>
        /// <param name="scriptFile">Optional python script file</param>
        /// <returns>Opened connection</returns>
        public static PythonConnection CreateConnection(string scriptFile = null)
        {
            var builder = CreateBuilder(scriptFile);
            var connection = new PythonConnection { ConnectionString = builder.ConnectionString };
            connection.Open();

            return connection;
        }

        /// <summary>
        /// Opens the provider when it is not opened yet. Unlike <see cref="CreateConnection"/> the method is
        /// idempotent, so repeated test initializations do not leak engine references
        /// </summary>
        public static void EnsureProviderOpen()
        {
            if (PythonDataProvider.State == ConnectionState.Open) return;

            var builder = CreateBuilder();
            PythonDataProvider.Open(builder.PythonPathComponents);
        }

        /// <summary>
        /// Kept for the tests that requested a clean engine state.
        /// The engine is shut down once in <see cref="Tests.AssemblyHooks"/>: re-initialization of
        /// pythonnet with imported numpy/pandas is slow and not reliable
        /// </summary>
        public static void ShutdownIfOwned()
        {
            // no-op by design, see AssemblyHooks.AssemblyCleanup
        }
    }
}
