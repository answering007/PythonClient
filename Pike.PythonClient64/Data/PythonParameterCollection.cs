using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;

namespace Pike.PythonClient64.Data
{
    /// <inheritdoc />
    /// <summary>
    /// An <see cref="T:System.Data.Common.DbParameterCollection" /> implementation
    /// </summary>
    public class PythonParameterCollection : DbParameterCollection
    {
        /// <summary>
        /// Python name for parameters dictionary
        /// </summary>
        public const string PythonName = "params";
        readonly SortedList<string, PythonParameter> _list = new SortedList<string, PythonParameter>();

        /// <inheritdoc />
        /// <summary>
        /// Specifies the number of items in the collection
        /// </summary>
        public override int Count => _list.Count;

        /// <inheritdoc />
        /// <summary>
        /// Specifies whether the collection is a fixed size. Current value is false
        /// </summary>
        public override bool IsFixedSize => false;

        /// <inheritdoc />
        /// <summary>
        /// Specifies whether the collection is read-only. Current value is false
        /// </summary>
        public override bool IsReadOnly => false;

        /// <inheritdoc />
        /// <summary>
        /// Specifies whether the collection is synchronized. Current value is true
        /// </summary>
        public override bool IsSynchronized => true;

        /// <inheritdoc />
        /// <summary>
        /// Specifies the <see cref="T:System.Object" /> to be used to synchronize access to the collection
        /// </summary>
        public override object SyncRoot { get; } = new object();

        /// <inheritdoc />
        /// <summary>
        /// Adds the specified <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> object to the <see cref="T:Pike.PythonClient64.Data.PythonParameterCollection" />
        /// </summary>
        /// <param name="value">The value of the <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> to add to the collection</param>
        /// <returns>The index of the <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> object in the collection</returns>
        public override int Add(object value)
        {
            return Add((PythonParameter)value);
        }

        /// <summary>
        /// Adds the specified <see cref="PythonParameter"/> object to the <see cref="PythonParameterCollection"/>
        /// </summary>
        /// <param name="parameter">The value of the <see cref="PythonParameter"/> to add to the collection</param>
        /// <returns>The index of the <see cref="PythonParameter"/> object in the collection</returns>
        public int Add(PythonParameter parameter)
        {
            if (parameter == null) throw new ArgumentNullException(nameof(parameter));
            if (string.IsNullOrWhiteSpace(parameter.ParameterName))
                throw new ArgumentException("ParameterName property can't be null or empty", nameof(parameter));
            if (_list.ContainsKey(parameter.ParameterName)) throw new ArgumentException("The given key is already exist", nameof(parameter));

            _list.Add(parameter.ParameterName, parameter);
            return _list.Count - 1;
        }

        /// <inheritdoc />
        /// <summary>
        /// Adds an array of items with the specified values to the current parameters collection
        /// </summary>
        /// <param name="values">An array of values of type <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> to add to the collection</param>
        public override void AddRange(Array values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));

            AddRange(values.Cast<PythonParameter>());
        }

        /// <summary>
        /// Adds an array of items with the specified values to the current parameters collection
        /// </summary>
        /// <param name="parameters">A collection of values of type <see cref="PythonParameter"/> to add to the collection</param>
        public void AddRange(IEnumerable<PythonParameter> parameters)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            foreach (var parameter in parameters)
                Add(parameter);
        }

        /// <inheritdoc />
        /// <summary>
        /// Remove all parameters from the current collection
        /// </summary>
        public override void Clear()
        {
            _list.Clear();
        }

        /// <inheritdoc />
        /// <summary>
        /// Indicates whether a <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> with the specified name exists in the collection
        /// </summary>
        /// <param name="value">The name of the <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> to look for in the collection</param>
        /// <returns>true if the <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> is in the collection; otherwise false</returns>
        public override bool Contains(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("value can't be null or empty");

            return _list.ContainsKey(value);
        }

        /// <inheritdoc />
        /// <summary>
        /// Indicates whether a <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> exists in the collection
        /// </summary>
        /// <param name="value">The <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> to look for</param>
        /// <returns>true if the <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> is in the collection; otherwise false</returns>
        public override bool Contains(object value)
        {
            return Contains((PythonParameter)value);
        }

        /// <summary>
        /// Indicates whether a <see cref="PythonParameter"/> exists in the collection
        /// </summary>
        /// <param name="parameter">The <see cref="PythonParameter"/> to look for</param>
        /// <returns>true if the <see cref="PythonParameter"/> is in the collection; otherwise false</returns>
        public bool Contains(PythonParameter parameter)
        {
            if (parameter == null) throw new ArgumentNullException(nameof(parameter));
            if (string.IsNullOrWhiteSpace(parameter.ParameterName)) throw new ArgumentException("ParameterName property can't be null or empty");

            return _list.ContainsKey(parameter.ParameterName);
        }

        /// <inheritdoc />
        /// <summary>
        /// Copies an array of items to the collection starting at the specified index
        /// </summary>
        /// <param name="array">The array of items to copy to the collection</param>
        /// <param name="index">The index in the collection to copy the items</param>
        public override void CopyTo(Array array, int index)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            if ((index < 0) || (index > array.Length)) throw new ArgumentOutOfRangeException(nameof(index));
            if ((array.Length - index) < _list.Count) throw new ArgumentException("(array.Length - index) < _list.Count");

            var q = _list.Values.ToArray();
            for (var i = index; i < Count; i++)
                array.SetValue(q[i], index++);
        }

        /// <inheritdoc />
        /// <summary>
        /// Exposes the GetEnumerator() method, which supports a simple iteration over a collection by a .NET Framework data provider
        /// </summary>
        /// <returns>An <see cref="T:System.Collections.IEnumerator" /> that can be used to iterate through the collection</returns>
        public override IEnumerator GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        /// <inheritdoc />
        /// <summary>
        /// Returns the index of the <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> object with the specified name
        /// </summary>
        /// <param name="parameterName">The name of the <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> object in the collection</param>
        /// <returns>The index of the <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> object with the specified name</returns>
        public override int IndexOf(string parameterName)
        {
            if (string.IsNullOrWhiteSpace(parameterName)) throw new ArgumentException("parameterName can't be null or empty");

            return _list.IndexOfKey(parameterName);
        }

        /// <summary>
        /// Returns the index of the <see cref="PythonParameter"/> object
        /// </summary>
        /// <param name="parameter">The <see cref="PythonParameter"/> object in the collection</param>
        /// <returns>The index of the <see cref="PythonParameter"/> object with the specified name</returns>
        public int IndexOf(PythonParameter parameter)
        {
            if (parameter == null) throw new ArgumentNullException(nameof(parameter));

            return IndexOf(parameter.ParameterName);
        }

        /// <inheritdoc />
        /// <summary>
        /// Returns the index of the <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> object
        /// </summary>
        /// <param name="value">The <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> object in the collection</param>
        /// <returns>The index of the <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> object with the specified name</returns>
        public override int IndexOf(object value)
        {
            return IndexOf((PythonParameter)value);
        }

        /// <inheritdoc />
        /// <summary>
        /// Inserts the specified index of the <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> object with the specified name into the collection at the specified index
        /// </summary>
        /// <param name="index">The index at which to insert the <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> object</param>
        /// <param name="value">The <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> object to insert into the collection</param>
        public override void Insert(int index, object value)
        {
            Add((PythonParameter)value);
        }

        /// <inheritdoc />
        /// <summary>
        /// Removes the <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> object from the collection
        /// </summary>
        /// <param name="value">The <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> object to remove</param>
        public override void Remove(object value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            var parameter = (PythonParameter)value;
            RemoveAt(parameter.ParameterName);
        }

        /// <inheritdoc />
        /// <summary>
        /// Removes the <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> object with the specified name from the collection
        /// </summary>
        /// <param name="parameterName">The name of the <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> object to remove</param>
        public override void RemoveAt(string parameterName)
        {
            if (string.IsNullOrWhiteSpace(parameterName)) throw new ArgumentException("parameterName can't be null or empty");

            _list.Remove(parameterName);
        }

        /// <inheritdoc />
        /// <summary>
        /// Removes the <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> object at the specified from the collection
        /// </summary>
        /// <param name="index">The index where the <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> object is located</param>
        public override void RemoveAt(int index)
        {
            _list.RemoveAt(index);
        }

        /// <inheritdoc />
        /// <summary>
        /// Returns <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> the object with the specified name
        /// </summary>
        /// <param name="parameterName">The name of the <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> in the collection</param>
        /// <returns>The <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> object with the specified name</returns>
        protected override DbParameter GetParameter(string parameterName)
        {
            if (string.IsNullOrWhiteSpace(parameterName)) throw new ArgumentException("parameterName can't be null or empty");

            return _list[parameterName];
        }

        /// <inheritdoc />
        /// <summary>
        /// Returns the <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> object at the specified index in the collection
        /// </summary>
        /// <param name="index">The index of the <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> in the collection</param>
        /// <returns>The <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> object at the specified index in the collection</returns>
        protected override DbParameter GetParameter(int index)
        {
            return _list.ElementAt(index).Value;
        }

        /// <inheritdoc />
        /// <summary>
        /// Sets the <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> object with the specified name to a new value
        /// </summary>
        /// <param name="parameterName">The name of the <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> object in the collection</param>
        /// <param name="value">The new <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> value</param>
        protected override void SetParameter(string parameterName, DbParameter value)
        {
            if (string.IsNullOrWhiteSpace(parameterName)) throw new ArgumentException("parameterName can't be null or empty");
            if (value == null) throw new ArgumentNullException(nameof(value));
            var parameter = (PythonParameter)value;
            if (string.IsNullOrWhiteSpace(parameter.ParameterName))
                parameter.ParameterName = parameterName;
            _list[parameterName] = parameter;
        }

        /// <inheritdoc />
        /// <summary>
        /// Sets the <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> object at the specified index to a new value
        /// </summary>
        /// <param name="index">The index where the <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> object is located</param>
        /// <param name="value">The new <see cref="T:Pike.PythonClient64.Data.PythonParameter" /> value</param>
        protected override void SetParameter(int index, DbParameter value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            var name = GetParameter(index).ParameterName;
            SetParameter(name, value);
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection
        /// </summary>
        public IEnumerable<PythonParameter> Values => _list.Values;
    }
}
