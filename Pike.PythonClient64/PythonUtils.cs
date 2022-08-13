using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Python.Runtime;

namespace Pike.PythonClient64
{
    /// <inheritdoc />
    /// <summary>
    /// Python help utilities
    /// </summary>
    public partial class PythonUtils: IDisposable
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

        static readonly DateTime NumPyDateTime = new DateTime(1970, 1, 1);
        static readonly string ModuleText;
        dynamic _module = PythonEngine.ModuleFromString("helper", ModuleText);

        string SerializeDataFrame(dynamic pyObject)
        {
            return _module.serializeDataFrame(pyObject).As<string>();
        }

        /// <summary>
        /// Deserialize data table from pandas dataframe
        /// </summary>
        /// <param name="pyObject">Pandas dataframe object</param>
        /// <returns>Result data table</returns>
        public static DataTable DeserializeTable(dynamic pyObject)
        {
            if (pyObject == null) throw new ArgumentNullException(nameof(pyObject));

            using (var utilities = new PythonUtils())
            {
                if (!utilities.IsDataFrame(pyObject)) throw new ArgumentException("Object is not a pandas dataframe", nameof(pyObject));

                //Serialize data to json and transfer
                var json = utilities.SerializeDataFrame(pyObject);

                //Deserialize data to JObject
                JObject deserialize = JsonConvert.DeserializeObject<JObject>(json);

                //Read fields data types and create table
                var fields = deserialize["schema"]["fields"].ToObject<FieldInfo[]>().ToDictionary(f => f.Name); //Check for unique column names
                var resulTable = new DataTable();
                foreach (var field in fields.Values)
                {
                    resulTable.Columns.Add(new DataColumn(field.Name)
                    {
                        DataType = field.ManagedType,
                        AllowDBNull = true
                    });
                }

                //Create serializer and deserialize data
                var serializer = new JsonSerializer();
                serializer.Converters.Add(new DataTableReadConverter { ResulTable = resulTable });
                resulTable = deserialize["data"].ToObject<DataTable>(serializer);

                return resulTable;
            }
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