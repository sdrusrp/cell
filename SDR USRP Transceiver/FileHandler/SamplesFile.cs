using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileHandler
{
    public class SamplesFile
    {
        public string FileName { get; set; }
        public Dictionary<string, string> Header { get; set; }
        public List<Complex> Samples { get; set; }

        public SamplesFile()
        {
            Header = new Dictionary<string, string>();
            Samples = new List<Complex>();
        }

        // TODO marshaling to c++ object
    }
}
