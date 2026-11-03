using System;
using System.Collections.Generic;

namespace Pike.PythonClient64.FrameReaders
{
    /// <summary>
    /// polars data types that have no unambiguous .NET/DataTable representation
    /// </summary>
    internal static class PolarsDtypes
    {
        static readonly HashSet<string> Forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "List", "Array", "Struct", "Map", "Object", "Unknown", "Extension", "Int128", "UInt128"
        };

        /// <summary>
        /// True when the dtype must be rejected in strict mode (or converted to string otherwise)
        /// </summary>
        /// <param name="dtype">dtype name</param>
        /// <returns>True for the unsupported dtype</returns>
        public static bool IsForbidden(string dtype)
        {
            return Forbidden.Contains(SupportedTypes.NormalizeDtype(dtype));
        }
    }
}
