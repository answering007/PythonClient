using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64;

namespace Pike.PythonClient.Test.Tests.Unit
{
    /// <summary>
    /// Unit tests for <see cref="KeyIndexCollection{TKey,TValue}"/> (regression tests for P0-4, P1-2).
    /// The tests do not require Python runtime
    /// </summary>
    [TestClass]
    [TestCategory("Unit")]
    public class KeyIndexCollectionUnit
    {
        static KeyIndexCollection<string, string> CreateCollection()
        {
            var collection = new KeyIndexCollection<string, string>();
            collection.Add("a", "1");
            collection.Add("b", "2");
            collection.Add("c", "3");
            return collection;
        }

        /// <summary>
        /// Set must replace the value and keep the position of the existed key (P0-4)
        /// </summary>
        [TestMethod]
        public void P0_4_Set_ReplacesValueKeepingPosition()
        {
            var collection = CreateCollection();

            collection.Set("b", "22");

            Assert.AreEqual(3, collection.Count);
            Assert.AreEqual("22", collection.GetByKey("b"));
            Assert.AreEqual(1, collection.IndexOfKey("b"));
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, collection.Keys.ToArray());
        }

        /// <summary>
        /// Set must append a new key to the end of the collection (P0-4)
        /// </summary>
        [TestMethod]
        public void P0_4_Set_AddsNewKeyAtTheEnd()
        {
            var collection = CreateCollection();

            collection.Set("d", "4");

            Assert.AreEqual(4, collection.Count);
            CollectionAssert.AreEqual(new[] { "a", "b", "c", "d" }, collection.Keys.ToArray());
        }

        /// <summary>
        /// Insert must place a new key at the requested index (P0-4)
        /// </summary>
        [TestMethod]
        public void P0_4_Insert_PlacesNewKeyAtIndex()
        {
            var collection = CreateCollection();

            collection.Insert(1, "z", "0");

            CollectionAssert.AreEqual(new[] { "a", "z", "b", "c" }, collection.Keys.ToArray());
            Assert.AreEqual(1, collection.IndexOfKey("z"));
        }

        /// <summary>
        /// Insert must move an existed key to the requested index (P0-4)
        /// </summary>
        [TestMethod]
        public void P0_4_Insert_MovesExistedKeyToIndex()
        {
            var collection = CreateCollection();

            collection.Insert(0, "c", "33");

            CollectionAssert.AreEqual(new[] { "c", "a", "b" }, collection.Keys.ToArray());
            Assert.AreEqual("33", collection.GetByKey("c"));
            Assert.AreEqual(3, collection.Count);
        }

        /// <summary>
        /// Insert must validate the index (P0-4)
        /// </summary>
        [TestMethod]
        public void P0_4_Insert_OutOfRangeThrows()
        {
            var collection = CreateCollection();

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => collection.Insert(-1, "z", "0"));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => collection.Insert(4, "z", "0"));
        }

        /// <summary>
        /// Values and enumeration must follow the insertion order (P0-4)
        /// </summary>
        [TestMethod]
        public void P0_4_ValuesAndEnumeration_AreOrdered()
        {
            var collection = CreateCollection();
            collection.Set("b", "22");
            collection.Insert(0, "z", "0");

            CollectionAssert.AreEqual(new[] { "0", "1", "22", "3" }, collection.Values.ToArray());
            CollectionAssert.AreEqual(
                new[] { "z=0", "a=1", "b=22", "c=3" },
                collection.Select(pair => $"{pair.Key}={pair.Value}").ToArray());
        }

        /// <summary>
        /// TryGetValue must report missing keys without exceptions (P0-4)
        /// </summary>
        [TestMethod]
        public void P0_4_TryGetValue_ReportsMissingKey()
        {
            var collection = CreateCollection();

            Assert.IsTrue(collection.TryGetValue("a", out var value));
            Assert.AreEqual("1", value);

            Assert.IsFalse(collection.TryGetValue("missing", out var missing));
            Assert.IsNull(missing);
        }

        /// <summary>
        /// Removing a missing key must be a no-op (P0-4)
        /// </summary>
        [TestMethod]
        public void P0_4_Remove_MissingKeyIsIgnored()
        {
            var collection = CreateCollection();

            collection.Remove("missing");

            Assert.AreEqual(3, collection.Count);
        }

        /// <summary>
        /// Duplicated keys must be reported by Add (P0-4)
        /// </summary>
        [TestMethod]
        public void P0_4_Add_DuplicateKeyThrows()
        {
            var collection = CreateCollection();

            Assert.ThrowsException<ArgumentException>(() => collection.Add("a", "1"));
        }

        /// <summary>
        /// Renaming a key must keep its position and value (P1-2)
        /// </summary>
        [TestMethod]
        public void P1_2_RenameKey_KeepsPositionAndValue()
        {
            var collection = CreateCollection();

            collection.RenameKey("b", "beta");

            CollectionAssert.AreEqual(new[] { "a", "beta", "c" }, collection.Keys.ToArray());
            Assert.AreEqual("2", collection.GetByKey("beta"));
            Assert.AreEqual(1, collection.IndexOfKey("beta"));
            Assert.IsFalse(collection.ContainsKey("b"));
        }

        /// <summary>
        /// Renaming to an existed key must be reported (P1-2)
        /// </summary>
        [TestMethod]
        public void P1_2_RenameKey_DuplicateThrows()
        {
            var collection = CreateCollection();

            Assert.ThrowsException<ArgumentException>(() => collection.RenameKey("b", "a"));
            Assert.AreEqual(3, collection.Count);
        }

        /// <summary>
        /// Renaming a missing key must be reported (P1-2)
        /// </summary>
        [TestMethod]
        public void P1_2_RenameKey_MissingKeyThrows()
        {
            var collection = CreateCollection();

            Assert.ThrowsException<KeyNotFoundException>(() => collection.RenameKey("missing", "other"));
        }
    }
}
