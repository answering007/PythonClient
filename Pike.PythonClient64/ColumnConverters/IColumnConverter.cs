using System;

namespace Pike.PythonClient64.ColumnConverters
{
    public interface IColumnConverter
    {
        string PythonTypeName { get; }
        
        Type TargetType { get; }

        object[] ConvertValues(object[] pythonValues);
    }
}
