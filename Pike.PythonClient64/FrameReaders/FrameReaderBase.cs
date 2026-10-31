using System;
using System.Collections.Generic;
using System.Data;
using Python.Runtime;
using Pike.PythonClient64.ColumnConverters;

namespace Pike.PythonClient64.FrameReaders
{
    /// <summary>
    /// Conversion helpers shared by the pandas and polars frame readers
    /// </summary>
    internal abstract class FrameReaderBase
    {
        /// <summary>
        /// Gets the converter for the column dtype. Unknown dtype is rejected in strict mode (default)
        /// </summary>
        /// <param name="columnName">Column name</param>
        /// <param name="dtype">dtype name</param>
        /// <returns>Column converter</returns>
        /// <exception cref="NotSupportedException">Thrown when dtype has no registered converter and strict mode is on</exception>
        protected static IColumnConverter GetColumnConverter(string columnName, string dtype)
        {
            if (SupportedTypes.TryGetConverter(dtype, out var converter)) return converter;
            if (PythonDataQuery.StrictColumnTypes)
                throw new NotSupportedException($"Column '{columnName}' has unsupported dtype '{dtype}'");

            // Fallback for consumers that prefer string conversion over the exception
            return SupportedTypes.GetConverter("object");
        }

        /// <summary>
        /// Gets the converter for a dtype that has no unambiguous .NET representation (polars nested types and so on).
        /// Strict mode (default) rejects it, otherwise the value is converted to string
        /// </summary>
        /// <param name="columnName">Column name</param>
        /// <param name="dtype">dtype name</param>
        /// <param name="forbidden">True when the dtype is not supported by the provider at all</param>
        /// <returns>Column converter</returns>
        /// <exception cref="NotSupportedException">Thrown when the dtype is forbidden and strict mode is on</exception>
        protected static IColumnConverter GetColumnConverter(string columnName, string dtype, bool forbidden)
        {
            if (!forbidden) return GetColumnConverter(columnName, dtype);
            if (PythonDataQuery.StrictColumnTypes)
                throw new NotSupportedException($"Column '{columnName}' has unsupported dtype '{dtype}'");

            return SupportedTypes.GetConverter("object");
        }

        /// <summary>
        /// Makes a unique valid <see cref="DataColumn"/> name from the python column name
        /// </summary>
        /// <param name="name">Python column name</param>
        /// <param name="index">Column index</param>
        /// <param name="usedNames">Names that are already used by the table</param>
        /// <returns>Unique valid column name</returns>
        protected static string MakeColumnName(string name, int index, ISet<string> usedNames)
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
        /// Adds all rows to the table. Change notifications are disabled, the row buffer is reused
        /// </summary>
        /// <param name="dataTable">Target table with the columns</param>
        /// <param name="columnValues">Values of every column</param>
        protected static void FillRows(DataTable dataTable, object[][] columnValues)
        {
            if (columnValues.Length == 0) return;

            var rowCount = columnValues[0]?.Length ?? 0;
            if (rowCount == 0) return;

            var columnsCount = columnValues.Length;
            var rowValues = new object[columnsCount];

            dataTable.BeginLoadData();
            try
            {
                for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
                {
                    for (var columnIndex = 0; columnIndex < columnsCount; columnIndex++)
                        rowValues[columnIndex] = columnValues[columnIndex][rowIndex];

                    // Rows.Add copies the values, so the buffer is reused for the next row
                    dataTable.Rows.Add(rowValues);
                }
            }
            finally
            {
                dataTable.EndLoadData();
            }
        }

        /// <summary>
        /// Scales integer values to nanoseconds (converter contract), NaT and None are kept as is
        /// </summary>
        /// <param name="values">Values in the dtype resolution</param>
        /// <param name="nanosecondsPerUnit">Nanoseconds per dtype unit</param>
        /// <returns>Values in nanoseconds</returns>
        protected static object[] ScaleToNanoseconds(object[] values, long nanosecondsPerUnit)
        {
            if (nanosecondsPerUnit == 1) return values;

            var result = new object[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                // Values come from tolist()/to_list() as python objects, so they are converted before scaling
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
        /// Converts the python list to the array of python objects
        /// </summary>
        /// <param name="list">Python list</param>
        /// <returns>Array of python values</returns>
        protected static object[] ToObjectArray(PyObject list)
        {
            return (object[])(dynamic)list;
        }
    }
}
