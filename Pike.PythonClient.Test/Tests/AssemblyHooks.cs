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
        /// Shuts the python engine down when all tests are finished
        /// </summary>
        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            PythonDataProvider.Shutdown();
        }
    }
}
