using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64;
using Pike.PythonClient64.ColumnConverters;

namespace Pike.PythonClient.Test.Tests.Unit
{
    /// <summary>
    /// Unit tests for <see cref="SupportedTypes"/> registry (regression tests for P0-9, P2-6).
    /// The tests do not require Python runtime
    /// </summary>
    [TestClass]
    [TestCategory("Unit")]
    public class SupportedTypesUnit
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
        /// dtype parameters and byte order markers are removed (P0-9)
        /// </summary>
        [TestMethod]
        public void P0_9_NormalizeDtype_RemovesParameters()
        {
            Assert.AreEqual("datetime64", SupportedTypes.NormalizeDtype("datetime64[ns, UTC]"));
            Assert.AreEqual("datetime64", SupportedTypes.NormalizeDtype("datetime64[ns]"));
            Assert.AreEqual("timedelta64", SupportedTypes.NormalizeDtype("timedelta64[ns]"));
            Assert.AreEqual("string", SupportedTypes.NormalizeDtype("string[python]"));
            Assert.AreEqual("i8", SupportedTypes.NormalizeDtype("<i8"));
            Assert.AreEqual("O", SupportedTypes.NormalizeDtype("|O"));
            Assert.AreEqual("Int64", SupportedTypes.NormalizeDtype("  Int64  "));
            Assert.AreEqual(string.Empty, SupportedTypes.NormalizeDtype(null));
            Assert.AreEqual(string.Empty, SupportedTypes.NormalizeDtype("   "));
        }

        /// <summary>
        /// numpy and nullable pandas extension dtypes are supported (P0-9)
        /// </summary>
        [TestMethod]
        public void P0_9_Registry_SupportsNumpyAndNullableDtypes()
        {
            var supported = new[]
            {
                "bool", "boolean", "int8", "int16", "int32", "int64", "uint8", "uint16", "uint32", "uint64",
                "float32", "float64", "datetime64[ns]", "datetime64[ns, UTC]", "timedelta64[ns]",
                "object", "string", "string[python]", "category", "Int8", "Int64", "UInt32", "Float64"
            };

            foreach (var dtype in supported)
                Assert.IsTrue(SupportedTypes.TryGetConverter(dtype, out _), $"dtype '{dtype}' is not supported");
        }

        /// <summary>
        /// Converters are registered per managed type (P0-9)
        /// </summary>
        [TestMethod]
        public void P0_9_Registry_ReturnsExpectedConverters()
        {
            Assert.IsInstanceOfType(SupportedTypes.GetConverter("bool"), typeof(BooleanConverter));
            Assert.IsInstanceOfType(SupportedTypes.GetConverter("Boolean"), typeof(BooleanConverter));
            Assert.IsInstanceOfType(SupportedTypes.GetConverter("int32"), typeof(IntegerConverter));
            Assert.IsInstanceOfType(SupportedTypes.GetConverter("Int64"), typeof(LongConverter));
            Assert.IsInstanceOfType(SupportedTypes.GetConverter("float32"), typeof(DoubleConverter));
            Assert.IsInstanceOfType(SupportedTypes.GetConverter("datetime64[ns]"), typeof(DateTimeConverter));
            Assert.IsInstanceOfType(SupportedTypes.GetConverter("timedelta64[ns]"), typeof(TimeSpanConverter));
            Assert.IsInstanceOfType(SupportedTypes.GetConverter("string[python]"), typeof(StringConverter));
        }

        /// <summary>
        /// Unknown dtype must be reported instead of a silent string conversion (P0-9)
        /// </summary>
        [TestMethod]
        public void P0_9_UnknownDtypeThrows()
        {
            Assert.IsFalse(SupportedTypes.TryGetConverter("complex128", out _));
            Assert.ThrowsException<NotSupportedException>(() => SupportedTypes.GetConverter("complex128"));
        }

        /// <summary>
        /// Custom converters can be registered and the registry can be reset (P2-6)
        /// </summary>
        [TestMethod]
        public void P0_9_RegisterAndReset()
        {
            var converter = new TestConverter();

            SupportedTypes.Register(converter, "test_dtype[ns]");

            Assert.IsTrue(SupportedTypes.TryGetConverter("test_dtype", out var registered));
            Assert.AreSame(converter, registered);

            SupportedTypes.Reset();

            Assert.IsFalse(SupportedTypes.TryGetConverter("test_dtype", out _));
            Assert.IsInstanceOfType(SupportedTypes.GetConverter("int32"), typeof(IntegerConverter));
        }

        /// <summary>
        /// Registry values are exposed as read-only (P2-6)
        /// </summary>
        [TestMethod]
        public void P0_9_RegistryIsReadOnly()
        {
            Assert.IsInstanceOfType(SupportedTypes.Values, typeof(IReadOnlyDictionary<string, IColumnConverter>));
            Assert.AreSame(SupportedTypes.Values["bool"], SupportedTypes.Values["boolean"]);
        }

        class TestConverter : IColumnConverter
        {
            public string PythonTypeName => "test_dtype";

            public Type TargetType => typeof(string);

            public object[] ConvertValues(object[] pythonValues)
            {
                return pythonValues;
            }
        }
    }
}
