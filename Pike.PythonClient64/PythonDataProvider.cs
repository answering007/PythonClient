using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Python.Runtime;
using static Python.Runtime.Py;

namespace Pike.PythonClient64
{
    public static class PythonDataProvider
    {
        static GILState _gilState;
        static dynamic _sys;

        public static object Locker { get; } = new object();
        
        public static ConnectionState State { get; private set; } = ConnectionState.Closed;

        public static string PythonDllPath { get; set; }

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

        public static void Open(IEnumerable<string> pathComponents = null)
        {
            if (string.IsNullOrWhiteSpace(PythonDllPath)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(PythonDllPath));

            lock (Locker)
            {
                if (State == ConnectionState.Open) return;

                Runtime.PythonDLL = PythonDllPath;
                PythonEngine.Initialize();
                _gilState = GIL();

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
        }

        public static void Open(bool useDefaultPathComponents, IEnumerable<string> additionalPathComponents = null)
        {
            var pathComponents = new List<string>();
            if (useDefaultPathComponents)
                pathComponents.AddRange(DefaultPathComponents);
            if (additionalPathComponents != null)
                pathComponents.AddRange(additionalPathComponents);
            Open(pathComponents);
        }

        public static void Close()
        {
            lock (Locker)
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

                PythonEngine.Shutdown();
                _gilState = GIL();

                State = ConnectionState.Closed;
            }
        }
    }
}
