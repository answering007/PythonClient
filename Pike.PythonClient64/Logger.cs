using log4net;
using log4net.Appender;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using System.Data;
using System;
using System.Linq;
using System.Text;

namespace Pike.PythonClient64
{
    public static class Logger
    {
        static Logger()
        {
            ConfigureLog4Net();
        }
        private static void ConfigureLog4Net()
        {
            // Create a file appender
            var fileAppender = new FileAppender
            {
                Name = "FileAppender",
                File = @"C:\Users\Pike\Desktop\Log.txt", // Path to the log file
                AppendToFile = true,
                Layout = new PatternLayout("%date [%thread] %-5level %logger - %message%newline")
            };
            fileAppender.ActivateOptions(); // Activate the appender

            // Create a root logger and set its level and add the appender
            var hierarchy = (Hierarchy)LogManager.GetRepository();
            var rootLogger = hierarchy.Root;
            rootLogger.Level = log4net.Core.Level.Debug; // Set logging level
            rootLogger.AddAppender(fileAppender);

            // Activate the configuration
            hierarchy.Configured = true;
        }

        public static string ToStringData(this DataTable dataTable)
        {
            var sb = new StringBuilder();
            
            var columnLine = string.Join("\t", dataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
            sb.AppendLine(columnLine);
            foreach (var row in dataTable.Rows.Cast<DataRow>())
            {
                var rowLine = string.Join("\t", row.ItemArray);
                sb.AppendLine(rowLine);
            }

            return sb.ToString();
        }

        public static ILog Log { get; } = LogManager.GetLogger("Main");
    }
}