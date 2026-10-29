using System;
using Pike.PythonClient64.ColumnConverters;
using Python.Runtime;

namespace Pike.PythonClient64
{
    /// <summary>
    /// Column metadata of a DataFrame: managed type, converter and names
    /// </summary>
    internal sealed class DataFrameColumn
    {
        /// <summary>
        /// Create a column metadata instance
        /// </summary>
        /// <param name="label">Original python label</param>
        /// <param name="name">Valid unique column name for <see cref="System.Data.DataColumn"/></param>
        /// <param name="dtype">Python dtype name</param>
        /// <param name="converter">Column converter</param>
        public DataFrameColumn(PyObject label, string name, string dtype, IColumnConverter converter)
        {
            Label = label;
            Name = name;
            Dtype = dtype;
            Converter = converter;
        }

        /// <summary>
        /// Original python label. The series are selected by it, so non-string labels (0, 1, tuples) work
        /// </summary>
        public PyObject Label { get; }

        /// <summary>
        /// Valid unique name for <see cref="System.Data.DataColumn"/>
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Python dtype name
        /// </summary>
        public string Dtype { get; }

        /// <summary>
        /// Column converter
        /// </summary>
        public IColumnConverter Converter { get; }

        /// <summary>
        /// Managed column type
        /// </summary>
        public Type DataType => Converter.TargetType;
    }
}
