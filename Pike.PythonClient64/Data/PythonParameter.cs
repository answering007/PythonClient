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
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is null</exception>
        /// <exception cref="NotSupportedException">Thrown when <paramref name="type"/> has no associated <see cref="DbType"/></exception>
        static DbType ParseDbType(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                    return DbType.Boolean;
                case TypeCode.Byte:
                    return DbType.Byte;
                case TypeCode.Char:
                    return DbType.String;
                case TypeCode.DateTime:
                    return DbType.DateTime;
                case TypeCode.Decimal:
                    return DbType.Decimal;
                case TypeCode.Double:
                    return DbType.Double;
                case TypeCode.Int16:
                    return DbType.Int16;
                case TypeCode.Int32:
                    return DbType.Int32;
                case TypeCode.Int64:
                    return DbType.Int64;
                case TypeCode.SByte:
                    return DbType.SByte;
                case TypeCode.Single:
                    return DbType.Single;
                case TypeCode.String:
                    return DbType.String;
                case TypeCode.UInt16:
                    return DbType.UInt16;
                case TypeCode.UInt32:
                    return DbType.UInt32;
                case TypeCode.UInt64:
                    return DbType.UInt64;
            }

            if (type == typeof(Guid)) return DbType.Guid;
            if (type == typeof(TimeSpan)) return DbType.Time;
            if (type == typeof(DateTimeOffset)) return DbType.DateTimeOffset;
            if (type == typeof(byte[])) return DbType.Binary;

            throw new NotSupportedException($"Value of type {type} is not supported");
        }

        /// <summary>
        /// Get size of the primitive type
        /// </summary>
        /// <param name="type">Primitive type value</param>
        /// <param name="value">Object for the <see cref="T:System.String"/> and <see cref="T:System.Byte"/> array data types</param>
        /// <returns>Size of the type</returns>
        static int GetSize(DbType type, object value = null)
        {
            switch (type)
            {
                case DbType.Boolean:
                    return sizeof(bool);
                case DbType.Byte:
                    return sizeof(byte);
                case DbType.SByte:
                    return sizeof(sbyte);
                case DbType.Int16:
                    return sizeof(short);
                case DbType.UInt16:
                    return sizeof(ushort);
                case DbType.Int32:
                    return sizeof(int);
                case DbType.UInt32:
                    return sizeof(uint);
                case DbType.Int64:
                    return sizeof(long);
                case DbType.UInt64:
                    return sizeof(ulong);
                case DbType.Single:
                    return sizeof(float);
                case DbType.Double:
                    return sizeof(double);
                case DbType.Decimal:
                case DbType.Currency:
                    return sizeof(decimal);
                case DbType.DateTime:
                case DbType.DateTime2:
                case DbType.DateTimeOffset:
                    return 8;
                case DbType.Time:
                    return 8;
                case DbType.Guid:
                    return 16;
                case DbType.String:
                    return (value as string)?.Length ?? 0;
                case DbType.Binary:
                    return (value as byte[])?.Length ?? 0;
                default:
                    return 0;
            }
        }

        DbType _dbType = DbType.String;
        bool _dbTypeIsSetExplicitly;

        /// <inheritdoc />
        /// <summary>
        /// Gets or sets the <see cref="P:Pike.PythonClient64.Data.PythonParameter.DbType" /> of the parameter.
        /// The value is used as a hint only, <see cref="Value"/> remains the source of truth
        /// </summary>
        public override DbType DbType
        {
            get => _dbType;
            set
            {
                _dbType = value;
                _dbTypeIsSetExplicitly = true;
            }
        }

        ParameterDirection _direction = ParameterDirection.Input;

        /// <inheritdoc />
        /// <summary>
        /// Gets or sets a value that indicates whether the parameter is input-only, output-only, bidirectional, or a stored procedure return value parameter.
        /// Supported type is input-only
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown when direction other than <see cref="ParameterDirection.Input"/> is set</exception>
        public override ParameterDirection Direction
        {
            get => _direction;
            set
            {
                if (value != ParameterDirection.Input)
                    throw new NotSupportedException(
                        $"Direction '{value}' is not supported, only '{ParameterDirection.Input}' is supported");

                _direction = value;
            }
        }

        /// <inheritdoc />
        /// <summary>
        /// Gets or sets a value that indicates whether the parameter accepts null values.
        /// Default value is true
        /// </summary>
        public override bool IsNullable { get; set; } = true;

        string _parameterName = string.Empty;

        /// <inheritdoc />
        /// <summary>
        /// Gets or sets the name of the parameter
        /// </summary>
        public override string ParameterName
        {
            get => _parameterName;
            set => _parameterName = value ?? string.Empty;
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
        /// Gets or sets the value of the parameter. Null and <see cref="T:System.DBNull"/> values are allowed
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown when value has no associated <see cref="DbType"/></exception>
        public override object Value
        {
            get => _value;
            set
            {
                _value = value;
                if (!_dbTypeIsSetExplicitly)
                    _dbType = value == null || value == DBNull.Value ? DbType.String : ParseDbType(value.GetType());
                Size = GetSize(_dbType, _value);
            }
        }
        /// <inheritdoc />
        /// <summary>
        /// Resets the DbType property to its original settings
        /// </summary>
        public override void ResetDbType()
        {
            _dbType = DbType.String;
            _dbTypeIsSetExplicitly = false;
        }
    }
}