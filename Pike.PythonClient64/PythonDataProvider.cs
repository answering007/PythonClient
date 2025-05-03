using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Python.Runtime;
using static Python.Runtime.Py;

namespace Pike.PythonClient64
{
    /// <summary>
    /// Connection provider to execute Python data scripts
    /// </summary>
    public static class PythonDataProvider
    {
        static GILState _gilState;
        static dynamic _sys;

        /// <summary>
        /// Connection state
        /// </summary>
        public static ConnectionState State { get; private set; } = ConnectionState.Closed;

        /// <summary>
        /// Provider version
        /// </summary>
        public static string Version => PythonEngine.Version;

        /// <summary>
        /// Full path to python*.dll library
        /// </summary>
        public static string PythonDllPath { get; set; }

        /// <summary>
        /// Default PATH components (\Lib, \DLLs, \Lib\site-packages, \Library\bin)
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

                return new[] { lib, dlls, packages, libraryBin };
            }
        }

        /// <summary>
        /// Opens the Python runtime environment and sets up the system path with the specified path components.
        /// </summary>
        /// <param name="pathComponents">Optional additional path components to append to the Python sys.path. If null, only default path components are used.</param>
        /// <exception cref="ArgumentException">Thrown if PythonDllPath is null or whitespace.</exception>
        public static void Open(IEnumerable<string> pathComponents = null)
        {
            //if (!Debugger.IsAttached)
            //    Debugger.Launch();
            
            if (string.IsNullOrWhiteSpace(PythonDllPath)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(PythonDllPath));
            if (State == ConnectionState.Open) return;

            Runtime.PythonDLL = PythonDllPath;  // Set path to python*.dll library
            PythonEngine.Initialize();          // Initialize engine
            _gilState = GIL();                  // Initialize GIL

            // Set PATH components
            _sys = Import("sys");
            if (pathComponents != null)
            {
                foreach (var pathComponent in pathComponents)
                {
                    if (string.IsNullOrWhiteSpace(pathComponent)) continue;
                    _sys.path.append(pathComponent);
                }
            }

            State = ConnectionState.Open;
        }

        /// <summary>
        /// Opens the Python runtime environment with the specified path components.
        /// </summary>
        /// <param name="useDefaultPathComponents">If true, default path components will be used in addition to any additional path components specified.</param>
        /// <param name="additionalPathComponents">Optional additional path components to append to the Python sys.path.</param>
        /// <exception cref="ArgumentException">Thrown if PythonDllPath is null or whitespace.</exception>
        public static void Open(bool useDefaultPathComponents, IEnumerable<string> additionalPathComponents = null)
        {
            var pathComponents = new List<string>();
            if (useDefaultPathComponents)
                pathComponents.AddRange(DefaultPathComponents);
            if (additionalPathComponents != null)
                pathComponents.AddRange(additionalPathComponents);
            Open(pathComponents);
        }

        /// <summary>
        /// Closes the Python runtime environment.
        /// </summary>
        public static void Close()
        {
            if (State == ConnectionState.Closed) return;

            if (_sys != null)
            {
                _sys.Dispose();
                _sys = null;
            }

            if (_gilState != null)
            {
                _gilState.Dispose();
                _gilState = null;
            }

            State = ConnectionState.Closed;
            PythonEngine.Shutdown();
        }
    }
}
