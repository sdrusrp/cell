using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FileHandler;

namespace FileHandlerTests
{
    [TestClass]
    public class ScanTest
    {
        [TestMethod]
        public void ScanLargeFilesTest()
        {
            string lPattern = @"^RX_[0-9]{3}_[0-9]+\.txt";
        }

        [TestMethod]
        public void ScanSmallFilesTests()
        {
            string lPattern = @"^RX_[0-9]\.[0-9]{2}\.txt";
        }
    }
}
