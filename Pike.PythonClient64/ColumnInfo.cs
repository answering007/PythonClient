using System;
using Python.Runtime;

namespace Pike.PythonClient64
{
    /// <summary>
    /// Represent pandas.DataFrame column info
    /// </summary>
    public class ColumnInfo
    {
        private readonly PythonUtils _utils;

        /// <summary>
        /// Create an instance of <see cref="ColumnInfo"/>
        /// </summary>
        /// <param name="utils">Python utilities</param>
        /// <param name="name">Column name</param>
        /// <param name="pyType">Column type</param>
        public ColumnInfo(PythonUtils utils, string name, PyType pyType)
        {
            _utils = utils ?? throw new ArgumentNullException(nameof(utils));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name can't be null or empty", nameof(name));

            Name = name;
            PythonType = pyType;
            switch (PythonType)
            {
                case PyType.Undefined:
                    ManagedType = typeof(string);
                    break;
                case PyType.BoolType:
                    ManagedType = typeof(bool);
                    break;
                case PyType.Int8Type:
                    ManagedType = typeof(sbyte);
                    break;
                case PyType.Uint8Type:
                    ManagedType = typeof(byte);
                    break;
                case PyType.Int16Type:
                    ManagedType = typeof(short);
                    break;
                case PyType.Uint16Type:
                    ManagedType = typeof(ushort);
                    break;
                case PyType.Uint32Type:
                    ManagedType = typeof(uint);
                    break;
                case PyType.Int64Type:
                    ManagedType = typeof(long);
                    break;
                case PyType.Uint64Type:
                    ManagedType = typeof(ulong);
                    break;
                case PyType.Int32Type:
                    ManagedType = typeof(int);
                    break;
                case PyType.Float16Type:
                    ManagedType = typeof(float);
                    break;
                case PyType.Float32Type:
                    ManagedType = typeof(float);
                    break;
                case PyType.Float64Type:
                    ManagedType = typeof(double);
                    break;
                case PyType.Complex128Type:
                    ManagedType = typeof(string);
                    break;
                case PyType.Complex64Type:
                    ManagedType = typeof(string);
                    break;
                case PyType.ObjectType:
                    ManagedType = typeof(string);
                    break;
                case PyType.Datetime64Type:
                    ManagedType = typeof(DateTime);
                    break;
                case PyType.Timedelta64Type:
                    ManagedType = typeof(TimeSpan);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Column name
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Column type
        /// </summary>
        public PyType PythonType { get; }

        /// <summary>
        /// Managed column type
        /// </summary>
        public Type ManagedType { get; }

        /// <summary>
        /// Get managed value from python value
        /// </summary>
        /// <param name="pyObject">Python value</param>
        /// <returns>Managed value</returns>
        public object GetManagedValue(PyObject pyObject)
        {
            if (pyObject == null) throw new ArgumentNullException(nameof(pyObject));

            var isNoneOrNaN = _utils.IsNoneOrNaN(pyObject);
            if (isNoneOrNaN) return null;

            if (PythonType == PyType.Datetime64Type)
                return _utils.GetDateTime(pyObject);

            if (PythonType == PyType.Timedelta64Type)
                return _utils.GetTimeSpan(pyObject);

            return pyObject.AsManagedObject(ManagedType);
        }
    }
}