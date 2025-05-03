using System;

namespace Pike.PythonClient64.ColumnConverters
{
    /// <summary>
    /// Interface for dataframe column values converter
    /// </summary>
    public interface IColumnConverter
    {
        /// <summary>
        /// Python type name
        /// </summary>
        string PythonTypeName { get; }
        
        /// <summary>
        /// Managed type
        /// </summary>
        Type TargetType { get; }

        /// <summary>
        /// Convert python values to managed values
        /// </summary>
        /// <param name="pythonValues">Array of python values</param>
        /// <returns>Array of managed values</returns>
        object[] ConvertValues(object[] pythonValues);
    }
}
