using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;

namespace Pike.PythonClient64.Install
{
    /// <summary>
    /// Base Ado.Net data provider registration class.
    /// The config file is updated safely: backup file → temp file → atomic replace
    /// </summary>
    public abstract class DataProviderInstallationBase
    {
        /// <summary>
        /// Config name
        /// </summary>
        public const string Config = "Config";
        /// <summary>
        /// machine.config file name
        /// </summary>
        public const string MachineFileName = "machine.config";

        /// <summary>
        /// Configuration section name
        /// </summary>
        public const string Configuration = "configuration";
        /// <summary>
        /// system.data section name
        /// </summary>
        public const string SystemData = "system.data";
        /// <summary>
        /// DbProviderFactories section name
        /// </summary>
        public const string DbProviderFactories = "DbProviderFactories";
        /// <summary>
        /// add keyword
        /// </summary>
        public const string Add = "add";

        /// <summary>
        /// Name keyword
        /// </summary>
        public const string NameKey = "name";
        /// <summary>
        /// Invariant keyword
        /// </summary>
        public const string InvariantNameKey = "invariant";
        /// <summary>
        /// Description keyword
        /// </summary>
        public const string DescriptionKey = "description";
        /// <summary>
        /// Type keyword
        /// </summary>
        public const string TypeKey = "type";

        /// <summary>
        /// Full path to x32 machine.config file
        /// </summary>
        public static string Machine32Config => GetMachineConfigFile(false);

        /// <summary>
        /// Full path to x64 machine.config file
        /// </summary>
        public static string Machine64Config => GetMachineConfigFile(true);

        /// <summary>
        /// Gets machine.config full path for the specified architecture.
        /// The path is built from the current runtime directory, so there is no dependency on MSBuild
        /// </summary>
        /// <param name="use64Bit">True for the 64 bit .NET Framework directory</param>
        /// <returns>Full path to machine.config</returns>
        static string GetMachineConfigFile(bool use64Bit)
        {
            var runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory()
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var directory = new DirectoryInfo(runtimeDirectory);
            var frameworkDirectory = directory.Parent;

            if (frameworkDirectory?.Parent == null)
                return Path.Combine(runtimeDirectory, Config, MachineFileName);

            // Current runtime directory is ...\Microsoft.NET\Framework[64]\v4.0.x, so the last but one
            // segment defines the architecture
            var isCurrent64Bit = frameworkDirectory.Name.Equals("Framework64", StringComparison.OrdinalIgnoreCase);
            var fullFrameworkDirectory = isCurrent64Bit == use64Bit
                ? runtimeDirectory
                : Path.Combine(frameworkDirectory.Parent.FullName, use64Bit ? "Framework64" : "Framework",
                    directory.Name);

            return Path.Combine(fullFrameworkDirectory, Config, MachineFileName);
        }

        /// <summary>
        /// Get <see cref="XmlWriter"/> parameters. File is written as UTF-8 without BOM
        /// </summary>
        public static XmlWriterSettings XmlWriterSettings => new XmlWriterSettings
        {
            NewLineOnAttributes = false,
            Indent = true,
            IndentChars = "\t",
            Encoding = new UTF8Encoding(false)
        };

        /// <summary>
        /// Provider name
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Provider description
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Provider invariant name
        /// </summary>
        public string Invariant { get; }

        /// <summary>
        /// Provider qualified name
        /// </summary>
        public string QualifiedName { get; }

        /// <summary>
        /// Create an instance of <see cref="DataProviderInstallationBase"/>
        /// </summary>
        /// <param name="type">Provider type</param>
        /// <param name="name">Provider name</param>
        /// <param name="description">Provider description</param>
        protected DataProviderInstallationBase(Type type, string name, string description)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Parameter can't be null or empty", nameof(name));
            if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Parameter can't be null or empty", nameof(description));

            Name = name;
            Description = description;
            Invariant = type.FullName;
            // Fully qualified name (Version, Culture, PublicKeyToken): the runtime resolves an assembly outside
            // the host application base only by a full name, which is what the GAC installation relies on.
            // AssemblyVersion is fixed for the whole 2.x line (Directory.Build.props), so the registered record
            // stays valid across provider upgrades.
            QualifiedName = type.AssemblyQualifiedName;
        }

        /// <summary>
        /// Register data provider
        /// </summary>
        /// <param name="fileName">Configuration file name</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="fileName"/> is null or empty</exception>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist</exception>
        /// <exception cref="InvalidOperationException">Thrown when the file has no 'configuration' node</exception>
        public void RegisterProvider(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("file can't be null or empty", nameof(fileName));
            if (!File.Exists(fileName)) throw new FileNotFoundException("file not found", fileName);

            var xml = Load(fileName);
            var configuration = xml.ChildNodes.OfType<XmlElement>()
                                    .FirstOrDefault(n => n.Name.Equals(Configuration, StringComparison.OrdinalIgnoreCase))
                                ?? throw new InvalidOperationException(
                                    $"'{Configuration}' node is not found in '{fileName}'");

            var data = GetOrCreate(configuration, xml, SystemData, configuration);
            // DbProviderFactories node is a child of system.data, not of configuration
            var dbProviderFactories = GetOrCreate(data, xml, DbProviderFactories, data);

            var dataProviderNode = dbProviderFactories.ChildNodes.OfType<XmlElement>()
                .FirstOrDefault(n => n.HasAttribute(InvariantNameKey) &&
                                     n.GetAttribute(InvariantNameKey)
                                         .Equals(Invariant, StringComparison.OrdinalIgnoreCase));

            if (dataProviderNode == null)
            {
                dataProviderNode = xml.CreateElement(Add);
                dbProviderFactories.AppendChild(dataProviderNode);
            }

            // Attributes are updated for the existed node too, so the assembly upgrade is picked up
            dataProviderNode.SetAttribute(NameKey, Name);
            dataProviderNode.SetAttribute(InvariantNameKey, Invariant);
            dataProviderNode.SetAttribute(DescriptionKey, Description);
            dataProviderNode.SetAttribute(TypeKey, QualifiedName);

            Save(fileName, xml);
        }

        /// <summary>
        /// UnRegister data provider
        /// </summary>
        /// <param name="fileName">Configuration file name</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="fileName"/> is null or empty</exception>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist</exception>
        public void UnregisterProvider(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("file can't be null or empty", nameof(fileName));
            if (!File.Exists(fileName)) throw new FileNotFoundException("file not found", fileName);

            var xml = Load(fileName);
            var configuration = xml.ChildNodes.OfType<XmlElement>()
                .FirstOrDefault(n => n.Name.Equals(Configuration, StringComparison.OrdinalIgnoreCase));
            var data = configuration?.ChildNodes.OfType<XmlElement>()
                .FirstOrDefault(n => n.Name.Equals(SystemData, StringComparison.OrdinalIgnoreCase));
            var dbProviderFactories = data?.ChildNodes.OfType<XmlElement>()
                .FirstOrDefault(n => n.Name.Equals(DbProviderFactories, StringComparison.OrdinalIgnoreCase));

            var dataProviderNode = dbProviderFactories?.ChildNodes.OfType<XmlElement>()
                .FirstOrDefault(n => n.HasAttribute(InvariantNameKey) &&
                                     n.GetAttribute(InvariantNameKey)
                                         .Equals(Invariant, StringComparison.OrdinalIgnoreCase));
            if (dataProviderNode == null) return;

            dbProviderFactories.RemoveChild(dataProviderNode);
            Save(fileName, xml);
        }

        /// <summary>
        /// Gets the child node or creates it in the specified parent
        /// </summary>
        /// <param name="parent">Parent node used to search the child</param>
        /// <param name="xml">Target document</param>
        /// <param name="name">Child node name</param>
        /// <param name="target">Node the new child is appended to</param>
        /// <returns>Found or created node</returns>
        static XmlElement GetOrCreate(XmlElement parent, XmlDocument xml, string name, XmlElement target)
        {
            var element = parent.ChildNodes.OfType<XmlElement>()
                .FirstOrDefault(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (element != null) return element;

            element = xml.CreateElement(name);
            target.AppendChild(element);
            return element;
        }

        /// <summary>
        /// Loads the config file keeping the XML declaration
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <returns>Loaded document</returns>
        static XmlDocument Load(string fileName)
        {
            var xml = new XmlDocument();
            xml.Load(fileName);

            return xml;
        }

        /// <summary>
        /// Saves the config file safely: temp file → validation → backup → atomic replace
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <param name="xml">Document to save</param>
        static void Save(string fileName, XmlDocument xml)
        {
            var fullFileName = Path.GetFullPath(fileName);
            var directory = Path.GetDirectoryName(fullFileName) ?? ".";
            var tempFileName = Path.Combine(directory, $"{Path.GetFileName(fullFileName)}.{Guid.NewGuid():N}.tmp");
            var backupFileName = $"{fullFileName}.bak-{DateTime.UtcNow:yyyyMMddHHmmss}";

            try
            {
                using (var xmlWriter = XmlWriter.Create(tempFileName, XmlWriterSettings))
                    xml.Save(xmlWriter);

                // Validate the result before the original file is replaced
                var check = new XmlDocument();
                check.Load(tempFileName);

                if (File.Exists(backupFileName)) File.Delete(backupFileName);
                File.Replace(tempFileName, fullFileName, backupFileName, true);
            }
            finally
            {
                if (File.Exists(tempFileName)) File.Delete(tempFileName);
            }
        }
    }
}