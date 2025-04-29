using System;
using System.Collections.Generic;
using System.Collections;

namespace Pike.PythonClient64
{
    public class KeyIndexCollection<TKey, TValue>: IEnumerable<KeyValuePair<TKey, TValue>>
    {
        readonly Dictionary<TKey, TValue> _dictionary = new Dictionary<TKey, TValue>();
        readonly List<TKey> _keys = new List<TKey>();

        public void Add(TKey key, TValue value)
        {
            if (_dictionary.ContainsKey(key)) throw new ArgumentException("An element with the same key already exists.");

            _dictionary[key] = value;
            _keys.Add(key);
        }

        public TValue GetByKey(TKey key)
        {
            return _dictionary[key];
        }

        public TValue GetByIndex(int index)
        {
            if (index < 0 || index >= _keys.Count) throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");

            var key = _keys[index];
            return _dictionary[key];
        }

        public int Count => _keys.Count;

        public TKey GetKeyByIndex(int index)
        {
            if (index < 0 || index >= _keys.Count) throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");

            return _keys[index];
        }

        public bool ContainsKey(TKey key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            return _dictionary.ContainsKey(key);
        }

        public int IndexOfKey(TKey key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            return _keys.IndexOf(key);
        }

        public IEnumerable<TValue> Values => _dictionary.Values;

        public void Clear()
        {
            _dictionary.Clear();
            _keys.Clear();
        }

        public void Remove(TKey key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            _keys.Remove(key);
            _dictionary.Remove(key);
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= _keys.Count) throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");

            var key = _keys[index];
            _dictionary.Remove(key);
            _keys.RemoveAt(index);
        }

        public TValue this[TKey key] => _dictionary[key];

        public TValue ElementAt(int index)
        {
            if (index < 0 || index >= _keys.Count) throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");

            var key = _keys[index];
            return _dictionary[key];
        }

        #region Implementation of IEnumerable

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return _dictionary.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }

}
