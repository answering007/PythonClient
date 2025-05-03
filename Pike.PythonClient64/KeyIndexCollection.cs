using System;
using System.Collections.Generic;
using System.Collections;

namespace Pike.PythonClient64
{
    /// <summary>
    /// Represents a collection of keys and values. List of keys that can be accessed by index
    /// </summary>
    /// <typeparam name="TKey">The type of the keys in the collection</typeparam>
    /// <typeparam name="TValue">The type of the values in the collection.</typeparam>
    public class KeyIndexCollection<TKey, TValue>: IEnumerable<KeyValuePair<TKey, TValue>>
    {
        readonly Dictionary<TKey, TValue> _dictionary = new Dictionary<TKey, TValue>();
        readonly List<TKey> _keys = new List<TKey>();

        /// <summary>
        /// Adds an element with the specified key and value to the collection.
        /// </summary>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="value">The value of the element to add.</param>
        /// <exception cref="ArgumentException">An element with the same key already exists in the collection.</exception>
        public void Add(TKey key, TValue value)
        {
            if (_dictionary.ContainsKey(key)) throw new ArgumentException("An element with the same key already exists.");

            _dictionary[key] = value;
            _keys.Add(key);
        }

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <returns>The value associated with the specified key.</returns>
        /// <exception cref="KeyNotFoundException">The specified key is not found in the collection.</exception>
        public TValue GetByKey(TKey key)
        {
            return _dictionary[key];
        }

        /// <summary>
        /// Gets the value associated with the specified index.
        /// </summary>
        /// <param name="index">The index of the value to get.</param>
        /// <returns>The value associated with the specified index.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Index is out of range.</exception>
        public TValue GetByIndex(int index)
        {
            if (index < 0 || index >= _keys.Count) throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");

            var key = _keys[index];
            return _dictionary[key];
        }

        /// <summary>
        /// Number of elements;
        /// </summary>
        public int Count => _keys.Count;

        /// <summary>
        /// Gets the key associated with the specified index.
        /// </summary>
        /// <param name="index">The index of the key to get.</param>
        /// <returns>The key associated with the specified index.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Index is out of range.</exception>
        public TKey GetKeyByIndex(int index)
        {
            if (index < 0 || index >= _keys.Count) throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");

            return _keys[index];
        }

        /// <summary>
        /// Determines whether the collection contains an element with the specified key.
        /// </summary>
        /// <param name="key">The key to locate in the collection.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the key is null.</exception>
        public bool ContainsKey(TKey key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            return _dictionary.ContainsKey(key);
        }

        /// <summary>
        /// Determines the index of a specific key in the collection.
        /// </summary>
        /// <param name="key">The key to locate in the collection.</param>
        /// <returns>The index of <paramref name="key"/> if found in the list; otherwise, -1.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the key is null.</exception>
        public int IndexOfKey(TKey key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            return _keys.IndexOf(key);
        }

        /// <summary>
        /// Collection of values
        /// </summary>
        public IEnumerable<TValue> Values => _dictionary.Values;

        /// <summary>
        /// Removes all elements from the collection.
        /// </summary>
        public void Clear()
        {
            _dictionary.Clear();
            _keys.Clear();
        }

        /// <summary>
        /// Removes the element with the specified key from the collection.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <exception cref="ArgumentNullException">key is null.</exception>
        public void Remove(TKey key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            _keys.Remove(key);
            _dictionary.Remove(key);
        }

        /// <summary>
        /// Removes the element at the specified index of the collection.
        /// </summary>
        /// <param name="index">The index of the element to remove.</param>
        /// <exception cref="ArgumentOutOfRangeException">Index is out of range.</exception>
        public void RemoveAt(int index)
        {
            if (index < 0 || index >= _keys.Count) throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");

            var key = _keys[index];
            _dictionary.Remove(key);
            _keys.RemoveAt(index);
        }

        /// <summary>
        /// Gets the value  associated with the specified key.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <returns>The value associated with the specified key.</returns>
        public TValue this[TKey key] => _dictionary[key];

        /// <summary>
        /// Retrieves the value at the specified index in the collection.
        /// </summary>
        /// <param name="index">The zero-based index of the value to retrieve.</param>
        /// <returns>The value at the specified index.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of range.</exception>
        public TValue ElementAt(int index)
        {
            if (index < 0 || index >= _keys.Count) throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");

            var key = _keys[index];
            return _dictionary[key];
        }

        #region Implementation of IEnumerable

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return _dictionary.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }

}
