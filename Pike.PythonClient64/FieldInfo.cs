using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Pike.PythonClient64
{
    public partial class PythonUtils
    {
        /// <summary>
        /// Represent pandas column name and type
        /// </summary>
        public class FieldInfo
        {
            static readonly IDictionary<string, Type> SupportedFields = new Dictionary<string, Type>
            {
                {"string", typeof(string)},
                {"boolean", typeof(bool)},
                {"number", typeof(double)},
                {"integer", typeof(int)},
                {"duration", typeof(TimeSpan)},
                {"datetime", typeof(DateTime)}
            };

            /// <summary>
            /// Column name
            /// </summary>
            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; }
            /// <summary>
            /// Column type
            /// </summary>
            [JsonProperty(PropertyName = "type")]
            public string Type { get; set; }
            /// <summary>
            /// Managed column type
            /// </summary>
            [JsonIgnore]
            public Type ManagedType => SupportedFields.ContainsKey(Type) ? SupportedFields[Type] : typeof(string);
        }
    }
}