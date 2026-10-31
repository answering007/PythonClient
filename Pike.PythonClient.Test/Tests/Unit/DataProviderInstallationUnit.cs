using System;
using System.IO;
using System.Text;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pike.PythonClient64.Data;
using Pike.PythonClient64.Install;

namespace Pike.PythonClient.Test.Tests.Unit
{
    /// <summary>
    /// Unit tests for the data provider registration (regression tests for P0-5).
    /// The tests work on a copy of a machine.config-like file and do not require administrator rights
    /// </summary>
    [TestClass]
    [TestCategory("Unit")]
    public class DataProviderInstallationUnit
    {
        const string Invariant = "Pike.PythonClient64.Data.PythonProviderFactory";
        const string XPath = "/configuration/system.data/DbProviderFactories/add[@invariant='" + Invariant + "']";

        string _folder;
        string _configFile;

        /// <summary>
        /// Creates a temp folder for the config file
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            _folder = Path.Combine(Path.GetTempPath(), "Pike.PythonClient.Test", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
            _configFile = Path.Combine(_folder, "machine.config");
        }

        /// <summary>
        /// Removes the temp folder
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        void WriteConfig(string systemData)
        {
            var content = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
	<startup>
		<supportedRuntime version=""v4.0"" />
	</startup>
{systemData}
	<system.web>
		<httpRuntime maxRequestLength=""4096"" />
	</system.web>
</configuration>";

            File.WriteAllText(_configFile, content, new UTF8Encoding(false));
        }

        XmlDocument Load()
        {
            var xml = new XmlDocument();
            xml.Load(_configFile);

            return xml;
        }

        XmlElement GetProviderNode(XmlDocument xml)
        {
            return (XmlElement)xml.SelectSingleNode(XPath);
        }

        /// <summary>
        /// Missing system.data node is created together with the provider record (P0-5)
        /// </summary>
        [TestMethod]
        public void P0_5_Register_CreatesNodesInsideSystemData()
        {
            WriteConfig(string.Empty);

            new DbProviderInstallation().RegisterProvider(_configFile);

            var xml = Load();
            Assert.IsNotNull(xml.SelectSingleNode("/configuration/system.data"), "'system.data' node is missing");
            Assert.IsNotNull(GetProviderNode(xml), "provider record is missing");

            // DbProviderFactories must not be a direct child of configuration
            Assert.IsNull(xml.SelectSingleNode("/configuration/DbProviderFactories"));
        }

        /// <summary>
        /// Existed system.data node is reused, DbProviderFactories is created inside it (P0-5)
        /// </summary>
        [TestMethod]
        public void P0_5_Register_CreatesDbProviderFactoriesInsideSystemData()
        {
            WriteConfig("\t<system.data />");

            new DbProviderInstallation().RegisterProvider(_configFile);

            var xml = Load();
            Assert.IsNotNull(xml.SelectSingleNode("/configuration/system.data/DbProviderFactories"));
            Assert.IsNotNull(GetProviderNode(xml));
        }

        /// <summary>
        /// Existed record with the assembly version is updated (P0-5)
        /// </summary>
        [TestMethod]
        public void P0_5_Register_UpdatesExistedRecord()
        {
            WriteConfig(@"	<system.data>
		<DbProviderFactories>
			<add name=""Old name"" invariant=""" + Invariant + @""" description=""Old description"" type=""" + Invariant + @", Pike.PythonClient64, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"" />
		</DbProviderFactories>
	</system.data>");

            new DbProviderInstallation().RegisterProvider(_configFile);

            var xml = Load();
            var node = GetProviderNode(xml);
            Assert.IsNotNull(node);
            Assert.AreEqual("Python x64 Data Provider", node.GetAttribute("name"));
            Assert.AreEqual(".Net Framework Data Provider for Python x64", node.GetAttribute("description"));

            // The registered type must be the fully qualified name: only a full name is resolved from the GAC
            var typeName = node.GetAttribute("type");
            Assert.AreEqual(typeof(PythonProviderFactory).AssemblyQualifiedName, typeName);
            StringAssert.Contains(typeName, "Version=");
            StringAssert.Contains(typeName, "Culture=neutral");
            StringAssert.Contains(typeName, "PublicKeyToken=");
            Assert.AreNotEqual(0, typeof(PythonProviderFactory).Assembly.GetName().GetPublicKeyToken().Length,
                "the provider assembly must stay strong-named, otherwise it cannot be installed into the GAC");

            Assert.AreEqual(1, xml.SelectNodes(XPath).Count);
        }

        /// <summary>
        /// The record written by the previous releases (version independent, no PublicKeyToken) is replaced
        /// by the fully qualified name
        /// </summary>
        [TestMethod]
        public void P0_5_Register_ReplacesPartialNameWithFullName()
        {
            WriteConfig(@"	<system.data>
		<DbProviderFactories>
			<add name='Python x64 Data Provider' invariant='" + Invariant + @"' description='.Net Framework Data Provider for Python x64' type='" + Invariant + @", Pike.PythonClient64' />
		</DbProviderFactories>
	</system.data>");

            new DbProviderInstallation().RegisterProvider(_configFile);

            var node = GetProviderNode(Load());
            Assert.IsNotNull(node);
            Assert.AreEqual(typeof(PythonProviderFactory).AssemblyQualifiedName, node.GetAttribute("type"));
        }

        /// <summary>
        /// Registration is idempotent (P0-5)
        /// </summary>
        [TestMethod]
        public void P0_5_Register_IsIdempotent()
        {
            WriteConfig(string.Empty);

            var installation = new DbProviderInstallation();
            installation.RegisterProvider(_configFile);
            installation.RegisterProvider(_configFile);

            Assert.AreEqual(1, Load().SelectNodes(XPath).Count);
        }
        /// <summary>
        /// Registration creates a backup, keeps the XML declaration and writes UTF-8 without BOM (P0-5)
        /// </summary>
        [TestMethod]
        public void P0_5_Register_CreatesBackupAndKeepsDeclaration()
        {
            WriteConfig(string.Empty);

            new DbProviderInstallation().RegisterProvider(_configFile);

            var text = File.ReadAllText(_configFile);
            Assert.IsTrue(text.TrimStart('\uFEFF', ' ', '\r', '\n').StartsWith("<?xml "), "XML declaration is lost");
            Assert.IsTrue(text.Contains("/configuration"), "file was replaced by something unexpected");

            var backups = Directory.GetFiles(_folder, "machine.config.bak-*");
            Assert.AreEqual(1, backups.Length, "backup file is not created");

            var bytes = File.ReadAllBytes(_configFile);
            Assert.IsFalse(bytes.Length > 2 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                "file must not contain BOM");
        }

        /// <summary>
        /// Comment and other sections of the config file are preserved (P0-5)
        /// </summary>
        [TestMethod]
        public void P0_5_Register_KeepsOtherSections()
        {
            WriteConfig(string.Empty);

            new DbProviderInstallation().RegisterProvider(_configFile);

            var xml = Load();
            Assert.IsNotNull(xml.SelectSingleNode("/configuration/startup/supportedRuntime"));
            Assert.IsNotNull(xml.SelectSingleNode("/configuration/system.web/httpRuntime"));
        }

        /// <summary>
        /// Unregister removes only the provider record (P0-5)
        /// </summary>
        [TestMethod]
        public void P0_5_Unregister_RemovesRecord()
        {
            WriteConfig(string.Empty);

            var installation = new DbProviderInstallation();
            installation.RegisterProvider(_configFile);
            installation.UnregisterProvider(_configFile);

            var xml = Load();
            Assert.IsNull(GetProviderNode(xml));
            Assert.IsNotNull(xml.SelectSingleNode("/configuration/system.data/DbProviderFactories"));
            Assert.IsNotNull(xml.SelectSingleNode("/configuration/startup"));
        }

        /// <summary>
        /// Unregister of a missed record does not change the file (P0-5)
        /// </summary>
        [TestMethod]
        public void P0_5_Unregister_MissingRecordIsIgnored()
        {
            WriteConfig("\t<system.data />");
            var before = File.ReadAllText(_configFile);

            new DbProviderInstallation().UnregisterProvider(_configFile);

            Assert.AreEqual(before, File.ReadAllText(_configFile));
        }

        /// <summary>
        /// machine.config paths point to the .NET Framework directories (P0-5, MSBuild independent)
        /// </summary>
        [TestMethod]
        public void P0_5_MachineConfigPaths_PointToFrameworkDirectories()
        {
            var machine64 = DbProviderInstallation.Machine64Config.Replace('\\', '/');
            var machine32 = DbProviderInstallation.Machine32Config.Replace('\\', '/');

            StringAssert.Contains(machine64, "/Framework64/");
            StringAssert.Contains(machine32, "/Framework/");
            StringAssert.EndsWith(machine64, "/Config/machine.config");
            StringAssert.EndsWith(machine32, "/Config/machine.config");
            Assert.IsTrue(File.Exists(DbProviderInstallation.Machine64Config), "x64 machine.config is not found");
        }
    }
}
