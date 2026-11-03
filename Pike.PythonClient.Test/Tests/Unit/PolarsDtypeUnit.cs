using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64;
using Pike.PythonClient64.ColumnConverters;

namespace Pike.PythonClient.Test.Tests.Unit
{
    /// <summary>
    /// polars dtype registry and dtype normalization tests. The tests do not require the python runtime
    /// </summary>
    [TestClass]
    [TestCategory("Unit")]
    public class PolarsDtypeUnit
    {
        /// <summary>
        /// Restores the default converters after each test
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            SupportedTypes.Reset();
        }

        /// <summary>
        /// polars dtype parameters in round brackets are removed
        /// </summary>
        [TestMethod]
        public void NormalizeDtype_PolarsParametersAreRemoved()
        {
            Assert.AreEqual("Datetime", SupportedTypes.NormalizeDtype("Datetime(time_unit='us', time_zone=None)"));
            Assert.AreEqual("Datetime", SupportedTypes.NormalizeDtype("Datetime(time_unit='ns', time_zone='UTC')"));
            Assert.AreEqual("Duration", SupportedTypes.NormalizeDtype("Duration(time_unit='ms')"));
            Assert.AreEqual("Decimal", SupportedTypes.NormalizeDtype("Decimal(precision=38, scale=2)"));
            Assert.AreEqual("List", SupportedTypes.NormalizeDtype("List(String)"));
            Assert.AreEqual("Array", SupportedTypes.NormalizeDtype("Array(Int64, 3)"));
            Assert.AreEqual("Struct", SupportedTypes.NormalizeDtype("Struct({'a': Int64})"));
            Assert.AreEqual("Enum", SupportedTypes.NormalizeDtype("Enum(categories=['a', 'b'])"));
        }

        /// <summary>
        /// The pandas dtype normalization is not changed by the polars support
        /// </summary>
        [TestMethod]
        public void NormalizeDtype_PandasBehaviourIsPreserved()
        {
            Assert.AreEqual("datetime64", SupportedTypes.NormalizeDtype("datetime64[ns, UTC]"));
            Assert.AreEqual("timedelta64", SupportedTypes.NormalizeDtype("timedelta64[ns]"));
            Assert.AreEqual("string", SupportedTypes.NormalizeDtype("string[python]"));
            Assert.AreEqual("i8", SupportedTypes.NormalizeDtype("<i8"));
            Assert.AreEqual("O", SupportedTypes.NormalizeDtype("|O"));
            Assert.AreEqual("Int64", SupportedTypes.NormalizeDtype("  Int64  "));
            Assert.AreEqual(string.Empty, SupportedTypes.NormalizeDtype(null));
        }

        /// <summary>
        /// polars dtype names have registered converters
        /// </summary>
        [TestMethod]
        public void Registry_SupportsPolarsDtypes()
        {
            var dtypes = new[]
            {
                "Boolean", "Int8", "Int16", "Int32", "Int64", "UInt8", "UInt16", "UInt32", "UInt64",
                "Float16", "Float32", "Float64", "String", "Utf8", "Date", "Datetime", "Duration", "Time",
                "Categorical", "Enum", "Decimal", "Binary", "Null",
                "Datetime(time_unit='us', time_zone=None)", "Duration(time_unit='ms')",
                "Decimal(precision=38, scale=2)"
            };

            foreach (var dtype in dtypes)
                Assert.IsTrue(SupportedTypes.TryGetConverter(dtype, out _), $"dtype '{dtype}' is not supported");
        }

        /// <summary>
        /// polars dtype names are mapped to the expected converters
        /// </summary>
        [TestMethod]
        public void Registry_ReturnsExpectedPolarsConverters()
        {
            Assert.IsInstanceOfType(SupportedTypes.GetConverter("Datetime"), typeof(DateTimeConverter));
            Assert.IsInstanceOfType(SupportedTypes.GetConverter("Date"), typeof(DateTimeConverter));
            Assert.IsInstanceOfType(SupportedTypes.GetConverter("Duration"), typeof(TimeSpanConverter));
            Assert.IsInstanceOfType(SupportedTypes.GetConverter("Time"), typeof(TimeSpanConverter));
            Assert.IsInstanceOfType(SupportedTypes.GetConverter("Categorical"), typeof(StringConverter));
            Assert.IsInstanceOfType(SupportedTypes.GetConverter("Enum"), typeof(StringConverter));
            Assert.IsInstanceOfType(SupportedTypes.GetConverter("Utf8"), typeof(StringConverter));
            Assert.IsInstanceOfType(SupportedTypes.GetConverter("Null"), typeof(StringConverter));
            Assert.IsInstanceOfType(SupportedTypes.GetConverter("Float16"), typeof(DoubleConverter));
            Assert.IsInstanceOfType(SupportedTypes.GetConverter("Decimal"), typeof(DecimalConverter));
            Assert.IsInstanceOfType(SupportedTypes.GetConverter("Binary"), typeof(BinaryConverter));
        }

        /// <summary>
        /// The binary converter returns byte arrays, missing values become DBNull
        /// </summary>
        [TestMethod]
        public void BinaryConverter_ConvertsBytes()
        {
            var converter = new BinaryConverter();

            var values = converter.ConvertValues(new object[] { new byte[] { 1, 2 }, null, DBNull.Value, "text" });

            CollectionAssert.AreEqual(new byte[] { 1, 2 }, (byte[])values[0]);
            Assert.AreEqual(DBNull.Value, values[1]);
            Assert.AreEqual(DBNull.Value, values[2]);
            Assert.AreEqual(DBNull.Value, values[3]);
            Assert.AreEqual(typeof(byte[]), converter.TargetType);
        }
    }
}
