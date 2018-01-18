using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Xml.Linq;
using System.Xml;

namespace ParseCppAndH
{
    class Program
    {
        // nesvarbu kuriame<ItemGroup> vietoj yra bet kuris iš ClCompile ar kiti objektai,
        // jie gali būti kartu, arba atskirai - tai vistiek reiškia tą patį

        enum State
        {
            Header,
            Source
        }

        static readonly ConsoleColor DefaultBackgroundColor = Console.BackgroundColor;
        static readonly ConsoleColor DefaultForegroundColor = Console.ForegroundColor;

        static FileSystemWatcher Watcher;
        static ChangedFile FileChanged = new ChangedFile();
        static bool Shutdown = false;
        static bool LogEnabled = true;
        public static Configuration Config { get; private set; }
        

        static void Main(string[] args)
        {
            Configuration.Initialize(Environment.CurrentDirectory + @"\config.txt");
            Config = Configuration.Instance;

            Console.CursorVisible = false;
            Timer timer = new Timer(OnTimerElapsed);
            timer.Change(100, 500);

            Watcher = new FileSystemWatcher(Config.CpphFileDir, "*.cpph");
            Watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
            Watcher.Changed += OnFileContentsChanged;
            Watcher.Created += OnFileCreated;
            Watcher.Deleted += OnFileDeleted;
            Watcher.Renamed += OnFileCreated;
            Watcher.Renamed += OnFileRenamed;
            Watcher.EnableRaisingEvents = true;
            
            while (!Shutdown)
            {
                PrintOnScreenInfo();
                HandleInput();
            }
        }

        private static void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            PrintIfLoggingEnabled("Renamed File " + e.Name);
        }

        private static void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            PrintIfLoggingEnabled("Deleted File " + e.Name);
        }

        private static void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            if (!e.Name.EndsWith(".cpph"))
                return;

            PrintIfLoggingEnabled("Created File " + e.Name);
            CppHFile cppHFile = new CppHFile(e.Name);
            bool createdNewFiles = cppHFile.CreateSourceAndHeaderFiles();

            // updeitint vcproj failą tik tadą jeigu buvo sukurtas naujas failas, kad tas pats failas nesikartotų
            // kelis kartus ir VS necrashintų. (Taip reikia daryt, nes VS išsaugant sukuria laikinus failus,
            // ištrina seną failą ir sukuria naują.
            if(createdNewFiles)
                UpdateVcproj(cppHFile);

            FileChanged.SetNewChangedFile(e.Name);
            cppHFile = null;
        }

        private static void UpdateVcproj(CppHFile cppHFile)
        {
            XDocument xDoc = XDocument.Load(Config.VcprojFilePath);
            XElement root = xDoc.Root;
            XElement[] itemGropupElements = xDoc.Descendants(XName.Get("ItemGroup", root.Name.NamespaceName)).ToArray();

            XName clCompileName = XName.Get("ClCompile", root.Name.NamespaceName);
            XName clIncludeName = XName.Get("ClInclude", root.Name.NamespaceName);
            XName noneName = XName.Get("None", root.Name.NamespaceName);

            XElement sourceToInsert = new XElement(clCompileName, new XAttribute("Include", cppHFile.SourceName));
            XElement headerToInsert = new XElement(clIncludeName, new XAttribute("Include", cppHFile.HeaderName));
            XElement cpphToInsert   = new XElement(noneName,      new XAttribute("Include", cppHFile.CppHName));

            // Sourco ItemGroup
            try
            {
                // įterpia ClCompile į tą ItemGroup kur yra visi sourcai, jeigu toks ItemGroup egzistuoja.
                XElement sourceItemGroup = itemGropupElements.First(x => !x.HasAttributes && x.HasElements
                    && x.Element(clCompileName) != null);

                sourceItemGroup.Add(sourceToInsert);
            }
            catch(InvalidOperationException)
            {
                // jeigu feilina, sukuria naują ItemGroup elementą
                XElement itemGroup = new XElement(XName.Get("ItemGroup", root.Name.NamespaceName));
                itemGroup.Add(sourceToInsert);
                root.Add(itemGroup);
            }

            // Headerio ItemGroup
            try
            {
                // įterpia ClInclude į tą ItemGroup kur yra visi headeriai, jeigu toks ItemGroup egzistuoja.
                XElement headerItemGroup = itemGropupElements.First(x => !x.HasAttributes && x.HasElements
                    && x.Element(clIncludeName) != null);

                headerItemGroup.Add(headerToInsert);
            }
            catch (InvalidOperationException)
            {
                // jeigu feilina, sukuria naują ItemGroup elementą
                XElement itemGroup = new XElement(XName.Get("ItemGroup", root.Name.NamespaceName));
                itemGroup.Add(headerToInsert);
                root.Add(itemGroup);
            }

            // "None" ItemGroup
            try
            {
                // įterpia None į tą ItemGroup kur yra visi cpph ir kiti failai, jeigu toks ItemGroup egzistuoja.
                XElement noneItemGroup = itemGropupElements.First(x => !x.HasAttributes && x.HasElements
                    && x.Element(noneName) != null);

                noneItemGroup.Add(cpphToInsert);
            }
            catch (InvalidOperationException)
            {
                // jeigu feilina, sukuria naują ItemGroup elementą
                XElement itemGroup = new XElement(XName.Get("ItemGroup", root.Name.NamespaceName));
                itemGroup.Add(cpphToInsert);
                root.Add(itemGroup);
            }

            xDoc.Save("testxml.xml"); //test
            xDoc.Save(Config.VcprojFilePath);
        }

        private static void AttemptToQuit()
        {
            Console.WriteLine("Are you sure you want to quit? (y/n)");
            //while (Console.ReadKey(true).Key is var key && (key != ConsoleKey.Y || key != ConsoleKey.N))
            while(true)
            {
                ConsoleKey key = Console.ReadKey(true).Key;
                if (key == ConsoleKey.Y || key == ConsoleKey.T)
                {
                    Shutdown = true;
                    return;
                }
                else if (key == ConsoleKey.N)
                {
                    return;
                }
            }
        }

        private static void HandleInput()
        {
            var key = Console.ReadKey(true);
            switch (key.Key)
            {
                case ConsoleKey.Q:
                    AttemptToQuit();
                    break;
                case ConsoleKey.L:
                    LogEnabled = !LogEnabled;
                    break;
                case ConsoleKey.R:
                    Config.ResetConfig();
                    break;
                case ConsoleKey.N:
                    Console.CursorVisible = true;
                    Console.WriteLine("Enter full path to new configuration file");
                    Config.ChangeConfigFile(Console.ReadLine());
                    Console.ReadKey(true);
                    Console.CursorVisible = false;
                    break;
            }
        }

        private static void PrintOnScreenInfo()
        {
            Console.Clear();

            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.DarkRed;
            Console.WriteLine("Info Info Info");

            Console.ForegroundColor = DefaultForegroundColor;
            Console.BackgroundColor = DefaultBackgroundColor;

            Console.WriteLine("Q to quit");
            Console.WriteLine("L to toggle logging");
            if(LogEnabled)
                Console.WriteLine("Logging enabled");
            else
                Console.WriteLine("Logging disabled");
        }

        private static void OnFileContentsChanged(object sender, FileSystemEventArgs e)
        {
            PrintIfLoggingEnabled(e.ChangeType.ToString());
            FileChanged.SetNewChangedFile(e.Name);
        }
        
        private static void OnTimerElapsed(object sender)
        {
            if (FileChanged)
            {
                PrintIfLoggingEnabled("File Changed (OnTimerElapsed)");
                Parse(FileChanged.FileThatChanged);
                FileChanged.Reset();
            }
        }

        private static void PrintIfLoggingEnabled(string message)
        {
            if(LogEnabled)
            {
                Console.WriteLine(message);
            }
        }

        private static void Parse(string fileName)
        {
            CppHFile cpphFile = new CppHFile(fileName);

            string headerFileName = Configuration.Instance.HeaderFileDir + "\\" + cpphFile.HeaderName;
            string sourceFileName = Configuration.Instance.SourceFileDir + "\\" + cpphFile.SourceName;

            //string separator = "#__SourceStart__";
            string separator = Config.Separator;
            State currentState = State.Header;

            FileStream fileToParse = new FileStream(fileName, FileMode.Open,
                FileAccess.Read, FileShare.ReadWrite);
            FileStream headerStream = new FileStream(headerFileName, File.Exists(headerFileName) ? FileMode.Truncate :
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            FileStream sourceStream = new FileStream(sourceFileName, File.Exists(sourceFileName) ? FileMode.Truncate :
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            StreamReader fileStreamReader = new StreamReader(fileToParse);

            while (!fileStreamReader.EndOfStream)
            {
                string line = fileStreamReader.ReadLine();
                byte[] lineBytes = (line + Environment.NewLine).ToByteArray();
                if (line.Contains(separator))
                {
                    currentState = State.Source;
                    // Įrašo #include "Header.h" source failo pradžioj
                    var includeHeader = $"#include \"{cpphFile.HeaderName}\"".ToByteArray();
                    sourceStream.Write(includeHeader, 0, includeHeader.Length);
                    continue;
                }

                switch (currentState)
                {
                    case State.Header:
                        headerStream.Write(lineBytes, 0, lineBytes.Length);
                        break;
                    case State.Source:
                        sourceStream.Write(lineBytes, 0, lineBytes.Length);
                        break;
                }
            }

            fileToParse.Close();
            headerStream.Close();
            sourceStream.Close();
            fileStreamReader.Close();
        }
    }

    static class __Extensions__
    {
        public static byte[] ToByteArray(this string text)
        {
            return text.ToArray().Select(c => (byte)c).ToArray();
        }

        public static bool IsIgnoredChar(this char c)
        {
            char[] ignoredChars = { '\t' };
            return ignoredChars.Contains(c);
        }
    }
}
