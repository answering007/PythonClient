using System;
using System.Data;
using System.Data.Common;

namespace Pike.PythonClient64.Data
{
    /// <inheritdoc />
    /// <summary>
    /// Represents a parameter to <see cref="T:Pike.PythonClient64.Data.PythonCommand" />
    /// </summary>
    public class PythonParameter: DbParameter
    {
        /// <summary>
        /// Get associated <see cref="DbType"/> from <see cref="Type"/>
        /// </summary>
        /// <param name="type">Source type</param>
        /// <returns>Associated <see cref="DbType"/></returns>
        static DbType ParseDbType(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            var typeCode = Type.GetTypeCode(type);
            switch (typeCode)
            {
                case TypeCode.Boolean:
                    return DbType.Boolean;
                case TypeCode.DateTime:
                    return DbType.DateTime;
                case TypeCode.Double:
                    return DbType.Double;
                case TypeCode.Int32:
                    return DbType.Int32;
                case TypeCode.Int64:
                    return DbType.Int64;
                case TypeCode.String:
                    return DbType.String;
                default:
                    throw new SystemException("Value is of unknown data type");
            }
        }

        /// <summary>
        /// Get size of the primitive type
        /// </summary>
        /// <param name="type">Primitive type value</param>
        /// <param name="value">Object for the <see cref="string"/> data type</param>
        /// <returns>Size of the type</returns>
        static int GetSize(DbType type, object value = null)
        {
            switch (type)
            {
                case DbType.Boolean:
                    return sizeof(bool);
                case DbType.DateTime:
                    return 8;
                case DbType.Double:
                    return sizeof(double);
                case DbType.Int32:
                    return sizeof(int);
                case DbType.Int64:
                    return sizeof(long);
                case DbType.String:
                    return ((string)value)?.Length ?? 0;
                default:
                    throw new SystemException("Unknown data type");
            }
        }

        DbType _dbType = DbType.String;
        /// <inheritdoc />
        /// <summary>
        /// Gets the <see cref="P:Pike.PythonClient64.Data.PythonParameter.DbType" /> of the parameter.
        /// This can't be set directly - just set the <see cref="Value"/> instead
        /// </summary>
        public override DbType DbType
        {
            get => _dbType;
            set => throw new InvalidOperationException("Parameter type can't be set directly. Just set the Value");
        }

        /// <inheritdoc />
        /// <summary>
        /// Gets or sets a value that indicates whether the parameter is input-only, output-only, bidirectional, or a stored procedure return value parameter.
        /// Supported type is input-only
        /// </summary>
        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;

        /// <inheritdoc />
        /// <summary>
        /// Gets or sets a value that indicates whether the parameter accepts null values.
        /// Default value is false
        /// </summary>
        public override bool IsNullable { get; set; } = false;

        string _parameterName = string.Empty;
        /// <inheritdoc />
        /// <summary>
        /// Gets or sets the name of the parameter. Can't be renamed after first set
        /// </summary>
        public override string ParameterName
        {
            get => _parameterName;
            set
            {
                if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("value can't be null or empty");
                if (!string.IsNullOrWhiteSpace(_parameterName)) throw new InvalidOperationException("ParameterName is already defined");

                _parameterName = value;
            }
        }

        /// <inheritdoc />
        /// <summary>
        /// Gets or sets the maximum size, in bytes, of the data within the parameter
        /// </summary>
        public override int Size { get; set; }

        /// <inheritdoc />
        /// <summary>
        /// Gets or sets the name of the source column mapped to the <see cref="T:System.Data.DataSet" /> and used for loading or returning the <see cref="P:Pike.PythonClient64.Data.PythonParameter.Value" />
        /// </summary>
        public override string SourceColumn { get; set; }

        /// <inheritdoc />
        /// <summary>
        /// Sets or gets a value which indicates whether the source column is nullable
        /// </summary>
        public override bool SourceColumnNullMapping { get; set; } = false;

        /// <inheritdoc />
        /// <summary>
        /// Gets or sets the <see cref="T:System.Data.DataRowVersion" /> to use when you load <see cref="P:Pike.PythonClient64.Data.PythonParameter.Value" />
        /// </summary>
        public override DataRowVersion SourceVersion { get; set; } = DataRowVersion.Current;

        object _value;
        /// <inheritdoc />
        /// <summary>
        /// Gets or sets the value of the parameter
        /// </summary>
        public override object Value
        {
            get => _value;
            set
            {
                _value = value;
                _dbType = ParseDbType(_value.GetType());
                Size = GetSize(DbType, _value);
            }
        }
        /// <inheritdoc />
        /// <summary>
        /// Resets the DbType property to its original settings
        /// </summary>
        public override void ResetDbType()
        {
            _dbType = DbType.String;
        }
    }
}