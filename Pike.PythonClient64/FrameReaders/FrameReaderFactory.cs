using System;
using System.Data;
using Python.Runtime;

namespace Pike.PythonClient64.FrameReaders
{
    /// <summary>
    /// Detects the kind of the python object returned by the script and delegates the conversion to the
    /// corresponding reader. The kind is detected by the type module/name, so neither pandas nor polars
    /// has to be imported: both packages stay optional dependencies
    /// </summary>
    internal static class FrameReaderFactory
    {
        static readonly PandasFrameReader Pandas = new PandasFrameReader();
        static readonly PolarsFrameReader Polars = new PolarsFrameReader();

        /// <summary>
        /// Reads the script result into a DataTable. Must be called while the GIL is acquired
        /// </summary>
        /// <param name="frame">Python object assigned to the result variable</param>
        /// <param name="tableName">Table name (the result variable name)</param>
        /// <returns>Table with all rows of the frame</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="frame"/> is null</exception>
        /// <exception cref="InvalidOperationException">Thrown when the object is not a pandas/polars frame</exception>
        public static DataTable Read(PyObject frame, string tableName)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));

            var type = PythonTypeInfo.From(frame);

            if (type.IsPolars) return Polars.Read(frame, tableName);
            if (type.IsPandas) return Pandas.Read(frame, tableName);

            // Backward compatibility: any pandas-like object (subclass, geopandas, modin, test double)
            if (frame.HasAttr("columns") && frame.HasAttr("dtypes")) return Pandas.Read(frame, tableName);

            throw new InvalidOperationException(
                $"'{tableName}' must be a pandas.DataFrame or polars.DataFrame, got {type}");
        }
    }
}
