using System;
using System.Collections.Generic;
using System.Data;
using Python.Runtime;
using Pike.PythonClient64.ColumnConverters;

namespace Pike.PythonClient64.FrameReaders
{
    /// <summary>
    /// Reads a pandas.DataFrame into a DataTable. The logic is moved out of <see cref="PythonDataQuery"/>
    /// without changes, so the pandas behaviour stays byte-for-byte compatible
    /// </summary>
    internal sealed class PandasFrameReader : FrameReaderBase, IFrameReader
    {
        /// <inheritdoc />
        public FrameKind Kind => FrameKind.Pandas;

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">Thrown when the value is not a pandas.DataFrame</exception>
        /// <exception cref="NotSupportedException">Thrown when a dtype is not supported or columns are duplicated</exception>
        public DataTable Read(PyObject dataFrame, string tableName)
        {
            if (!dataFrame.HasAttr("columns") || !dataFrame.HasAttr("dtypes"))
            {
                using (PyObject pythonType = dataFrame.GetPythonType())
                    throw new InvalidOperationException(
                        $"'{tableName}' must be a pandas.DataFrame, got {pythonType}");
            }

            var dataTable = new DataTable(tableName);

            using (PyObject columnsObject = dataFrame.GetAttr("columns"))
            using (PyObject columnNames = columnsObject.InvokeMethod("tolist"))
            {
                var columnsCount = (int)columnNames.Length();
                var sourceNames = new string[columnsCount];

                for (var i = 0; i < columnsCount; i++)
                {
                    using (PyObject columnName = columnNames.GetItem(i))
                        sourceNames[i] = columnName.ToString();
                }

                // Duplicated labels are checked before any column is read: DataFrame[label] returns a DataFrame
                // (not a Series) for them, so the dtype lookup would fail with an uninformative python error
                var duplicates = new List<string>();
                var seenNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (var sourceName in sourceNames)
                    if (!seenNames.Add(sourceName)) duplicates.Add(sourceName);

                if (duplicates.Count > 0)
                    throw new NotSupportedException(
                        $"Duplicated column names are not supported: {string.Join(", ", duplicates)}");

                var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var columnValues = new object[columnsCount][];

                for (var i = 0; i < columnsCount; i++)
                {
                    // Column is selected by the original label: labels are not always strings (0, 1, tuples...)
                    using (PyObject columnLabel = columnNames.GetItem(i))
                    using (PyObject series = dataFrame.GetItem(columnLabel))
                    using (PyObject dtype = series.GetAttr("dtype"))
                    {
                        var dtypeName = dtype.ToString();
                        var converter = GetColumnConverter(sourceNames[i], dtypeName);

                        dataTable.Columns.Add(new DataColumn(MakeColumnName(sourceNames[i], i, usedNames))
                        {
                            AllowDBNull = true,
                            DataType = converter.TargetType
                        });

                        columnValues[i] = ReadColumnValues(series, converter, dtypeName);
                    }
                }

                FillRows(dataTable, columnValues);
            }

            return dataTable;
        }

        /// <summary>
        /// Reads and converts all values of the column
        /// </summary>
        /// <param name="series">Python series (DataFrame column)</param>
        /// <param name="converter">Column converter</param>
        /// <param name="dtype">Column dtype name</param>
        /// <returns>Array of managed values</returns>
        static object[] ReadColumnValues(PyObject series, IColumnConverter converter, string dtype)
        {
            // datetime64/timedelta64 are read as exact integers, so NaT and the resolution do not depend on tolist()
            if (converter is DateTimeConverter || converter is TimeSpanConverter)
            {
                var nanosecondsPerUnit = DtypeUnits.GetNanosecondsPerUnit(dtype);

                try
                {
                    using (PyObject int64Name = new PyString("int64"))
                    using (PyObject int64Series = series.InvokeMethod("astype", new[] { int64Name }))
                        return converter.ConvertValues(
                            ScaleToNanoseconds(ConvertSeriesValues(int64Series), nanosecondsPerUnit));
                }
                catch (PythonException)
                {
                    // tz-aware datetime64[ns, UTC] can't be cast to int64, python datetime objects are used instead
                }
            }

            return converter.ConvertValues(ConvertSeriesValues(series));
        }

        /// <summary>
        /// Converts series values to the array of python objects
        /// </summary>
        /// <param name="series">Python series (DataFrame column)</param>
        /// <returns>Array of python values</returns>
        static object[] ConvertSeriesValues(PyObject series)
        {
            using (PyObject values = series.GetAttr("values"))
            using (PyObject valuesList = values.InvokeMethod("tolist"))
                return ToObjectArray(valuesList);
        }
    }
}
