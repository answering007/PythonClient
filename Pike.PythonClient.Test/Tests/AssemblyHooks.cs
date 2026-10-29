using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64;

namespace Pike.PythonClient.Test
{
    /// <summary>
    /// Assembly level test hooks
    /// </summary>
    [TestClass]
    public class AssemblyHooks
    {
        /// <summary>
        /// Reports the python runtime used by the integration tests. The interpreter is not started here:
        /// the hook may run in the vstest process, where the python runtime of the test host is not available
        /// </summary>
        /// <param name="context">Test context of the current run</param>
        [AssemblyInitialize]
        public static void AssemblyInitialize(TestContext context)
        {
            context.WriteLine($"Python runtime: {TestEnvironment.PythonDll ?? "<not configured>"}");
            context.WriteLine($"Python version: {PythonRuntimeLocator.PythonVersion ?? "<unknown>"}");
            context.WriteLine($"Python home: {PythonRuntimeLocator.PythonHome ?? "<none>"}");
            context.WriteLine($"Packages: {PythonRuntimeLocator.Packages ?? "<unknown>"}");
            context.WriteLine($"Python source: {TestEnvironment.RuntimeSource ?? "<none>"}");
        }

        /// <summary>
        /// Shuts the python engine down when all tests are finished
        /// </summary>
        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            PythonDataProvider.Shutdown();
        }
    }
}
