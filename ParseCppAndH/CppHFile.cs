using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParseCppAndH
{
    class CppHFile
    {
        public string CppHName { get; private set; }
        public string HeaderName
        {
            get => CppHName.Substring(0, CppHName.Length - "cpph".Length) + "h";
        }
        public string SourceName
        {
            get => CppHName.Substring(0, CppHName.Length - "cpph".Length) + "cpp";
        }

        public CppHFile(string name)
        {
            this.CppHName = name;
        }

        public bool CreateSourceAndHeaderFiles()
        {
            var headerPath = Configuration.Instance.HeaderFileDir + @"\" + HeaderName;
            var sourcePath = Configuration.Instance.SourceFileDir + @"\" + SourceName;
            
            if (File.Exists(headerPath))
                return false;
            if (File.Exists(sourcePath))
                return false;

            File.Create(headerPath).Close();
            File.Create(sourcePath).Close();
            return true;

        }
    }
}
