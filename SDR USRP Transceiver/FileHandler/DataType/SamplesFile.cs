using System.Collections.Generic;

namespace UsrpIO.DataType
{
    /// <summary>
    /// 
    /// </summary>
    public class SamplesFile
    {
        public string FileName { get; set; }
        public Dictionary<string, string> Header { get; set; }
        public List<Complex> Samples { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public SamplesFile()
        {
            Header = new Dictionary<string, string>();
            Samples = new List<Complex>();
        }

        // TODO marshaling to and from sample_file c++ object
    }
}
