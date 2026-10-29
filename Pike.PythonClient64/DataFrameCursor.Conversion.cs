using System;
using System.Collections.Generic;
using System.Linq;
using Pike.PythonClient64.ColumnConverters;
using Python.Runtime;

namespace Pike.PythonClient64
{
    /// <summary>
    /// DataFrame description and chunk conversion
    /// </summary>
    internal sealed partial class DataFrameCursor
    {
        /// <summary>
        /// Reads column names, dtypes and converters. Duplicated labels are rejected, since
        /// <c>DataFrame[label]</c> returns a DataFrame for them instead of a Series
        /// </summary>
        /// <param name="dataFrame">Python DataFrame</param>
        /// <returns>Column metadata</returns>
        /// <exception cref="NotSupportedException">Thrown when a dtype is not supported or columns are duplicated</exception>
        static IReadOnlyList<DataFrameColumn> ReadColumns(PyObject dataFrame)
        {
            var labels = new List<PyObject>();
            var sourceNames = new List<string>();

            using (PyObject columnsObject = dataFrame.GetAttr("columns"))
            using (PyObject columnNames = columnsObject.InvokeMethod("tolist"))
            {
                var columnsCount = (int)columnNames.Length();
                for (var i = 0; i < columnsCount; i++)
                {
                    var label = columnNames.GetItem(i);
                    labels.Add(label);
                    sourceNames.Add(label.ToString());
                }
            }

            try
            {
                // Duplicated labels are checked before any column is read: DataFrame[label] returns a DataFrame
                // (not a Series) for them, so a dtype lookup would fail with an uninformative python error
                var duplicate = sourceNames
                    .GroupBy(name => name, StringComparer.Ordinal)
                    .FirstOrDefault(group => group.Count() > 1);

                if (duplicate != null)
                    throw new NotSupportedException($"Duplicated column names are not supported: {duplicate.Key}");

                var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var result = new List<DataFrameColumn>(labels.Count);

                for (var i = 0; i < labels.Count; i++)
                {
                    using (PyObject series = dataFrame.GetItem(labels[i]))
                    using (PyObject dtype = series.GetAttr("dtype"))
                    {
                        var dtypeName = dtype.ToString();
                        var converter = GetConverter(sourceNames[i], dtypeName);

                        result.Add(new DataFrameColumn(labels[i], MakeColumnName(sourceNames[i], i, usedNames),
                            dtypeName, converter));
                    }
                }

                return result;
            }
            catch (Exception)
            {
                // The cursor keeps the labels only on success, so they must not leak when the description fails
                foreach (var label in labels)
                    label.Dispose();

                throw;
            }
        }

        /// <summary>
        /// Gets the converter for the column dtype. Unknown dtype is rejected in strict mode (default)
        /// </summary>
        /// <param name="columnName">Column name</param>
        /// <param name="dtype">dtype name</param>
        /// <returns>Column converter</returns>
        /// <exception cref="NotSupportedException">Thrown when dtype has no registered converter and strict mode is on</exception>
        static IColumnConverter GetConverter(string columnName, string dtype)
        {
            if (SupportedTypes.TryGetConverter(dtype, out var converter)) return converter;
            if (PythonDataQuery.StrictColumnTypes)
                throw new NotSupportedException($"Column '{columnName}' has unsupported dtype '{dtype}'");

            // Fallback for consumers that prefer string conversion over the exception
            return SupportedTypes.GetConverter("object");
        }

        /// <summary>
        /// Makes a unique valid <see cref="System.Data.DataColumn"/> name from the python column name
        /// </summary>
        /// <param name="name">Python column name</param>
        /// <param name="index">Column index</param>
        /// <param name="usedNames">Names that are already used by the table</param>
        /// <returns>Unique valid column name</returns>
        static string MakeColumnName(string name, int index, ISet<string> usedNames)
        {
            if (string.IsNullOrWhiteSpace(name)) name = $"Column{index}";

            // '[', ']' and '.' are not allowed in the DataColumn name
            name = name.Replace("[", "(").Replace("]", ")").Replace(".", "_");

            var uniqueName = name;
            for (var i = 2; !usedNames.Add(uniqueName); i++)
                uniqueName = $"{name}_{i}";

            return uniqueName;
        }
        /// <summary>
        /// Takes the rows of the specified range as a DataFrame view
        /// </summary>
        /// <param name="start">First row index</param>
        /// <param name="stop">Row index after the last row</param>
        /// <returns>DataFrame with the requested rows</returns>
        PyObject TakeRows(int start, int stop)
        {
            using (PyObject startObject = start.ToPython())
            using (PyObject stopObject = stop.ToPython())
            using (PyObject sliceType = Py.Import("builtins").GetAttr("slice"))
            using (PyObject rowSlice = sliceType.Invoke(new[] { startObject, stopObject }))
            using (PyObject iloc = _dataFrame.GetAttr("iloc"))
                return iloc.InvokeMethod("__getitem__", new[] { rowSlice });
        }

        /// <summary>
        /// Reads and converts the column values of the frame (or of the current chunk)
        /// </summary>
        /// <param name="frame">DataFrame to read</param>
        /// <param name="column">Column metadata</param>
        /// <returns>Array of managed values</returns>
        static object[] ReadColumnValues(PyObject frame, DataFrameColumn column)
        {
            using (PyObject series = frame.GetItem(column.Label))
            {
                // datetime64/timedelta64 are read as exact integers, so NaT and the resolution do not depend on tolist()
                if (column.Converter is DateTimeConverter || column.Converter is TimeSpanConverter)
                {
                    var nanosecondsPerUnit = GetNanosecondsPerUnit(column.Dtype);

                    try
                    {
                        using (PyObject int64Name = new PyString("int64"))
                        using (PyObject int64Series = series.InvokeMethod("astype", new[] { int64Name }))
                            return column.Converter.ConvertValues(
                                ScaleToNanoseconds(ConvertSeriesValues(int64Series), nanosecondsPerUnit));
                    }
                    catch (PythonException)
                    {
                        // tz-aware datetime64[ns, UTC] can't be cast to int64, python datetime objects are used instead
                    }
                }

                return column.Converter.ConvertValues(ConvertSeriesValues(series));
            }
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
                return (object[])(dynamic)valuesList;
        }

        /// <summary>
        /// Scales integer values to nanoseconds (converter contract), NaT and None are kept as is
        /// </summary>
        /// <param name="values">Values in the dtype resolution</param>
        /// <param name="nanosecondsPerUnit">Nanoseconds per dtype unit</param>
        /// <returns>Values in nanoseconds</returns>
        static object[] ScaleToNanoseconds(object[] values, long nanosecondsPerUnit)
        {
            if (nanosecondsPerUnit == 1) return values;

            var result = new object[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                // Values come from tolist() as python objects, so they are converted before scaling
                if (!PythonValue.TryToInt64(values[i], out var int64Value) ||
                    int64Value == PythonValue.NaT ||
                    int64Value > long.MaxValue / nanosecondsPerUnit ||
                    int64Value < long.MinValue / nanosecondsPerUnit)
                {
                    result[i] = values[i];
                    continue;
                }

                result[i] = int64Value * nanosecondsPerUnit;
            }

            return result;
        }

        /// <summary>
        /// Gets the dtype unit: pandas uses datetime64[ns], datetime64[us] (pandas 3 default) and so on
        /// </summary>
        /// <param name="dtype">Column dtype name</param>
        /// <returns>Unit name, empty string when the dtype has no unit</returns>
        static string GetDtypeUnit(string dtype)
        {
            if (string.IsNullOrEmpty(dtype)) return string.Empty;

            var start = dtype.IndexOf('[');
            if (start < 0) return string.Empty;

            var end = dtype.IndexOfAny(new[] { ',', ']' }, start + 1);
            if (end < 0) return string.Empty;

            return dtype.Substring(start + 1, end - start - 1).Trim();
        }

        /// <summary>
        /// Gets the number of nanoseconds per dtype unit
        /// </summary>
        /// <param name="dtype">Column dtype name</param>
        /// <returns>Nanoseconds per unit, 1 when the unit is unknown</returns>
        static long GetNanosecondsPerUnit(string dtype)
        {
            switch (GetDtypeUnit(dtype))
            {
                case "ns":
                    return 1;
                case "us":
                    return 1000;
                case "ms":
                    return 1000000;
                case "s":
                    return 1000000000;
                case "m":
                    return 60L * 1000000000;
                case "h":
                    return 3600L * 1000000000;
                case "D":
                    return 24L * 3600 * 1000000000;
                default:
                    return 1;
            }
        }
    }
}
