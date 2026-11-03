using System;
using System.Collections.Generic;
using System.Data;
using Python.Runtime;
using Pike.PythonClient64.ColumnConverters;

namespace Pike.PythonClient64.FrameReaders
{
    /// <summary>
    /// Reads a polars.DataFrame into a DataTable without converting it to pandas.
    /// Values are read column by column: <c>to_list()</c> for the plain types and
    /// <c>to_physical()</c> for the temporal types (exact integers in the dtype unit)
    /// </summary>
    internal sealed class PolarsFrameReader : FrameReaderBase, IFrameReader
    {
        /// <inheritdoc />
        public FrameKind Kind => FrameKind.Polars;

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">Thrown when the value is not a polars.DataFrame</exception>
        /// <exception cref="NotSupportedException">Thrown when a dtype is not supported and strict mode is on</exception>
        public DataTable Read(PyObject frame, string tableName)
        {
            var type = PythonTypeInfo.From(frame);

            if (type.Name == "LazyFrame")
                throw new InvalidOperationException(
                    $"'{tableName}' is a {type}, materialize it in the script: result = <lazy>.collect()");

            if (type.Name != "DataFrame")
                throw new InvalidOperationException($"'{tableName}' must be a polars.DataFrame, got {type}");

            var dataTable = new DataTable(tableName);
            var sourceNames = ReadColumnNames(frame);
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var columnValues = new object[sourceNames.Length][];

            for (var i = 0; i < sourceNames.Length; i++)
            {
                using (PyObject series = frame.InvokeMethod("get_column", new PyString(sourceNames[i])))
                using (PyObject dtypeObject = series.GetAttr("dtype"))
                {
                    var dtype = dtypeObject.ToString();
                    var converter = GetColumnConverter(sourceNames[i], dtype, PolarsDtypes.IsForbidden(dtype));

                    dataTable.Columns.Add(new DataColumn(MakeColumnName(sourceNames[i], i, usedNames))
                    {
                        AllowDBNull = true,
                        DataType = converter.TargetType
                    });

                    columnValues[i] = ReadColumnValues(series, converter, dtype);
                }
            }

            FillRows(dataTable, columnValues);
            return dataTable;
        }

        /// <summary>
        /// Reads the column names. polars always uses string names, but they are still normalized later
        /// </summary>
        /// <param name="frame">polars.DataFrame</param>
        /// <returns>Column names</returns>
        static string[] ReadColumnNames(PyObject frame)
        {
            using (PyObject columns = frame.GetAttr("columns"))
            {
                var count = (int)columns.Length();
                var names = new string[count];

                for (var i = 0; i < count; i++)
                    using (PyObject name = columns.GetItem(i))
                        names[i] = name.ToString();

                return names;
            }
        }

        /// <summary>
        /// Reads and converts all values of the column
        /// </summary>
        /// <param name="series">polars.Series</param>
        /// <param name="converter">Column converter</param>
        /// <param name="dtype">Column dtype name</param>
        /// <returns>Array of managed values</returns>
        static object[] ReadColumnValues(PyObject series, IColumnConverter converter, string dtype)
        {
            // Date/Datetime/Duration/Time are read from the physical representation: to_list() returns python
            // date/datetime/timedelta objects, which lose the nanosecond precision and fail outside 1..9999
            if ((converter is DateTimeConverter || converter is TimeSpanConverter) &&
                DtypeUnits.GetTemporalKind(dtype) != TemporalKind.None)
            {
                var physicalValues = ReadPhysicalValues(series, converter, dtype);
                if (physicalValues != null) return physicalValues;
            }

            // Categorical/Enum must be read as values: to_physical() returns their UInt32 codes
            using (PyObject values = series.InvokeMethod("to_list"))
                return converter.ConvertValues(ToObjectArray(values));
        }

        /// <summary>
        /// Reads the values via <c>to_physical()</c> and scales them to nanoseconds
        /// </summary>
        /// <param name="series">polars.Series</param>
        /// <param name="converter">Column converter</param>
        /// <param name="dtype">Column dtype name</param>
        /// <returns>Array of managed values or null when the physical representation is not available</returns>
        static object[] ReadPhysicalValues(PyObject series, IColumnConverter converter, string dtype)
        {
            object[] values;
            var nanosecondsPerUnit = DtypeUnits.GetNanosecondsPerUnit(dtype);

            try
            {
                using (PyObject physical = series.InvokeMethod("to_physical"))
                using (PyObject physicalValues = physical.InvokeMethod("to_list"))
                    values = converter.ConvertValues(
                        ScaleToNanoseconds(ToObjectArray(physicalValues), nanosecondsPerUnit));
            }
            catch (PythonException)
            {
                // Incompatible polars version or an extension dtype: python date/datetime/timedelta are used
                return null;
            }

            // Time zone aware Datetime physical values are the UTC epoch: the column keeps the UTC clock value.
            // DateTime.Kind can't be preserved here: DataColumn of type DateTime normalizes it to Unspecified
            return values;
        }
    }
}
