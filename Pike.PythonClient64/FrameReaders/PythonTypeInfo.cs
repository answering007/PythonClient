using System;
using Python.Runtime;

namespace Pike.PythonClient64.FrameReaders
{
    /// <summary>
    /// Module and name of a python type. The values are read from the type object, so neither pandas nor polars
    /// has to be imported: both packages stay optional dependencies
    /// </summary>
    internal readonly struct PythonTypeInfo
    {
        /// <summary>
        /// Module name, for example 'pandas.core.frame' or 'polars.dataframe.frame'
        /// </summary>
        public string Module { get; }

        /// <summary>
        /// Type name, for example 'DataFrame' or 'LazyFrame'
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Creates the description of the specified type
        /// </summary>
        /// <param name="module">Module name</param>
        /// <param name="name">Type name</param>
        public PythonTypeInfo(string module, string name)
        {
            Module = module ?? string.Empty;
            Name = name ?? string.Empty;
        }

        /// <summary>
        /// True for any type of the 'polars' package
        /// </summary>
        public bool IsPolars => Module.StartsWith("polars", StringComparison.Ordinal);

        /// <summary>
        /// True for any type of the 'pandas' package
        /// </summary>
        public bool IsPandas => Module.StartsWith("pandas", StringComparison.Ordinal);

        /// <summary>
        /// Reads module and name of the python object type. Must be called while the GIL is acquired
        /// </summary>
        /// <param name="value">Python object</param>
        /// <returns>Type description</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null</exception>
        public static PythonTypeInfo From(PyObject value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            using (PyObject type = value.GetPythonType())
            using (PyObject module = type.GetAttr("__module__"))
            using (PyObject name = type.GetAttr("__name__"))
                return new PythonTypeInfo(module.ToString(), name.ToString());
        }

        /// <inheritdoc />
        public override string ToString() => Module.Length == 0 ? Name : $"{Module}.{Name}";
    }
}
