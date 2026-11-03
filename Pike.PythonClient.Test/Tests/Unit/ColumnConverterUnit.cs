using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64.ColumnConverters;

namespace Pike.PythonClient.Test.Tests.Unit
{
    /// <summary>
    /// Unit tests for column converters (regression tests for P0-8, P0-9).
    /// Values are prepared as managed objects, so the tests do not require Python runtime
    /// </summary>
    [TestClass]
    [TestCategory("Unit")]
    public class ColumnConverterUnit
    {
        const long NanosecondsPerHour = 3600000000000;

        /// <summary>
        /// numpy datetime64 nanoseconds are converted to DateTime (P0-8)
        /// </summary>
        [TestMethod]
        public void P0_8_DateTime_ConvertsNanoSeconds()
        {
            var values = new DateTimeConverter().ConvertValues(new object[]
            {
                0L,
                1600000000000000000L
            });

            Assert.AreEqual(new DateTime(1970, 1, 1), values[0]);
            Assert.AreEqual(new DateTime(2020, 9, 13, 12, 26, 40), values[1]);
        }

        /// <summary>
        /// Dates before 1970 must not throw (P0-8)
        /// </summary>
        [TestMethod]
        public void P0_8_DateTime_HandlesDatesBefore1970()
        {
            var values = new DateTimeConverter().ConvertValues(new object[] { -315619200000000000L });

            Assert.AreEqual(new DateTime(1960, 1, 1), values[0]);
        }

        /// <summary>
        /// NaT, None and DBNull become DBNull, existed DateTime goes through (P0-8)
        /// </summary>
        [TestMethod]
        public void P0_8_DateTime_MissingValuesBecomeDbNull()
        {
            var date = new DateTime(2021, 1, 2);
            var values = new DateTimeConverter().ConvertValues(new object[]
            {
                long.MinValue,
                null,
                DBNull.Value,
                date
            });

            Assert.AreEqual(DBNull.Value, values[0]);
            Assert.AreEqual(DBNull.Value, values[1]);
            Assert.AreEqual(DBNull.Value, values[2]);
            Assert.AreEqual(date, values[3]);
        }

        /// <summary>
        /// numpy timedelta64 nanoseconds are converted to TimeSpan (P0-8)
        /// </summary>
        [TestMethod]
        public void P0_8_TimeSpan_ConvertsNanoSeconds()
        {
            var values = new TimeSpanConverter().ConvertValues(new object[]
            {
                NanosecondsPerHour,
                -NanosecondsPerHour
            });

            Assert.AreEqual(TimeSpan.FromHours(1), values[0]);
            Assert.AreEqual(TimeSpan.FromHours(-1), values[1]);
        }

        /// <summary>
        /// NaT, None and DBNull become DBNull, existed TimeSpan goes through (P0-8)
        /// </summary>
        [TestMethod]
        public void P0_8_TimeSpan_MissingValuesBecomeDbNull()
        {
            var span = TimeSpan.FromMinutes(5);
            var values = new TimeSpanConverter().ConvertValues(new object[]
            {
                long.MinValue,
                null,
                span
            });

            Assert.AreEqual(DBNull.Value, values[0]);
            Assert.AreEqual(DBNull.Value, values[1]);
            Assert.AreEqual(span, values[2]);
        }

        /// <summary>
        /// Integer converter tolerates missing values and out of range values (P0-9)
        /// </summary>
        [TestMethod]
        public void P0_9_Integer_ConvertsValues()
        {
            var values = new IntegerConverter().ConvertValues(new object[]
            {
                1,
                2L,
                null,
                (long)int.MaxValue + 1
            });

            Assert.AreEqual(1, values[0]);
            Assert.AreEqual(2, values[1]);
            Assert.AreEqual(DBNull.Value, values[2]);
            Assert.AreEqual(DBNull.Value, values[3]);
        }

        /// <summary>
        /// Long converter tolerates missing values (P0-9)
        /// </summary>
        [TestMethod]
        public void P0_9_Long_ConvertsValues()
        {
            var values = new LongConverter().ConvertValues(new object[]
            {
                1L,
                (short)2,
                null,
                "text"
            });

            Assert.AreEqual(1L, values[0]);
            Assert.AreEqual(2L, values[1]);
            Assert.AreEqual(DBNull.Value, values[2]);
            Assert.AreEqual(DBNull.Value, values[3]);
        }

        /// <summary>
        /// Double converter maps NaN to DBNull (P0-9)
        /// </summary>
        [TestMethod]
        public void P0_9_Double_ConvertsValues()
        {
            var values = new DoubleConverter().ConvertValues(new object[]
            {
                1.5d,
                double.NaN,
                null,
                2
            });

            Assert.AreEqual(1.5d, values[0]);
            Assert.AreEqual(DBNull.Value, values[1]);
            Assert.AreEqual(DBNull.Value, values[2]);
            Assert.AreEqual(2d, values[3]);
        }

        /// <summary>
        /// Boolean converter tolerates missing values (P0-9)
        /// </summary>
        [TestMethod]
        public void P0_9_Boolean_ConvertsValues()
        {
            var values = new BooleanConverter().ConvertValues(new object[]
            {
                true,
                false,
                null,
                "text"
            });

            Assert.AreEqual(true, values[0]);
            Assert.AreEqual(false, values[1]);
            Assert.AreEqual(DBNull.Value, values[2]);
            Assert.AreEqual(DBNull.Value, values[3]);
        }

        /// <summary>
        /// String converter maps None to DBNull and converts other values to strings (P0-9)
        /// </summary>
        [TestMethod]
        public void P0_9_String_ConvertsValues()
        {
            var values = new StringConverter().ConvertValues(new object[]
            {
                "Pike",
                null,
                12
            });

            Assert.AreEqual("Pike", values[0]);
            Assert.AreEqual(DBNull.Value, values[1]);
            Assert.AreEqual("12", values[2]);
        }

        /// <summary>
        /// Target types of the converters are stable (P0-9)
        /// </summary>
        [TestMethod]
        public void P0_9_Converters_HaveExpectedTargetTypes()
        {
            Assert.AreEqual(typeof(bool), new BooleanConverter().TargetType);
            Assert.AreEqual(typeof(DateTime), new DateTimeConverter().TargetType);
            Assert.AreEqual(typeof(double), new DoubleConverter().TargetType);
            Assert.AreEqual(typeof(int), new IntegerConverter().TargetType);
            Assert.AreEqual(typeof(long), new LongConverter().TargetType);
            Assert.AreEqual(typeof(string), new StringConverter().TargetType);
            Assert.AreEqual(typeof(TimeSpan), new TimeSpanConverter().TargetType);
        }
    }
}
