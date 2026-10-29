using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64.Data;

namespace Pike.PythonClient.Test.Tests.Unit
{
    /// <summary>
    /// Unit tests for <see cref="PythonParameterCollection"/> (regression tests for P0-2, P0-4, P1-2).
    /// The tests do not require Python runtime
    /// </summary>
    [TestClass]
    [TestCategory("Unit")]
    public class ParameterCollectionUnit
    {
        static PythonParameter CreateParameter(string name, object value)
        {
            return new PythonParameter { ParameterName = name, Value = value };
        }

        static PythonParameterCollection CreateCollection()
        {
            var collection = new PythonParameterCollection();
            collection.Add(CreateParameter("a", 1));
            collection.Add(CreateParameter("b", 2));
            collection.Add(CreateParameter("c", 3));
            return collection;
        }

        /// <summary>
        /// Parameters[name] = value must replace an existed parameter (P0-4)
        /// </summary>
        [TestMethod]
        public void P0_4_SetParameter_ReplacesExistedParameter()
        {
            var collection = CreateCollection();

            collection["b"] = CreateParameter("b", 22);

            Assert.AreEqual(3, collection.Count);
            Assert.AreEqual(22, ((PythonParameter)collection["b"]).Value);
            Assert.AreEqual(1, collection.IndexOf("b"));
        }

        /// <summary>
        /// Add must reject duplicated names (P0-4)
        /// </summary>
        [TestMethod]
        public void P0_4_Add_DuplicateNameThrows()
        {
            var collection = CreateCollection();

            Assert.ThrowsException<ArgumentException>(() => collection.Add(CreateParameter("a", 1)));
        }

        /// <summary>
        /// Insert must place a parameter at the requested index (P0-4)
        /// </summary>
        [TestMethod]
        public void P0_4_Insert_PlacesParameterAtIndex()
        {
            var collection = CreateCollection();

            collection.Insert(1, CreateParameter("z", 0));

            Assert.AreEqual(4, collection.Count);
            Assert.AreEqual(1, collection.IndexOf("z"));
            Assert.AreEqual(2, collection.IndexOf("b"));
        }

        /// <summary>
        /// Insert must validate the index (P0-4)
        /// </summary>
        [TestMethod]
        public void P0_4_Insert_OutOfRangeThrows()
        {
            var collection = CreateCollection();

            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => collection.Insert(-1, CreateParameter("z", 0)));
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => collection.Insert(4, CreateParameter("z", 0)));
        }

        /// <summary>
        /// CopyTo must copy all parameters starting at the requested index (P0-2)
        /// </summary>
        [TestMethod]
        public void P0_2_CopyTo_CopiesAllItemsFromIndex()
        {
            var collection = CreateCollection();
            var target = new object[5];

            collection.CopyTo(target, 1);

            Assert.IsNull(target[0]);
            Assert.IsNull(target[4]);
            CollectionAssert.AreEqual(
                new[] { "a", "b", "c" },
                target.Skip(1).Take(3).Cast<PythonParameter>().Select(p => p.ParameterName).ToArray());
        }

        /// <summary>
        /// CopyTo must reject a target array that is too small (P0-2)
        /// </summary>
        [TestMethod]
        public void P0_2_CopyTo_TooSmallTargetThrows()
        {
            var collection = CreateCollection();

            Assert.ThrowsException<ArgumentException>(() => collection.CopyTo(new object[2], 1));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => collection.CopyTo(new object[5], -1));
            Assert.ThrowsException<ArgumentNullException>(() => collection.CopyTo(null, 0));
        }

        /// <summary>
        /// Contains(object) and IndexOf(object) must not throw for non-parameter values (P0-4)
        /// </summary>
        [TestMethod]
        public void P0_4_ObjectOverloadsAreSafe()
        {
            var collection = CreateCollection();

            Assert.IsFalse(collection.Contains((object)"not a parameter"));
            Assert.IsFalse(collection.Contains((object)null));
            Assert.AreEqual(-1, collection.IndexOf((object)"not a parameter"));
            Assert.AreEqual(-1, collection.IndexOf((object)null));
            Assert.IsTrue(collection.Contains((object)CreateParameter("a", 1)));
            Assert.AreEqual(0, collection.IndexOf((object)CreateParameter("a", 1)));
        }

        /// <summary>
        /// Removing a missing parameter must be a no-op (P0-4)
        /// </summary>
        [TestMethod]
        public void P0_4_Remove_MissingParameterIsIgnored()
        {
            var collection = CreateCollection();

            collection.Remove((object)CreateParameter("missing", 1));
            collection.Remove((object)"not a parameter");
            collection.Remove(null);

            Assert.AreEqual(3, collection.Count);
        }

        /// <summary>
        /// Enumerator must produce parameters, not key/value pairs (P0-4)
        /// </summary>
        [TestMethod]
        public void P0_4_Enumerator_ReturnsParameters()
        {
            var collection = CreateCollection();

            var items = collection.Cast<object>().ToArray();

            Assert.AreEqual(3, items.Length);
            Assert.IsTrue(items.All(item => item is PythonParameter));
            CollectionAssert.AreEqual(
                new[] { "a", "b", "c" },
                items.Cast<PythonParameter>().Select(p => p.ParameterName).ToArray());
        }

        /// <summary>
        /// Collection is not synchronized (P0-4)
        /// </summary>
        [TestMethod]
        public void P0_4_IsSynchronized_IsFalse()
        {
            Assert.IsFalse(new PythonParameterCollection().IsSynchronized);
        }

        /// <summary>
        /// Renaming a parameter after adding must be visible through the collection (P1-2)
        /// </summary>
        [TestMethod]
        public void P1_2_Rename_IsReflectedInCollection()
        {
            var collection = new PythonParameterCollection();
            var parameter = CreateParameter("old", 1);
            collection.Add(parameter);

            parameter.ParameterName = "new";

            Assert.IsTrue(collection.Contains("new"));
            Assert.IsFalse(collection.Contains("old"));
            Assert.AreEqual(0, collection.IndexOf("new"));
            Assert.AreEqual(parameter, collection["new"]);

            collection["new"] = CreateParameter("new", 2);
            Assert.AreEqual(1, collection.Count);
        }

        /// <summary>
        /// Renaming to a name used by another parameter must not corrupt the collection (P1-2)
        /// </summary>
        [TestMethod]
        public void P1_2_Rename_ToExistedNameKeepsCollectionIntact()
        {
            var collection = CreateCollection();
            var parameter = collection[0];

            parameter.ParameterName = "b";

            Assert.AreEqual(3, collection.Count);
            Assert.IsTrue(collection.Contains("b"));
        }
    }
}
