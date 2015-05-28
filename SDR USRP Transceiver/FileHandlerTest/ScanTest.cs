using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FileHandlerTest
{
    [TestClass]
    public class ScanTest
    {
        // regex patterns to read text files in scan mode
        private const string cPatternFileHuge = @"^RX_[0-9]{3}_[0-9]+\.txt";
        private const string cPatternFileSmall = @"^RX_[0-9]\.[0-9]{2}_\.txt";

        [TestMethod]
        public void ScanLargeFiles()
        {
        }
    }
}
