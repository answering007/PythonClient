namespace Pike.PythonClient64.FrameReaders
{
    /// <summary>
    /// Kind of the python frame returned by the script (diagnostics only)
    /// </summary>
    internal enum FrameKind
    {
        /// <summary>
        /// Unknown python object
        /// </summary>
        Unknown,

        /// <summary>
        /// pandas.DataFrame
        /// </summary>
        Pandas,

        /// <summary>
        /// polars.DataFrame
        /// </summary>
        Polars
    }
}
