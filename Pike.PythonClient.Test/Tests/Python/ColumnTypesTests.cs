using System;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64;

namespace Pike.PythonClient.Test.Tests.Python
{
    /// <summary>
    /// DataFrame column types tests (regression tests for P0-8/P0-9).
    /// Require the python runtime (PYTHONNET_TEST_DLL)
    /// </summary>
    [TestClass]
    [TestCategory("Python")]
    public class ColumnTypesTests
    {
        /// <summary>
        /// Opens the provider once per process (the engine is a process-wide singleton)
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestEnvironment.SkipIfNoPython();
            TestEnvironment.EnsureProviderOpen();
        }

        /// <summary>
        /// Restores the clean engine state
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            PythonDataQuery.StrictColumnTypes = true;
            TestEnvironment.ShutdownIfOwned();
        }

        /// <summary>
        /// numpy and nullable extension dtypes are converted to the expected managed types (P0-9)
        /// </summary>
        [TestMethod]
        public void P0_9_ColumnTypesAreConverted()
        {
            const string code = @"import numpy as np
import pandas as pd

result = pd.DataFrame({
    'Int8': np.array([1, 2], dtype='int8'),
    'UInt32': np.array([1, 2], dtype='uint32'),
    'Float32': np.array([1.5, np.nan], dtype='float32'),
    'Int64Nullable': pd.array([1, pd.NA], dtype='Int64'),
    'Boolean': pd.array([True, pd.NA], dtype='boolean'),
    'Text': pd.array(['a', None], dtype='string'),
    'TimeDelta': pd.to_timedelta(['1h', None])
})";

            var table = PythonDataQuery.RunScript(code);

            Assert.AreEqual(typeof(int), table.Columns["Int8"].DataType);
            Assert.AreEqual(typeof(int), table.Columns["UInt32"].DataType);
            Assert.AreEqual(typeof(double), table.Columns["Float32"].DataType);
            Assert.AreEqual(typeof(long), table.Columns["Int64Nullable"].DataType);
            Assert.AreEqual(typeof(bool), table.Columns["Boolean"].DataType);
            Assert.AreEqual(typeof(string), table.Columns["Text"].DataType);
            Assert.AreEqual(typeof(TimeSpan), table.Columns["TimeDelta"].DataType);

            Assert.AreEqual(1, table.Rows[0]["Int8"]);
            Assert.AreEqual(1, table.Rows[0]["UInt32"]);
            Assert.AreEqual(1.5d, table.Rows[0]["Float32"]);
            Assert.AreEqual(1L, table.Rows[0]["Int64Nullable"]);
            Assert.AreEqual(true, table.Rows[0]["Boolean"]);
            Assert.AreEqual("a", table.Rows[0]["Text"]);
            Assert.AreEqual(TimeSpan.FromHours(1), table.Rows[0]["TimeDelta"]);

            Assert.AreEqual(DBNull.Value, table.Rows[1]["Float32"]);
            Assert.AreEqual(DBNull.Value, table.Rows[1]["Int64Nullable"]);
            Assert.AreEqual(DBNull.Value, table.Rows[1]["Boolean"]);
            Assert.AreEqual(DBNull.Value, table.Rows[1]["Text"]);
            Assert.AreEqual(DBNull.Value, table.Rows[1]["TimeDelta"]);
        }

        /// <summary>
        /// NaT is converted to DBNull and dates before 1970 do not throw (P0-8)
        /// </summary>
        [TestMethod]
        public void P0_8_NaTAndDatesBefore1970()
        {
            const string code = @"import pandas as pd

result = pd.DataFrame({
    'DateTime': pd.to_datetime(['1960-01-01', None]),
    'DateTimeUtc': pd.to_datetime(['2020-01-01', None], utc=True)
})";

            var table = PythonDataQuery.RunScript(code);

            Assert.AreEqual(typeof(DateTime), table.Columns["DateTime"].DataType);
            Assert.AreEqual(new DateTime(1960, 1, 1), table.Rows[0]["DateTime"]);
            Assert.AreEqual(DBNull.Value, table.Rows[1]["DateTime"]);

            Assert.AreEqual(typeof(DateTime), table.Columns["DateTimeUtc"].DataType);
            Assert.AreEqual(DBNull.Value, table.Rows[1]["DateTimeUtc"]);
        }

        /// <summary>
        /// Missing values of the inferred string dtype (pandas 3 keeps them as NaN) become DBNull (P0-9)
        /// </summary>
        [TestMethod]
        public void P0_9_MissingStringValuesBecomeDbNull()
        {
            const string code = @"import pandas as pd
result = pd.DataFrame({'Text': ['Pike', None, 'Amol']})";

            var table = PythonDataQuery.RunScript(code);

            Assert.AreEqual(typeof(string), table.Columns["Text"].DataType);
            Assert.AreEqual("Pike", table.Rows[0]["Text"]);
            Assert.AreEqual(DBNull.Value, table.Rows[1]["Text"]);
            Assert.AreEqual("Amol", table.Rows[2]["Text"]);
        }

        /// <summary>
        /// Invalid DataColumn characters are replaced and colliding names are uniquified (P0-9)
        /// </summary>
        [TestMethod]
        public void P0_9_InvalidColumnNamesAreNormalized()
        {
            const string code = @"import pandas as pd
result = pd.DataFrame([[1, 2, 3]], columns=['a.b', 'a_b', 'a[b]'])";

            var table = PythonDataQuery.RunScript(code);
            var names = table.Columns.Cast<DataColumn>().Select(column => column.ColumnName).ToArray();

            CollectionAssert.AreEqual(new[] { "a_b", "a_b_2", "a(b)" }, names);
        }

        /// <summary>
        /// Duplicated column names are rejected with a clear error (P0-9)
        /// </summary>
        [TestMethod]
        public void P0_9_DuplicatedColumnNamesThrow()
        {
            const string code = @"import pandas as pd
result = pd.DataFrame([[1, 2]], columns=['a', 'a'])";

            var exception = Assert.ThrowsException<NotSupportedException>(() => PythonDataQuery.RunScript(code));
            StringAssert.Contains(exception.Message, "Duplicated column names");
        }

        /// <summary>
        /// Unknown dtype is rejected instead of the silent string conversion (P0-9)
        /// </summary>
        [TestMethod]
        public void P0_9_UnsupportedDtypeThrows()
        {
            const string code = @"import numpy as np
import pandas as pd

result = pd.DataFrame({'Complex': np.array([1 + 2j, 3 + 4j], dtype='complex128')})";

            var exception = Assert.ThrowsException<NotSupportedException>(() => PythonDataQuery.RunScript(code));
            StringAssert.Contains(exception.Message, "unsupported dtype");
        }

        /// <summary>
        /// Strict mode can be disabled: unknown dtype is converted to string (P0-9)
        /// </summary>
        [TestMethod]
        public void P0_9_StrictModeCanBeDisabled()
        {
            const string code = @"import numpy as np
import pandas as pd

result = pd.DataFrame({'Complex': np.array([1 + 2j], dtype='complex128')})";

            PythonDataQuery.StrictColumnTypes = false;

            var table = PythonDataQuery.RunScript(code);

            Assert.AreEqual(typeof(string), table.Columns["Complex"].DataType);
            Assert.AreEqual("(1+2j)", table.Rows[0]["Complex"]);
        }
    }
}
