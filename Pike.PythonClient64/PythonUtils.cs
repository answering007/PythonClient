using System;
using System.Data;
using System.IO;
using System.Reflection;
using Python.Runtime;

namespace Pike.PythonClient64
{
    /// <inheritdoc />
    /// <summary>
    /// Python help utilities
    /// </summary>
    public class PythonUtils: IDisposable
    {
        static PythonUtils()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"{assembly.GetName().Name}.Helper.py";

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                using (var reader = new StreamReader(stream ?? throw new InvalidOperationException()))
                {
                    ModuleText = reader.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// Convert pandas DataFrame to <see cref="DataTable"/>
        /// </summary>
        /// <param name="pyObject">Pandas DataFrame object</param>
        /// <returns>Result table</returns>
        public static DataTable DataFrameToDataTable(dynamic pyObject)
        {
            if (pyObject == null) throw new ArgumentNullException(nameof(pyObject));

            using (var utilities = new PythonUtils())
            {
                if (!utilities.IsDataFrame(pyObject)) throw new ArgumentException("Object is not a pandas dataframe", nameof(pyObject));

                //Define columns info
                var columnNames = utilities.GetDataFrameColumnNames(pyObject);
                var columns = new ColumnInfo[columnNames.Length];

                for (var i = 0; i < columns.Length; i++)
                {
                    var name = columnNames[i];
                    var type = utilities.GetTypeCode(pyObject, i);

                    columns[i] = new ColumnInfo(utilities, name, type);
                }

                //Define result table columns
                var dataTable = new DataTable("Result");
                foreach (var column in columns)
                {
                    var name = column.Name;
                    var dataColumn = new DataColumn(name)
                    {
                        Caption = name,
                        DataType = column.ManagedType,
                        AllowDBNull = true
                    };
                    dataTable.Columns.Add(dataColumn);
                }

                //Iterate for each row
                var rowCount = ((PyObject)pyObject.index).Length();
                for (long i = 0; i < rowCount; i++)
                {
                    var row = dataTable.NewRow();
                    for (var j = 0; j < columns.Length; j++)
                    {
                        var column = columns[j];
                        var pyValue = utilities.GetDataFrameItem(pyObject, i, j);
                        var value = pyValue == null ? null : column.GetManagedValue(pyValue);
                        row[j] = value ?? DBNull.Value;
                    }

                    dataTable.Rows.Add(row);
                }

                return dataTable;
            }
        }

        static readonly DateTime NumPyDateTime = new DateTime(1970, 1, 1);
        static readonly string ModuleText;
        dynamic _module = PythonEngine.ModuleFromString("helper", ModuleText);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pyObject"></param>
        /// <returns></returns>
        public string[] GetDataFrameColumnNames(PyObject pyObject)
        {
            if (pyObject == null) throw new ArgumentNullException(nameof(pyObject));
            if (!IsDataFrame(pyObject)) throw new ArgumentException("Argument is not a pandas dataframe", nameof(pyObject));

            return _module.getColumnNames(pyObject).As<string[]>();
        }

        /// <summary>
        /// True if <paramref name="pyObject"/> is a pandas DataFrame; otherwise false
        /// </summary>
        /// <param name="pyObject">Python object</param>
        /// <returns>True of false</returns>
        public bool IsDataFrame(PyObject pyObject)
        {
            if (pyObject == null) throw new ArgumentNullException(nameof(pyObject));

            return _module.isDataframe(pyObject).As<bool>();
        }

        /// <summary>
        /// True if <paramref name="pyObject"/> is None or NaN; otherwise false
        /// </summary>
        /// <param name="pyObject">Python object</param>
        /// <returns>True of false</returns>
        public bool IsNoneOrNaN(PyObject pyObject)
        {
            if (pyObject == null) throw new ArgumentNullException(nameof(pyObject));

            return _module.isNoneOrNaN(pyObject).As<bool>();
        }

        /// <summary>
        /// Get type enumeration from python DataFrame column by index
        /// </summary>
        /// <param name="pyObject">DataFrame</param>
        /// <param name="index">Column index</param>
        /// <returns>Associated <see cref="PyType"/> value</returns>
        public PyType GetTypeCode(PyObject pyObject, int index)
        {
            if (pyObject == null) throw new ArgumentNullException(nameof(pyObject));
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));

            return (PyType)_module.getTypeNo(pyObject, new PyInt(index)).As<int>();
        }

        /// <summary>
        /// Get managed value from numpy.datetime64 value
        /// </summary>
        /// <param name="pyObject">Numpy.datetime64 value</param>
        /// <returns>Associated managed value</returns>
        public DateTime GetDateTime(PyObject pyObject)
        {
            if (pyObject == null) throw new ArgumentNullException(nameof(pyObject));

            var numPyTicks = _module.dateTimeToTicks(pyObject).As<long>();
            return NumPyDateTime.AddTicks(numPyTicks);
        }

        /// <summary>
        /// Get managed value from numpy.timedelta64 value
        /// </summary>
        /// <param name="pyObject">Numpy.timedelta64 value</param>
        /// <returns>Associated managed value</returns>
        public TimeSpan GetTimeSpan(PyObject pyObject)
        {
            if (pyObject == null) throw new ArgumentNullException(nameof(pyObject));

            var numPyTicks = _module.timeDeltaToTicks(pyObject).As<long>();
            return TimeSpan.FromTicks(numPyTicks);
        }

        /// <summary>
        /// Get python item from pandas.DataFrame for specified row index and column index
        /// </summary>
        /// <param name="dataframe">Pandas.DataFrame object</param>
        /// <param name="row">Row index</param>
        /// <param name="column">Column index</param>
        /// <returns></returns>
        public PyObject GetDataFrameItem(PyObject dataframe, long row, long column)
        {
            if (dataframe == null) throw new ArgumentNullException(nameof(dataframe));
            if (row < 0) throw new ArgumentOutOfRangeException(nameof(row));
            if (column < 0) throw new ArgumentOutOfRangeException(nameof(column));

            return _module.getDataFrameItem(dataframe, row, column);
        }

        /// <summary>
        /// Get python boolean value from managed value
        /// </summary>
        /// <param name="value">Managed boolean value</param>
        /// <returns>Python boolean value</returns>
        public PyObject GetBool(bool value)
        {
            var b = value ? (byte)1 : (byte)0;
            return _module.getBoolValue(new PyInt(b));
        }

        /// <summary>
        /// Get numpy.datetime64 value from managed value
        /// </summary>
        /// <param name="value">Managed DateTime value</param>
        /// <returns>Numpy.datetime64 value</returns>
        public PyObject GetDateTime(DateTime value)
        {
            var ticks = value.Ticks - NumPyDateTime.Ticks;
            return _module.ticksToDateTime(new PyLong(ticks));
        }

        /// <summary>
        /// Release resources
        /// </summary>
        public void Dispose()
        {
            if (_module == null) return;

            _module.Dispose();
            _module = null;
        }
    }
}