using System.Data;
using Python.Runtime;

namespace Pike.PythonClient64.FrameReaders
{
    /// <summary>
    /// Reads a python frame object (pandas.DataFrame, polars.DataFrame) into a DataTable.
    /// Implementations are called while the GIL is acquired and never dispose the frame: the ownership
    /// stays in <see cref="PythonDataQuery"/>
    /// </summary>
    internal interface IFrameReader
    {
        /// <summary>
        /// Kind of the supported frame
        /// </summary>
        FrameKind Kind { get; }

        /// <summary>
        /// Reads the frame into a DataTable
        /// </summary>
        /// <param name="frame">Python frame object</param>
        /// <param name="tableName">Table name (the result variable name)</param>
        /// <returns>Table with all rows of the frame</returns>
        DataTable Read(PyObject frame, string tableName);
    }
}
