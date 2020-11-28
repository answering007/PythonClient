using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Microsoft.Build.Utilities;

namespace Pike.PythonClient64.Install
{
    /// <summary>
    /// Base Ado.Net data provider registration class
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
        public static string Machine32Config
            =>
                Path.Combine(
                    ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version40,
                        DotNetFrameworkArchitecture.Bitness32), Config, MachineFileName);

        /// <summary>
        /// Full path to x64 machine.config file
        /// </summary>
        public static string Machine64Config
            =>
                Path.Combine(
                    ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version40,
                        DotNetFrameworkArchitecture.Bitness64), Config, MachineFileName);

        /// <summary>
        /// Get <see cref="XmlWriter"/> parameters
        /// </summary>
        public static XmlWriterSettings XmlWriterSettings => new XmlWriterSettings
        {
            NewLineOnAttributes = false,
            Indent = true,
            IndentChars = "\t",
            Encoding = Encoding.UTF8
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
            QualifiedName = type.AssemblyQualifiedName;
        }

        /// <summary>
        /// Register data provider
        /// </summary>
        /// <param name="fileName">Configuration file name</param>
        public void RegisterProvider(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("file can't be null or empty");
            if (!File.Exists(fileName)) throw new FileNotFoundException("file not found", fileName);

            var xml = new XmlDocument { InnerXml = File.ReadAllText(fileName) };
            var configuration =
                xml.ChildNodes.OfType<XmlElement>()
                    .First(n => n.Name.Equals(Configuration, StringComparison.InvariantCultureIgnoreCase));
            var data =
                configuration.ChildNodes.OfType<XmlElement>()
                    .FirstOrDefault(n => n.Name.Equals(SystemData, StringComparison.InvariantCultureIgnoreCase));
            if (data == null)
            {
                data = xml.CreateElement(SystemData);
                configuration.AppendChild(data);
            }
            var dbProviderFactories =
                data.ChildNodes.OfType<XmlElement>()
                    .FirstOrDefault(n => n.Name.Equals(DbProviderFactories, StringComparison.InvariantCultureIgnoreCase));
            if (dbProviderFactories == null)
            {
                dbProviderFactories = xml.CreateElement(DbProviderFactories);
                configuration.AppendChild(dbProviderFactories);
            }
            var dataProviderNode =
                dbProviderFactories.ChildNodes.OfType<XmlElement>()
                    .FirstOrDefault(
                        n =>
                            n.HasAttribute(InvariantNameKey) &&
                            n.GetAttribute(InvariantNameKey)
                                .Equals(Invariant, StringComparison.InvariantCultureIgnoreCase));
            if (dataProviderNode == null)
            {
                dataProviderNode = xml.CreateElement(Add);
                dataProviderNode.SetAttribute(NameKey, Name);
                dataProviderNode.SetAttribute(InvariantNameKey, Invariant);
                dataProviderNode.SetAttribute(DescriptionKey, Description);
                dataProviderNode.SetAttribute(TypeKey, QualifiedName);
                dbProviderFactories.AppendChild(dataProviderNode);
            }
            using (var xmlWriter = XmlWriter.Create(fileName, XmlWriterSettings))
                xml.WriteTo(xmlWriter);
        }

        /// <summary>
        /// UnRegister data provider
        /// </summary>
        /// <param name="fileName">Configuration file name</param>
        public void UnregisterProvider(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("file can't be null or empty");
            if (!File.Exists(fileName)) throw new FileNotFoundException("file not found", fileName);

            var xml = new XmlDocument { InnerXml = File.ReadAllText(fileName) };
            var configuration =
                xml.ChildNodes.OfType<XmlElement>()
                    .First(n => n.Name.Equals(Configuration, StringComparison.InvariantCultureIgnoreCase));
            var data =
                configuration.ChildNodes.OfType<XmlElement>()
                    .FirstOrDefault(n => n.Name.Equals(SystemData, StringComparison.InvariantCultureIgnoreCase));
            var dbProviderFactories =
                data?.ChildNodes.OfType<XmlElement>()
                    .FirstOrDefault(n => n.Name.Equals(DbProviderFactories, StringComparison.InvariantCultureIgnoreCase));

            var dataProviderNode =
                dbProviderFactories?.ChildNodes.OfType<XmlElement>()
                    .FirstOrDefault(
                        n =>
                            n.HasAttribute(InvariantNameKey) &&
                            n.GetAttribute(InvariantNameKey)
                                .Equals(Invariant, StringComparison.InvariantCultureIgnoreCase));
            if (dataProviderNode == null) return;

            dbProviderFactories.RemoveChild(dataProviderNode);
            using (var xmlWriter = XmlWriter.Create(fileName, XmlWriterSettings))
                xml.WriteTo(xmlWriter);
        }
    }
}