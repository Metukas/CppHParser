using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace ParseCppAndH
{
    [Serializable]
    class Configuration
    {
        const char assignment = '=';
        const char separator = ';';
        //const char commentChar = '#';
        private static Configuration _instance;
        public static Configuration Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new Configuration()
                    {
                        _CpphFileDir = Environment.CurrentDirectory,
                        _HeaderFileDir = Environment.CurrentDirectory,
                        _SourceFileDir = Environment.CurrentDirectory,
                        _VcprojFilePath = ""
                    };
                }
                return _instance;
            }
        }           

        string configFilePath;
        string _CpphFileDir   ;
        string _HeaderFileDir ;
        string _SourceFileDir ;
        string _VcprojFilePath;

        public string CpphFileDir
        {
            get => _CpphFileDir;
            private set
            {
                if(Directory.Exists(value))
                {
                    _CpphFileDir = value;
                }
                else
                {
                    _CpphFileDir = Environment.CurrentDirectory;
                }
            }
        }
        public string HeaderFileDir
        {
            get => _HeaderFileDir;
            private set
            {
                if (Directory.Exists(value))
                {
                    _HeaderFileDir = value;
                }
                else
                {
                    _HeaderFileDir = Environment.CurrentDirectory;
                }
            }
        }
        public string SourceFileDir
        {
            get => _SourceFileDir;
            private set
            {
                if (Directory.Exists(value))
                {
                    _SourceFileDir = value;
                }
                else
                {
                    _SourceFileDir = Environment.CurrentDirectory;
                }
            }
        }
        public string VcprojFilePath
        {
            get => _VcprojFilePath;
            private set
            {
                if (File.Exists(value))
                {
                    _VcprojFilePath = value;
                }
                else
                {
                    _VcprojFilePath = "";
                }
            }
        }

        public string Separator { get; private set; }

        private Configuration() {}

        public Configuration(string cpphFileDirectoryToWatch, string headerFileDirectory,
            string sourceFileDirectory, string vcprojFileLocation)
        {
            CpphFileDir = cpphFileDirectoryToWatch;
            HeaderFileDir = headerFileDirectory;
            SourceFileDir = sourceFileDirectory;
            VcprojFilePath = vcprojFileLocation;
        }

        public string SerializeToCustomString()
        {
            string serialized =
                $"{nameof(CpphFileDir)}=\"{CpphFileDir}\";\n" +
                $"{nameof(HeaderFileDir)}=\"{HeaderFileDir}\";\n" +
                $"{nameof(SourceFileDir)}=\"{SourceFileDir}\";\n" +
                $"{nameof(VcprojFilePath)}=\"{VcprojFilePath}\";\n" +
                $"{nameof(Separator)}=\"{Separator}\";\n";

            return serialized;
        }

        public static bool Initialize(string configFilePath)
        {
            string configFileContents;
            try
            {
                configFileContents = File.ReadAllText(configFilePath);
            }
            catch(FileNotFoundException ex)
            {
                Console.WriteLine($"File {ex.FileName} not found");
                return false;
            }
            _instance = Configuration.DeserializeFromCustomString(configFileContents);
            if (_instance == null)
            {
                return false;
            }
            _instance.configFilePath = configFilePath;
            return true;
        }

        public void ResetConfig()
        {
            Initialize(configFilePath);
        }

        internal void ChangeConfigFile(string newConfigFile)
        {
            Initialize(newConfigFile);
        }

        private static Configuration DeserializeFromCustomString(string stringObject)
        {
            // doing some regex bullshit:
            // ištrina c stiliaus komentarus:
            stringObject = Regex.Replace(stringObject, @"(/\*([^*]|[\r\n]|(\*+([^*/]|[\r\n])))*\*+/)|(//.*)",
                "", RegexOptions.None);

            // Matchinam kiekvieną objekto propertį.
            // TODO: kodėl neveikia jeigu yra komentarų slashai? /
            string regexPattern = $"(?<Name>\\S*)\\s*=\\s*\"(?<Value>[\\w\\\\_:,.#\\/]*?)\";";
            Dictionary<string, string> properties = new Dictionary<string, string>(4);
            Regex regexEng = new Regex(regexPattern);
            MatchCollection matches = regexEng.Matches(stringObject);
            foreach(Match m in matches)
            {
                if(!properties.ContainsKey(m.Groups["Name"].Value.ToLower()))
                    properties.Add(m.Groups["Name"].Value.ToLower(), m.Groups["Value"].Value);
            }

            Configuration newConfig = new Configuration();
            ParsePropertyDictionary();
            return newConfig;

            void ParsePropertyDictionary()
            {
                try
                {
                    newConfig.CpphFileDir    = properties[nameof(CpphFileDir).ToLower()];
                    newConfig.HeaderFileDir  = properties[nameof(HeaderFileDir).ToLower()];
                    newConfig.SourceFileDir  = properties[nameof(SourceFileDir).ToLower()];
                    newConfig.VcprojFilePath = properties[nameof(VcprojFilePath).ToLower()];
                    newConfig.Separator      = properties[nameof(Separator).ToLower()];
                }
                catch
                {
                    Console.WriteLine("Failed to parse config file!");
                    newConfig = null;
                }
            }
        }        
    }
}
