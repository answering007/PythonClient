using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64.Data;

namespace Pike.PythonClient.Test.Tests.Unit
{
    /// <summary>
    /// Unit tests for <see cref="PythonConnectionStringBuilder"/> (regression tests for P0-1).
    /// The tests do not require Python runtime
    /// </summary>
    [TestClass]
    [TestCategory("Unit")]
    public class ConnectionStringBuilderUnit
    {
        const string ConnectionString =
            @"PYTHONDLL=C:\Py\python311.dll;PYTHONPATH=""C:\Py\Lib;C:\Py\Lib\site-packages"";FILE=C:\Scripts\TestScript.py";

        /// <summary>
        /// Values must be returned byte-for-byte, without upper-casing (P0-1)
        /// </summary>
        [TestMethod]
        public void P0_1_ConnectionString_PreservesValueCase()
        {
            var builder = PythonConnectionStringBuilder.Parse(ConnectionString);

            Assert.AreEqual(@"C:\Py\python311.dll", builder.PythonDll);
            Assert.AreEqual(@"C:\Py\Lib;C:\Py\Lib\site-packages", builder.PythonPath);
            Assert.AreEqual(@"C:\Scripts\TestScript.py", builder.File);
        }

        /// <summary>
        /// Serialization of the parsed builder must survive a round trip without value changes (P0-1)
        /// </summary>
        [TestMethod]
        public void P0_1_ConnectionString_RoundTripKeepsValues()
        {
            var builder = PythonConnectionStringBuilder.Parse(ConnectionString);
            var roundTrip = PythonConnectionStringBuilder.Parse(builder.ConnectionString);

            Assert.AreEqual(builder.PythonDll, roundTrip.PythonDll);
            Assert.AreEqual(builder.PythonPath, roundTrip.PythonPath);
            Assert.AreEqual(builder.File, roundTrip.File);
        }

        /// <summary>
        /// Keyword lookup must be case-insensitive, since the framework passes lower case keys (P0-1)
        /// </summary>
        [TestMethod]
        public void P0_1_ConnectionString_KeywordLookupIsCaseInsensitive()
        {
            var builder = PythonConnectionStringBuilder.Parse(ConnectionString);

            Assert.AreEqual(builder["FILE"], builder["file"]);
            Assert.AreEqual(builder["FILE"], builder["File"]);

            builder["PyThOnPaTh"] = @"C:\Py\Lib";
            Assert.AreEqual(@"C:\Py\Lib", builder.PythonPath);
        }

        /// <summary>
        /// Quoted PYTHONPATH must be split into components (P0-1)
        /// </summary>
        [TestMethod]
        public void P0_1_ConnectionString_PythonPathComponentsAreSplit()
        {
            var builder = PythonConnectionStringBuilder.Parse(ConnectionString);

            CollectionAssert.AreEqual(
                new[] { @"C:\Py\Lib", @"C:\Py\Lib\site-packages" },
                builder.PythonPathComponents);
        }

        /// <summary>
        /// Unsupported keyword must be reported (P0-1)
        /// </summary>
        [TestMethod]
        public void P0_1_ConnectionString_UnsupportedKeywordThrows()
        {
            Assert.ThrowsException<KeyNotFoundException>(() => PythonConnectionStringBuilder.Parse("UNKNOWN=1"));
        }

        /// <summary>
        /// Broken connection string must be reported (P0-1)
        /// </summary>
        [TestMethod]
        public void P0_1_ConnectionString_MissingEqualSignThrows()
        {
            Assert.ThrowsException<ArgumentException>(() => PythonConnectionStringBuilder.Parse("PYTHONDLL"));
        }

        /// <summary>
        /// Unquoted ';' inside the value is an invalid connection string (P0-1, breaking change)
        /// </summary>
        [TestMethod]
        public void P0_1_ConnectionString_ValueWithSemicolonMustBeQuoted()
        {
            Assert.ThrowsException<ArgumentException>(
                () => PythonConnectionStringBuilder.Parse(@"PYTHONPATH=C:\Py\Lib;C:\Py\Lib\site-packages"));
        }

        /// <summary>
        /// Null connection string must be rejected (P0-1)
        /// </summary>
        [TestMethod]
        public void P0_1_ConnectionString_NullThrows()
        {
            Assert.ThrowsException<ArgumentNullException>(() => PythonConnectionStringBuilder.Parse(null));
        }

        /// <summary>
        /// Empty connection string means "no settings" (P0-1)
        /// </summary>
        [TestMethod]
        public void P0_1_ConnectionString_EmptyStringProducesEmptyBuilder()
        {
            Assert.AreEqual(0, PythonConnectionStringBuilder.Parse(string.Empty).Count);
            Assert.AreEqual(0, PythonConnectionStringBuilder.Parse("   ").Count);
        }

        /// <summary>
        /// Unicode values must not be changed (P0-1)
        /// </summary>
        [TestMethod]
        public void P0_1_ConnectionString_UnicodeValueIsPreserved()
        {
            const string path = "C:\\\u0421\u043A\u0440\u0438\u043F\u0442\u044B\\\u0442\u0435\u0441\u0442.py";
            var builder = PythonConnectionStringBuilder.Parse($"FILE={path}");

            Assert.AreEqual(path, builder.File);
        }

        /// <summary>
        /// Trailing ';' and spaces around '=' are allowed (P0-1)
        /// </summary>
        [TestMethod]
        public void P0_1_ConnectionString_TrailingSemicolonAndSpacesAreTolerated()
        {
            var builder = PythonConnectionStringBuilder.Parse(@"PYTHONDLL = C:\Py\python311.dll;");

            Assert.AreEqual(@"C:\Py\python311.dll", builder.PythonDll);
        }

        /// <summary>
        /// Empty value is treated as a missing key by the base parser (P0-1)
        /// </summary>
        [TestMethod]
        public void P0_1_ConnectionString_EmptyValueIsTreatedAsMissing()
        {
            var builder = PythonConnectionStringBuilder.Parse("PYTHONPATH=");

            Assert.AreEqual(0, builder.Count);
            Assert.IsFalse(builder.ContainsKey("PYTHONPATH"));
            Assert.IsNull(builder.PythonPath);
            Assert.AreEqual(0, builder.PythonPathComponents.Length);
        }

        /// <summary>
        /// Only supported keywords are exposed by <see cref="PythonConnectionStringBuilder.Keys"/> (P0-1)
        /// </summary>
        [TestMethod]
        public void P0_1_Keys_ContainsSupportedKeywordsOnly()
        {
            var keys = new PythonConnectionStringBuilder().Keys.Cast<string>().ToArray();

            CollectionAssert.AreEquivalent(new[] { "PYTHONDLL", "PYTHONPATH", "FILE" }, keys);
        }

        /// <summary>
        /// Validation requires PYTHONDLL (P0-1)
        /// </summary>
        [TestMethod]
        public void P0_1_Validate_RequiresPythonDll()
        {
            Assert.ThrowsException<InvalidOperationException>(() => new PythonConnectionStringBuilder().Validate());
            Assert.ThrowsException<InvalidOperationException>(
                () => new PythonConnectionStringBuilder { PythonPath = @"C:\Py" }.Validate());

            new PythonConnectionStringBuilder { PythonDll = @"C:\Py\python311.dll" }.Validate();
        }
    }
}
