using System;
using System.Data;
using System.Xml;
using Newtonsoft.Json;

namespace Pike.PythonClient64
{
    /// <inheritdoc />
    /// <summary>
    /// Represent pandas <seealso cref="JsonConverter"/>
    /// </summary>
    public class DataTableReadConverter : JsonConverter
    {
        /// <summary>
        /// Result data table
        /// </summary>
        public DataTable ResulTable { get; internal set; }

        static void ReadAndAssert(JsonReader reader)
        {
            if (!reader.Read()) throw new FormatException("Unexpected end when reading JSON.");
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        /// <exception cref="T:System.FormatException"></exception>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;

            if (reader.TokenType != JsonToken.StartArray)
                throw new FormatException($"Unexpected JSON token when reading DataTable. Expected StartArray, got {reader.TokenType}.");

            ReadAndAssert(reader);

            while (reader.TokenType != JsonToken.EndArray)
            {
                CreateRow(reader, ResulTable, serializer);
                ReadAndAssert(reader);
            }

            return ResulTable;
        }

        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(DataTable);
        }

        void CreateRow(JsonReader reader, DataTable dataTable, JsonSerializer serializer)
        {
            var dataRow = dataTable.NewRow();
            ReadAndAssert(reader);

            while (reader.TokenType == JsonToken.PropertyName)
            {
                var columnName = (string)reader.Value;
                if (columnName == null) throw new InvalidOperationException();
                ReadAndAssert(reader);

                var column = ResulTable.Columns[columnName];

                object columnValue;
                if (column.DataType == typeof(TimeSpan))
                {
                    var serializedTimeSpan = serializer.Deserialize<string>(reader);
                    columnValue = XmlConvert.ToTimeSpan(serializedTimeSpan);
                }
                else
                {
                    columnValue = reader.Value != null
                        ? serializer.Deserialize(reader, column.DataType) ?? DBNull.Value
                        : DBNull.Value;
                }

                dataRow[columnName] = columnValue;
                ReadAndAssert(reader);
            }

            dataTable.Rows.Add(dataRow);
        }
    }
}