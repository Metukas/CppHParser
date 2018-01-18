using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParseCppAndH
{
    class ChangedFile
    {
        public bool NewChange { get; set; }
        public string FileThatChanged { get; set; }

        public ChangedFile()
        {
            Reset();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name">The name of file (NOT a full path)</param>
        internal void SetNewChangedFile(string name)
        {
            FileThatChanged = name;
            NewChange = true;
        }

        internal void Reset()
        {
            FileThatChanged = "";
            NewChange = false;
        }

        public static implicit operator bool(ChangedFile cf) => cf.NewChange;
    }
}
