using System;
using System.Collections;
using System.Data;
using System.Data.Common;
using System.Globalization;

namespace Pike.PythonClient64.Data
{
    /// <summary>
    /// Forward-only <see cref="DbDataReader"/> over a pandas DataFrame.
    /// The frame is read in chunks (<see cref="PythonCommand.StreamingChunkSize"/> rows), so a large result is not
    /// materialized into a DataTable
    /// </summary>
    public sealed class PythonDataReader : DbDataReader
    {
        readonly DataFrameCursor _cursor;
        readonly bool _schemaOnly;
        readonly int _maxRows;
        readonly Action _onClose;
        int _rowInChunk = -1;
        int _rowsRead;
        bool _closed;

        /// <summary>
        /// Creates a reader over the specified cursor
        /// </summary>
        /// <param name="cursor">Cursor over the DataFrame. The reader takes the ownership</param>
        /// <param name="schemaOnly">True to expose the schema only</param>
        /// <param name="maxRows">Maximum number of rows returned by <see cref="Read"/></param>
        /// <param name="onClose">Optional action executed when the reader is closed (e.g. CloseConnection)</param>
        internal PythonDataReader(DataFrameCursor cursor, bool schemaOnly = false, int maxRows = int.MaxValue,
            Action onClose = null)
        {
            _cursor = cursor ?? throw new ArgumentNullException(nameof(cursor));
            _schemaOnly = schemaOnly;
            _maxRows = maxRows;
            _onClose = onClose;
        }

        /// <inheritdoc />
        public override int FieldCount => _cursor.Columns.Count;

        /// <inheritdoc />
        public override bool HasRows => !_schemaOnly && _cursor.HasRows;

        /// <inheritdoc />
        public override bool IsClosed => _closed;

        /// <inheritdoc />
        public override int Depth => 0;

        /// <inheritdoc />
        public override int RecordsAffected => -1;

        /// <inheritdoc />
        public override object this[int ordinal] => GetValue(ordinal);

        /// <inheritdoc />
        public override object this[string name] => GetValue(GetOrdinal(name));

        /// <inheritdoc />
        /// <summary>
        /// Advances to the next row. Chunks are read lazily, so the memory usage is bounded by the chunk size
        /// </summary>
        /// <returns>True when a row is available</returns>
        public override bool Read()
        {
            if (_closed || _schemaOnly) return false;
            if (_rowsRead >= _maxRows) return false;

            if (_rowInChunk + 1 < _cursor.ChunkRowCount)
            {
                _rowInChunk++;
                _rowsRead++;
                return true;
            }

            if (!_cursor.ReadNextChunk()) return false;

            _rowInChunk = 0;
            _rowsRead++;
            return true;
        }

        /// <inheritdoc />
        public override object GetValue(int ordinal)
        {
            if (_closed) throw new InvalidOperationException("The reader is closed");

            var column = GetColumn(ordinal);
            if (_rowInChunk < 0) throw new InvalidOperationException("Read() must be called before accessing values");

            return _cursor.GetValue(column, _rowInChunk);
        }

        /// <inheritdoc />
        public override bool IsDBNull(int ordinal)
        {
            return GetValue(ordinal) is DBNull;
        }

        /// <inheritdoc />
        public override int GetValues(object[] values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));

            var count = Math.Min(values.Length, FieldCount);
            for (var i = 0; i < count; i++)
                values[i] = GetValue(i);

            return count;
        }

        /// <inheritdoc />
        public override T GetFieldValue<T>(int ordinal)
        {
            return (T)GetRequiredValue(ordinal);
        }

        /// <inheritdoc />
        public override string GetName(int ordinal)
        {
            return _cursor.Columns[GetColumn(ordinal)].Name;
        }

        /// <inheritdoc />
        public override string GetDataTypeName(int ordinal)
        {
            return _cursor.Columns[GetColumn(ordinal)].Dtype;
        }

        /// <inheritdoc />
        public override Type GetFieldType(int ordinal)
        {
            return _cursor.Columns[GetColumn(ordinal)].DataType;
        }

        /// <inheritdoc />
        public override int GetOrdinal(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name can't be null or empty", nameof(name));

            for (var i = 0; i < _cursor.Columns.Count; i++)
            {
                if (string.Equals(_cursor.Columns[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            throw new IndexOutOfRangeException($"Column '{name}' is not found");
        }

        /// <inheritdoc />
        public override bool NextResult()
        {
            return false;
        }

        int GetColumn(int ordinal)
        {
            if (ordinal < 0 || ordinal >= _cursor.Columns.Count) throw new IndexOutOfRangeException("ordinal is out of range");

            return ordinal;
        }

        object GetRequiredValue(int ordinal)
        {
            var value = GetValue(ordinal);
            if (value == null || value is DBNull)
                throw new InvalidCastException($"Column '{GetName(ordinal)}' contains NULL");

            return value;
        }

        /// <inheritdoc />
        public override bool GetBoolean(int ordinal) => Convert.ToBoolean(GetRequiredValue(ordinal), CultureInfo.InvariantCulture);

        /// <inheritdoc />
        public override byte GetByte(int ordinal) => Convert.ToByte(GetRequiredValue(ordinal), CultureInfo.InvariantCulture);

        /// <inheritdoc />
        public override char GetChar(int ordinal) => Convert.ToChar(GetRequiredValue(ordinal), CultureInfo.InvariantCulture);

        /// <inheritdoc />
        public override DateTime GetDateTime(int ordinal) => Convert.ToDateTime(GetRequiredValue(ordinal), CultureInfo.InvariantCulture);

        /// <inheritdoc />
        public override decimal GetDecimal(int ordinal) => Convert.ToDecimal(GetRequiredValue(ordinal), CultureInfo.InvariantCulture);

        /// <inheritdoc />
        public override double GetDouble(int ordinal) => Convert.ToDouble(GetRequiredValue(ordinal), CultureInfo.InvariantCulture);

        /// <inheritdoc />
        public override float GetFloat(int ordinal) => Convert.ToSingle(GetRequiredValue(ordinal), CultureInfo.InvariantCulture);

        /// <inheritdoc />
        public override Guid GetGuid(int ordinal)
        {
            var value = GetRequiredValue(ordinal);

            return value is Guid guid ? guid : Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        /// <inheritdoc />
        public override short GetInt16(int ordinal) => Convert.ToInt16(GetRequiredValue(ordinal), CultureInfo.InvariantCulture);

        /// <inheritdoc />
        public override int GetInt32(int ordinal) => Convert.ToInt32(GetRequiredValue(ordinal), CultureInfo.InvariantCulture);

        /// <inheritdoc />
        public override long GetInt64(int ordinal) => Convert.ToInt64(GetRequiredValue(ordinal), CultureInfo.InvariantCulture);

        /// <inheritdoc />
        public override string GetString(int ordinal) => Convert.ToString(GetRequiredValue(ordinal), CultureInfo.InvariantCulture);

        /// <inheritdoc />
        /// <summary>
        /// Reads a binary value into the buffer
        /// </summary>
        /// <param name="ordinal">Column ordinal</param>
        /// <param name="dataOffset">Offset in the value</param>
        /// <param name="buffer">Target buffer</param>
        /// <param name="bufferOffset">Offset in the buffer</param>
        /// <param name="length">Number of bytes to read</param>
        /// <returns>Number of bytes read</returns>
        public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length)
        {
            var value = GetRequiredValue(ordinal);
            if (!(value is byte[] bytes)) throw new InvalidCastException($"Column '{GetName(ordinal)}' is not binary");

            if (buffer == null) return bytes.Length;

            var count = (int)Math.Min(length, bytes.Length - dataOffset);
            Array.Copy(bytes, dataOffset, buffer, bufferOffset, count);

            return count;
        }

        /// <inheritdoc />
        /// <summary>
        /// Reads characters of a string value into the buffer
        /// </summary>
        /// <param name="ordinal">Column ordinal</param>
        /// <param name="dataOffset">Offset in the value</param>
        /// <param name="buffer">Target buffer</param>
        /// <param name="bufferOffset">Offset in the buffer</param>
        /// <param name="length">Number of characters to read</param>
        /// <returns>Number of characters read</returns>
        public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length)
        {
            var text = GetString(ordinal);

            if (buffer == null) return text.Length;

            var count = (int)Math.Min(length, text.Length - dataOffset);
            text.CopyTo((int)dataOffset, buffer, bufferOffset, count);

            return count;
        }

        /// <inheritdoc />
        /// <summary>
        /// Returns the schema of the result. The schema is produced by <see cref="DataTableReader"/>, so it contains
        /// the standard ADO.NET columns (IsKey, IsUnique, ColumnSize...) required by DataTable.Load/DataAdapter.
        /// The Python dtype name is available via <see cref="GetDataTypeName"/>
        /// </summary>
        /// <returns>A <see cref="DataTable"/> with the schema description</returns>
        public override DataTable GetSchemaTable()
        {
            var schema = new DataTable("SchemaTable")
            {
                Locale = CultureInfo.InvariantCulture
            };

            for (var i = 0; i < _cursor.Columns.Count; i++)
            {
                var column = _cursor.Columns[i];
                schema.Columns.Add(new DataColumn(column.Name, column.DataType) { AllowDBNull = true });
            }

            using (var reader = schema.CreateDataReader())
                return reader.GetSchemaTable();
        }

        /// <inheritdoc />
        public override IEnumerator GetEnumerator()
        {
            return new DbEnumerator(this);
        }

        /// <inheritdoc />
        /// <summary>
        /// Closes the reader: the DataFrame and the python objects are released
        /// </summary>
        public override void Close()
        {
            if (_closed) return;

            _closed = true;
            _cursor.Dispose();
            _onClose?.Invoke();
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing) Close();

            base.Dispose(disposing);
        }
    }
}
