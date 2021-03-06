﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileHandler
{
    /// <summary>
    /// This class implements exception which is thrown when error occurs in FileHandler class object.
    /// </summary>
    class FileHandlerException : Exception
    {
        /// <summary>
        /// Constructor of FileHandlerException.
        /// </summary>
        /// <param name="pMessage">Message contained in exception indicating error cause.</param>
        public FileHandlerException(string pMessage)
            : base(pMessage)
        { }
    }
}
