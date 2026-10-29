using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using Python.Runtime;
using static Python.Runtime.Py;

namespace Pike.PythonClient64
{
    /// <summary>
    /// Connection provider to execute Python data scripts.
    /// The python engine is a singleton for the process: it is initialized once and reference counted
    /// </summary>
    public static class PythonDataProvider
    {
        static readonly object Sync = new object();
        static int _references;
        static string _pythonVersion;
        static IntPtr _savedThreadState = IntPtr.Zero;
        static int _ownerThreadId;

        /// <summary>
        /// Connection state
        /// </summary>
        public static ConnectionState State { get; private set; } = ConnectionState.Closed;

        /// <summary>
        /// Provider version (python version). Empty string when the engine is not initialized
        /// </summary>
        public static string Version => _pythonVersion ?? string.Empty;

        /// <summary>
        /// Full path to python*.dll library
        /// </summary>
        public static string PythonDllPath { get; set; }

        /// <summary>
        /// True (default) to call <see cref="PythonEngine.Shutdown"/> when the last consumer is closed.
        /// False to keep the engine alive until the process exits (recommended for Excel/Power Query)
        /// </summary>
        public static bool ShutdownOnLastClose { get; set; } = true;

        /// <summary>
        /// Number of opened consumers of the python engine (diagnostics)
        /// </summary>
        public static int ReferenceCount => _references;

        /// <summary>
        /// Default PATH components (\Lib, \DLLs, \Lib\site-packages, \Library\bin).
        /// Non-existed directories are filtered out
        /// </summary>
        public static IEnumerable<string> DefaultPathComponents
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

                var components = new List<string> { lib, dlls, packages, libraryBin };

                // PYTHONHOME / PYTHONPATH from the environment
                components.AddRange(SplitPaths(Environment.GetEnvironmentVariable("PYTHONHOME")));
                components.AddRange(SplitPaths(Environment.GetEnvironmentVariable("PYTHONPATH")));

                return components.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            }
        }

        /// <summary>
        /// Splits the ';' separated paths
        /// </summary>
        /// <param name="paths">Paths to split</param>
        /// <returns>Path components</returns>
        static IEnumerable<string> SplitPaths(string paths)
        {
            return string.IsNullOrWhiteSpace(paths)
                ? Enumerable.Empty<string>()
                : paths.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Opens the Python runtime environment and sets up the system path with the specified path components.
        /// The engine is initialized once and reference counted: the repeated call reuses the existed engine
        /// </summary>
        /// <param name="pathComponents">Optional additional path components to prepend to the Python sys.path. If null, only default path components are used.</param>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="PythonDllPath"/> is null or whitespace</exception>
        /// <exception cref="FileNotFoundException">Thrown if python*.dll file does not exist</exception>
        public static void Open(IEnumerable<string> pathComponents = null)
        {
            if (string.IsNullOrWhiteSpace(PythonDllPath))
                throw new InvalidOperationException(
                    "PythonDllPath is required, set full path to python*.dll before Open()");

            lock (Sync)
            {
                if (PythonEngine.IsInitialized)
                {
                    _references++;
                    State = ConnectionState.Open;
                    _pythonVersion = _pythonVersion ?? PythonEngine.Version;
                }
                else
                {
                    if (!File.Exists(PythonDllPath))
                        throw new FileNotFoundException("Can't find python*.dll file", PythonDllPath);

                    Runtime.PythonDLL = PythonDllPath;     // Set path to python*.dll library
                    PythonEngine.Initialize();             // Initialize engine

                    try
                    {
                        // Initialize() leaves the GIL acquired by the current thread, so it is released here:
                        // otherwise every Python call from another thread would block forever
                        _ownerThreadId = Thread.CurrentThread.ManagedThreadId;
                        _savedThreadState = PythonEngine.BeginAllowThreads();
                        _pythonVersion = PythonEngine.Version;
                    }
                    catch (Exception)
                    {
                        // Engine must not stay initialized in a half-configured state
                        _pythonVersion = null;
                        State = ConnectionState.Closed;
                        ShutdownEngine();
                        throw;
                    }

                    _references = 1;
                    State = ConnectionState.Open;
                }
            }

            // GIL is acquired outside the lock: a thread that already holds the GIL can't deadlock the provider
            AddPathComponents(pathComponents);
        }

        /// <summary>
        /// Opens the Python runtime environment with the specified path components.
        /// </summary>
        /// <param name="useDefaultPathComponents">If true, default path components will be used in addition to any additional path components specified.</param>
        /// <param name="additionalPathComponents">Optional additional path components to prepend to the Python sys.path.</param>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="PythonDllPath"/> is null or whitespace</exception>
        public static void Open(bool useDefaultPathComponents, IEnumerable<string> additionalPathComponents = null)
        {
            var pathComponents = new List<string>();
            if (useDefaultPathComponents) pathComponents.AddRange(DefaultPathComponents);
            if (additionalPathComponents != null) pathComponents.AddRange(additionalPathComponents);

            Open(pathComponents);
        }

        /// <summary>
        /// Closes the Python runtime environment.
        /// The engine is shut down only when the last consumer is closed and <see cref="ShutdownOnLastClose"/> is true
        /// </summary>
        public static void Close()
        {
            bool shutdown;

            lock (Sync)
            {
                if (_references == 0)
                {
                    State = ConnectionState.Closed;
                    return;
                }

                _references--;
                if (_references > 0) return;               // Other consumers still use the engine

                State = ConnectionState.Closed;
                _pythonVersion = null;
                shutdown = ShutdownOnLastClose && PythonEngine.IsInitialized;
            }

            // Shutdown needs the GIL, so it must not be called while the lock is held
            if (shutdown) ShutdownEngine();
        }

        /// <summary>
        /// Shuts the python engine down regardless of the reference counter (process/assembly cleanup)
        /// </summary>
        public static void Shutdown()
        {
            bool shutdown;

            lock (Sync)
            {
                _references = 0;
                State = ConnectionState.Closed;
                _pythonVersion = null;
                shutdown = PythonEngine.IsInitialized;
            }

            if (shutdown) ShutdownEngine();
        }

        /// <summary>
        /// Releases the saved thread state and finalizes the python engine.
        /// The saved thread state can be restored only on the thread that saved it, so the finalization is skipped
        /// when it is requested from another thread: the interpreter then stays alive until the process exits
        /// </summary>
        static void ShutdownEngine()
        {
            if (!PythonEngine.IsInitialized) return;

            if (Thread.CurrentThread.ManagedThreadId != _ownerThreadId)
            {
                // The GIL is released, so other threads can still use the engine; the process exit finalizes it
                return;
            }

            var threadState = _savedThreadState;
            _savedThreadState = IntPtr.Zero;

            if (threadState != IntPtr.Zero)
            {
                try
                {
                    PythonEngine.EndAllowThreads(threadState);
                }
                catch (Exception)
                {
                    // The interpreter is finalized below anyway
                }
            }

            _ownerThreadId = 0;

            try
            {
                PythonEngine.Shutdown();
            }
            catch (PlatformNotSupportedException)
            {
                // pythonnet 3.0.5 serializes the runtime state with BinaryFormatter, which is not available
                // on .NET 8+. The interpreter then stays alive until the process exits (safe fallback)
            }
        }

        /// <summary>
        /// Prepends path components to the python sys.path
        /// </summary>
        /// <param name="pathComponents">Path components to add</param>
        static void AddPathComponents(IEnumerable<string> pathComponents)
        {
            if (pathComponents == null) return;

            using (GIL())
            using (PyObject sysModule = Import("sys"))
            {
                dynamic sys = sysModule;
                foreach (var pathComponent in pathComponents)
                {
                    if (string.IsNullOrWhiteSpace(pathComponent) || !Directory.Exists(pathComponent)) continue;

                    sys.path.insert(0, pathComponent);   // insert(0, ...) gives priority to the user paths
                }
            }
        }
    }
}
