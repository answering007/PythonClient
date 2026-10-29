using System.Collections.Generic;

namespace Pike.PythonClient64
{
    /// <summary>
    /// Python script execution context: query text, parameters and result settings.
    /// The context is a per-call object, so parallel commands do not share any state
    /// </summary>
    public sealed class PythonScriptContext
    {
        /// <summary>
        /// Python code text passed to the script as a 'query' variable
        /// </summary>
        public string Query { get; set; }

        /// <summary>
        /// Parameters passed to the script as a 'params' variable
        /// </summary>
        public IDictionary<string, object> Parameters { get; } = new Dictionary<string, object>();

        /// <summary>
        /// Name of the script variable that holds the pandas.DataFrame result. Default value is 'result'
        /// </summary>
        public string ResultName { get; set; } = PythonDataQuery.ResultKey;

        /// <summary>
        /// True (default) to require the result variable.
        /// False to return an empty table when the script does not define the result variable
        /// </summary>
        public bool ResultRequired { get; set; } = true;
    }
}
