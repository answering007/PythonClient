namespace Pike.PythonClient64
{
    /// <summary>
    /// Python data types
    /// </summary>
    public enum PyType
    {
        Undefined = 0,
        BoolType = 1,
        Int8Type = 2,
        Uint8Type = 3,
        Int16Type = 4,
        Uint16Type = 5,
        Uint32Type = 7,
        Int64Type = 9,
        Uint64Type = 10,
        Int32Type = 11,
        Float16Type = 12,
        Float32Type = 13,
        Float64Type = 14,
        Complex128Type = 16,
        Complex64Type = 17,
        ObjectType = 19,
        Datetime64Type = 23,
        Timedelta64Type = 24
    }
}