using System;
using System.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64.Data;

namespace Pike.PythonClient.Test.Tests.Unit
{
    /// <summary>
    /// Unit tests for <see cref="PythonParameter"/> (regression tests for P0-3, P1-2).
    /// The tests do not require Python runtime
    /// </summary>
    [TestClass]
    [TestCategory("Unit")]
    public class ParameterUnit
    {
        /// <summary>
        /// Null value must be allowed (P0-3)
        /// </summary>
        [TestMethod]
        public void P0_3_NullValueIsAllowed()
        {
            var parameter = new PythonParameter { ParameterName = "x", Value = null };

            Assert.IsNull(parameter.Value);
            Assert.AreEqual(DbType.String, parameter.DbType);
            Assert.AreEqual(0, parameter.Size);
        }

        /// <summary>
        /// DBNull value must be allowed (P0-3)
        /// </summary>
        [TestMethod]
        public void P0_3_DbNullValueIsAllowed()
        {
            var parameter = new PythonParameter { ParameterName = "x", Value = DBNull.Value };

            Assert.AreEqual(DBNull.Value, parameter.Value);
            Assert.AreEqual(DbType.String, parameter.DbType);
            Assert.AreEqual(0, parameter.Size);
        }

        /// <summary>
        /// String size is the value length (P0-3, GetSize must not cast to string blindly)
        /// </summary>
        [TestMethod]
        public void P0_3_StringValueSetsSizeFromLength()
        {
            var parameter = new PythonParameter { Value = "Pike" };

            Assert.AreEqual(DbType.String, parameter.DbType);
            Assert.AreEqual(4, parameter.Size);
        }

        /// <summary>
        /// DbType is inferred from the value type (P0-3, P1-2)
        /// </summary>
        [TestMethod]
        public void P0_3_DbTypeIsInferredFromValue()
        {
            Assert.AreEqual(DbType.Boolean, new PythonParameter { Value = true }.DbType);
            Assert.AreEqual(DbType.Byte, new PythonParameter { Value = (byte)1 }.DbType);
            Assert.AreEqual(DbType.Int16, new PythonParameter { Value = (short)1 }.DbType);
            Assert.AreEqual(DbType.Int32, new PythonParameter { Value = 1 }.DbType);
            Assert.AreEqual(DbType.Int64, new PythonParameter { Value = 1L }.DbType);
            Assert.AreEqual(DbType.Single, new PythonParameter { Value = 1.5f }.DbType);
            Assert.AreEqual(DbType.Double, new PythonParameter { Value = 1.5d }.DbType);
            Assert.AreEqual(DbType.Decimal, new PythonParameter { Value = 1.5m }.DbType);
            Assert.AreEqual(DbType.String, new PythonParameter { Value = 'a' }.DbType);
            Assert.AreEqual(DbType.DateTime, new PythonParameter { Value = DateTime.Today }.DbType);
            Assert.AreEqual(DbType.Guid, new PythonParameter { Value = Guid.NewGuid() }.DbType);
            Assert.AreEqual(DbType.Time, new PythonParameter { Value = TimeSpan.FromSeconds(1) }.DbType);
            Assert.AreEqual(DbType.DateTimeOffset, new PythonParameter { Value = DateTimeOffset.Now }.DbType);
        }

        /// <summary>
        /// Byte array value is a binary parameter (P1-2)
        /// </summary>
        [TestMethod]
        public void P1_2_ByteArraySetsBinaryType()
        {
            var parameter = new PythonParameter { Value = new byte[] { 1, 2, 3 } };

            Assert.AreEqual(DbType.Binary, parameter.DbType);
            Assert.AreEqual(3, parameter.Size);
        }

        /// <summary>
        /// Explicit DbType must survive a Value assignment (P1-2)
        /// </summary>
        [TestMethod]
        public void P1_2_DbTypeIsSettableAndActsAsHint()
        {
            var parameter = new PythonParameter { DbType = DbType.Decimal };

            parameter.Value = 12.5m;

            Assert.AreEqual(DbType.Decimal, parameter.DbType);
            Assert.AreEqual(16, parameter.Size);
        }

        /// <summary>
        /// ResetDbType must restore the automatic DbType detection (P1-2)
        /// </summary>
        [TestMethod]
        public void P1_2_ResetDbTypeRestoresInference()
        {
            var parameter = new PythonParameter { DbType = DbType.String, Value = 12.5m };
            Assert.AreEqual(DbType.String, parameter.DbType);

            parameter.ResetDbType();
            parameter.Value = 12.5m;

            Assert.AreEqual(DbType.Decimal, parameter.DbType);
        }

        /// <summary>
        /// Only ParameterDirection.Input is supported (P1-2)
        /// </summary>
        [TestMethod]
        public void P1_2_DirectionOnlyInputIsSupported()
        {
            var parameter = new PythonParameter { Direction = ParameterDirection.Input };
            Assert.AreEqual(ParameterDirection.Input, parameter.Direction);

            Assert.ThrowsException<NotSupportedException>(
                () => new PythonParameter { Direction = ParameterDirection.Output });
            Assert.ThrowsException<NotSupportedException>(
                () => new PythonParameter { Direction = ParameterDirection.InputOutput });
            Assert.ThrowsException<NotSupportedException>(
                () => new PythonParameter { Direction = ParameterDirection.ReturnValue });
        }

        /// <summary>
        /// Parameter accepts null values by default (P1-2)
        /// </summary>
        [TestMethod]
        public void P1_2_IsNullableDefaultsToTrue()
        {
            Assert.IsTrue(new PythonParameter().IsNullable);
        }

        /// <summary>
        /// Parameter name can be changed after it was set (P1-2)
        /// </summary>
        [TestMethod]
        public void P1_2_ParameterNameIsRenameable()
        {
            var parameter = new PythonParameter { ParameterName = "first" };
            parameter.ParameterName = "second";

            Assert.AreEqual("second", parameter.ParameterName);
        }
    }
}
