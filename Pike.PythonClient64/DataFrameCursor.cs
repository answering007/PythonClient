using System;
using System.Collections.Generic;
using System.Linq;
using Pike.PythonClient64.ColumnConverters;
using Python.Runtime;

namespace Pike.PythonClient64
{
    /// <summary>
    /// Reads a pandas DataFrame in chunks, so a large frame is not materialized at once.
    /// The cursor takes the ownership of the DataFrame. Construction must be done while the GIL is acquired
    /// (see <see cref="PythonDataQuery"/>), the other members acquire the GIL themselves
    /// </summary>
    internal sealed partial class DataFrameCursor : IDisposable
    {
        /// <summary>
        /// Chunk size used when a table is materialized
        /// </summary>
        public const int DefaultChunkSize = 8192;

        readonly PyObject _dataFrame;
        readonly int _chunkSize;
        object[][] _chunk;
        int _chunkRowCount;
        bool _disposed;

        /// <summary>
        /// Describes the specified DataFrame: column names, dtypes and converters
        /// </summary>
        /// <param name="dataFrame">Python DataFrame. The cursor takes the ownership and disposes it</param>
        /// <param name="resultName">Result variable name used in error messages</param>
        /// <param name="chunkSize">Number of rows read at once</param>
        /// <exception cref="InvalidOperationException">Thrown when the value is not a pandas.DataFrame</exception>
        /// <exception cref="NotSupportedException">Thrown when a dtype is not supported or columns are duplicated</exception>
        public DataFrameCursor(PyObject dataFrame, string resultName, int chunkSize = DefaultChunkSize)
        {
            if (dataFrame == null) throw new ArgumentNullException(nameof(dataFrame));
            if (chunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSize));

            _dataFrame = dataFrame;
            _chunkSize = chunkSize;

            try
            {
                if (!dataFrame.HasAttr("columns") || !dataFrame.HasAttr("dtypes"))
                {
                    using (PyObject pythonType = dataFrame.GetPythonType())
                        throw new InvalidOperationException(
                            $"'{resultName}' must be a pandas.DataFrame, got {pythonType}");
                }

                RowCount = (int)dataFrame.GetAttr("shape").GetItem(0).As<int>();
                Columns = ReadColumns(dataFrame);
            }
            catch (Exception)
            {
                // The frame is owned by the cursor, so it must not leak when the description fails
                dataFrame.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Columns of the frame
        /// </summary>
        public IReadOnlyList<DataFrameColumn> Columns { get; }

        /// <summary>
        /// Total number of rows
        /// </summary>
        public int RowCount { get; }

        /// <summary>
        /// Number of rows already consumed
        /// </summary>
        public int Position { get; private set; }

        /// <summary>
        /// Number of rows in the current chunk
        /// </summary>
        public int ChunkRowCount => _chunkRowCount;

        /// <summary>
        /// True when the frame has rows
        /// </summary>
        public bool HasRows => RowCount > 0;

        /// <summary>
        /// Reads the next chunk of rows
        /// </summary>
        /// <returns>True when the chunk has been read, false when the frame is exhausted</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the cursor is already disposed</exception>
        public bool ReadNextChunk()
        {
            using (Py.GIL())
            {
                if (_disposed) throw new ObjectDisposedException(nameof(DataFrameCursor));

                if (Position >= RowCount)
                {
                    _chunk = null;
                    _chunkRowCount = 0;
                    return false;
                }

                var start = Position;
                var stop = Math.Min(start + _chunkSize, RowCount);

                PyObject chunkFrame = null;
                try
                {
                    chunkFrame = start == 0 && stop == RowCount ? _dataFrame : TakeRows(start, stop);

                    var values = new object[Columns.Count][];
                    for (var i = 0; i < Columns.Count; i++)
                        values[i] = ReadColumnValues(chunkFrame, Columns[i]);

                    _chunk = values;
                    _chunkRowCount = stop - start;
                    Position = stop;

                    return _chunkRowCount > 0;
                }
                finally
                {
                    if (!ReferenceEquals(chunkFrame, _dataFrame)) chunkFrame?.Dispose();
                }
            }
        }

        /// <summary>
        /// Gets the value of the current chunk
        /// </summary>
        /// <param name="columnIndex">Column index</param>
        /// <param name="rowIndex">Row index inside the current chunk</param>
        /// <returns>Managed value or <see cref="DBNull"/></returns>
        public object GetValue(int columnIndex, int rowIndex)
        {
            if (_chunk == null) throw new InvalidOperationException("Read the chunk before getting a value");
            if (columnIndex < 0 || columnIndex >= Columns.Count) throw new ArgumentOutOfRangeException(nameof(columnIndex));
            if (rowIndex < 0 || rowIndex >= _chunkRowCount) throw new ArgumentOutOfRangeException(nameof(rowIndex));

            return _chunk[columnIndex][rowIndex];
        }

        /// <summary>
        /// Releases the DataFrame and column labels
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            using (Py.GIL())
            {
                _disposed = true;
                _chunk = null;
                _chunkRowCount = 0;

                foreach (var column in Columns)
                    column.Label?.Dispose();

                _dataFrame.Dispose();
            }
        }
    }
}
