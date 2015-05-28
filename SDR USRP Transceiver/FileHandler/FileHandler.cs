using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FileHandler
{
    public enum StreamMode
    {
        Write,
        Read
    };

    public enum ReadMode
    {
        Scan,
        OnDemand
    };

    public enum Scanning
    {
        Start,
        Stop
    };

    /// <summary>
    /// This class is responsible for writing to or reading data from the .dat files at the selected directory.
    /// When writing, class object automatically checks if data fulfills saving convention along with throwing and event
    /// if prepared data is not prepared correctly.
    /// When reading, it splits read file to header part and proper content part.
    /// File header and read content is accessible for other classes.
    /// </summary>
    public class FileHandler
    {
        #region public fields

        public List<SamplesFile> Files { get; set; }

        #endregion

        #region private fields

        private readonly string mDirPath;
        private StreamMode mMode;
        private ReadMode mReadMode;
        private StreamReader mReader;
        private StreamWriter mWriter;
        private bool mWriterIsBusy; // TODO
        private bool mReaderIsBusy; // TODO
        private string mFilePattern;
        private readonly Task mScanThread;
        private CancellationTokenSource mTokenSource;
        private CancellationToken mToken;
        private DateTime mLastModification;

        private const string cTxt = "*.txt";
        private const string cDat = "*.dat";
        private const char cHeaderIndicator = '$';
        private const char cSeparator = ';';
        private const char cEqualSign = '=';
        private const string cHeaderKeyPattern = @"^\w+(?==)"; // word before equal sign
        private const string cHeaderValuePattern = @"(?<==).*$"; // word after equal sign
        private const string cSamplesPattern = @"-?\d*\.?[\w-]+\s-?\d*\.?[\w-]+(?=;)"; // real and imag part space-separated with semicolon end
        
        #endregion

        #region methods

        /// <summary>
        /// This is constructor of the FileHandler class object.
        /// It sets folder from where data will be read or write to
        /// and also selects mode of the object which defines if
        /// it is working as reader or writer.
        /// </summary>
        /// <param name="pDirPath">Path where data will be write or read from.</param>
        /// <param name="pMode">Mode of the created object.</param>
        public FileHandler(string pDirPath, StreamMode pMode)
        {
            if (pDirPath != String.Empty)
            {
                if (pDirPath.EndsWith(@"\"))
                {
                    mDirPath = pDirPath;
                }
                else
                {
                    mDirPath = pDirPath + @"\";
                }
            }
            else
            {
                mDirPath = @"Temp\";
            }

            // check if folder exists, create if not
            DirCheck(mDirPath);

            // key objects initialization
            Files = new List<SamplesFile>();
            mMode = pMode;
            mTokenSource = new CancellationTokenSource();
            mToken = mTokenSource.Token;
            mScanThread = new Task(Scan, mToken);

            Debug.WriteLine("FileHandler: Object created. Data dir: {0}, Mode: {1}", mDirPath, mMode);
        }

        /// <summary>
        /// This is finalizer of the FileHandler object. It ends scan thread if started.
        /// </summary>
        ~FileHandler()
        {
            if (mScanThread.Status == TaskStatus.RanToCompletion ||
                mScanThread.Status == TaskStatus.Faulted)
            {
                mScanThread.Dispose();
            }
        }
        
        /// <summary>
        /// This method checks if directory exists at given path.
        /// If not it creates directory at the selected place. 
        /// </summary>
        /// <param name="pDirPath">Path to directory where data will be stored.</param>
        /// <remarks>
        /// Method handles relative and absolute paths.
        /// If directory do not exists, new directory will be created
        /// at the place specified by the path.
        /// </remarks>
        private void DirCheck(string pDirPath)
        {
            if (!Directory.Exists(pDirPath))
            {
                Directory.CreateDirectory(pDirPath);
            }
        }

        /// <summary>
        /// This method changes FileHander work mode.
        /// Class object can work either in reader or writer mode.
        /// </summary>
        /// <param name="pFileHandlerMode">New FileHandler object mode.</param>
        /// <remarks>
        /// If object mode is changed to read mode then read mode is by default set as OnDemand.
        /// </remarks>
        public void ChangeMode(StreamMode pFileHandlerMode)
        {
            ChangeMode(pFileHandlerMode, ReadMode.OnDemand);
        }

        /// <summary>
        /// This method changes FileHander work mode.
        /// Class object can work either in reader or writer mode.
        /// </summary>
        /// <param name="pFileHandlerMode">New FileHandler object mode.</param>
        /// <param name="pReadMode">Preferable read mode.</param>
        /// <remarks>
        /// If object mode is changed to read mode then read mode is by default set as OnDemand.
        /// </remarks>
        public void ChangeMode(StreamMode pFileHandlerMode, ReadMode pReadMode)
        {
            mMode = pFileHandlerMode;

            Debug.WriteLine("FileHandler: Changing mode to {0}", mMode);

            if (mMode == StreamMode.Read)
                ChangeReadMode(pReadMode);
        }

        /// <summary>
        /// This method changes object's read mode.
        /// </summary>
        /// <param name="pNewMode">New read mode.</param>
        public void ChangeReadMode(ReadMode pNewMode)
        {
            if ((mReadMode == ReadMode.Scan) && (pNewMode == ReadMode.OnDemand))
            {
                StopScan();
            }

            mReadMode = pNewMode;
        }

        /// <summary>
        /// This method changes object's read mode.
        /// </summary>
        /// <param name="pNewMode">New read mode.</param>
        /// <param name="pScanStart">If true directory scanning starts automatically.</param>
        /// <param name="pFilePattern">
        /// Pattern written in regular expression syntax
        /// which indicates which files should be read.
        /// </param>
        public void ChangeReadMode(ReadMode pNewMode, Scanning pScanStart, string pFilePattern)
        {
            Debug.WriteLine("FileHandler: Changing read mode to {0}", pNewMode);

            if (pNewMode == ReadMode.OnDemand || pScanStart == Scanning.Stop)
                ChangeReadMode(pNewMode);
            else
            {
                mReadMode = pNewMode;

                StartScan(pFilePattern);
            }
        }

        /// <summary>
        /// This method loads file from the temporary data directory which contains
        /// files with gathered samples. Object containing read data is added to public
        /// samples list.
        /// </summary>
        /// <param name="pFileName">Name of the file to read.</param>
        /// <returns>SamplesFile object containing samples data read.</returns>
        public async Task ReadFileAsync(string pFileName)
        {
            SamplesFile lFile = new SamplesFile();
            bool lHeaderPart = false;

            using (mReader = File.OpenText(pFileName))
            {
                #region performance diagnostics

                Stopwatch lFileRead = new Stopwatch();
                lFileRead.Start();
                string lFileName = new Func<string>(() =>
                {
                    int lLastBackslash = pFileName.LastIndexOf('\\');
                    return pFileName.Substring(lLastBackslash, pFileName.Length - lLastBackslash);
                })();

                Debug.WriteLine("FileHandler: Reading file: " + lFileName);

                #endregion

                string lNextLine;

                while ((lNextLine = await mReader.ReadLineAsync()) != null)
                {
                    #region performance diagnostics

                    Stopwatch lLineRead = new Stopwatch();
                    lLineRead.Start();

                    #endregion

                    if (lNextLine[0] == cHeaderIndicator) // indicates if header part starts or stops
                    {
                        lHeaderPart = !lHeaderPart;
                        continue;
                    }

                    if (lHeaderPart) // fill header info
                    {
                        Match lKey = Regex.Match(lNextLine, cHeaderKeyPattern);
                        Match lValue = Regex.Match(lNextLine, cHeaderValuePattern);
                        lFile.Header.Add(lKey.Value, lValue.Value);
                    }
                    else // fill samples list
                    {
                        #region performance diagnostics

                        Stopwatch lSw = new Stopwatch();
                        lSw.Start();

                        #endregion

                        foreach (Match lMatch in Regex.Matches(lNextLine, cSamplesPattern))
                        {
                            lFile.Samples.Add((Complex)lMatch.Value);
                        }

                        #region performance diagnostics

                        lSw.Stop();
                        Debug.WriteLine("FileHandler: Sample list creation time: {0}", lSw.Elapsed);

                        #endregion
                    }

                    #region performance diagnostics

                    lLineRead.Stop();
                    Debug.WriteLine("FileHandler: Line read time: {0}", lLineRead.Elapsed);

                    #endregion
                }

               #region performance diagnostics

                lFileRead.Stop();
                Debug.WriteLine("FileHandler: File read time: {0}", lFileRead.Elapsed );

                #endregion

            }

            // add file to shared file list
            lock (Files)
            {
                Files.Add(lFile);
            }
        }

        /// <summary>
        /// This method writes asynchronously selected file to hard disk. 
        /// </summary>
        /// <param name="pFile">Object of the SamplesFile class which contains header, samples and file name.</param>
        public void WriteFileAsync(SamplesFile pFile)
        {
            WriteFileAsync(pFile.Header, pFile.Samples, pFile.FileName);
        }

        /// <summary>
        /// This method writes asynchronously selected file to hard disk. 
        /// </summary>
        /// <param name="pHeader">File header.</param>
        /// <param name="pSamples">Signal samples which will be stored in file.</param>
        /// <param name="pFileName">File name.</param>
        public async void WriteFileAsync(Dictionary<string, string> pHeader, List<Complex> pSamples, string pFileName)
        {
            using (mWriter = new StreamWriter(mDirPath + pFileName))
            {
                // save header
                mWriter.WriteLine(cHeaderIndicator);
                foreach (KeyValuePair<string, string> lLine in pHeader)
                {
                    mWriter.WriteLine(lLine.Key + cEqualSign + lLine.Value);
                }
                mWriter.WriteLine(cHeaderIndicator);

                // save samples
                foreach (Complex lSample in pSamples)
                {
                    await mWriter.WriteLineAsync(lSample.ToString() + cSeparator);
                }

            }
        }

        /// <summary>
        /// This method scans directory looking for files to read.
        /// Firstly method reads files which has the oldest modification date.
        /// After reading file method bubbles event with SampleFile object
        /// containing read data.
        /// After read files are removed from the directory. 
        /// </summary>
        private void Scan()
        {
            // if cancellation request was sent before loop started
            if (mToken.IsCancellationRequested)
            {
                Debug.WriteLine("FileHandler: Scan task cancellation requested before scanning has started");
                return;
            }

            Queue<FileInfo> lSampleFiles = GetFilesInDirectory();
            Debug.WriteLine("FileHandler: Starting file scanning process");

            // scan directory for new files
            while (!mToken.IsCancellationRequested)
            {
                if (lSampleFiles.Count != 0)
                {
                    // look for the oldest file
                    FileInfo lSamples = lSampleFiles.Peek();

                    try
                    {
                        // check if it fulfills file regex pattern
                        if (Regex.IsMatch(lSamples.Name, mFilePattern))
                        {
                            // check if file is locked by other process
                            if (!IsFileLocked(lSamples))
                            {
                                // read file and send result in the event
                                ReadFileAsync(lSamples.FullName).Wait();
                            }
                        }
                    }
                    catch (IOException e)
                    {
                        Debug.WriteLine("FileHandler: IOException occured. Message: " + e.Message);
                        throw;
                    }
                    finally
                    {
                        // clean up sample
                        // lSamples.Delete();
                        lSampleFiles.Dequeue();
                    }
                }
                // update samplefiles list
                UpdateFilesList(lSampleFiles);
            }

            // if task cancellation requested clean up used resources
            if (mToken.IsCancellationRequested)
            {
                Debug.WriteLine("FileHandler: Scan task cancellation requested. Cleaning up resources");
            }
        }

        /// <summary>
        /// This method gets files from directory and saves that infomration to the Queue object.
        /// </summary>
        /// <returns>Queue where information about files in directory is stored</returns>
        private Queue<FileInfo> GetFilesInDirectory()
        {
            Queue<FileInfo> lFileData = new Queue<FileInfo>();

            string[] lFilePaths = Directory.GetFiles(mDirPath, cTxt);

            foreach (string lFileName in lFilePaths)
            {
                if (File.Exists(lFileName))
                    lFileData.Enqueue(new FileInfo(lFileName));
            }

            if (lFileData.Count != 0)
            {
                mLastModification = lFileData.Last().LastWriteTime;
            }
            else
            {
                mLastModification = DateTime.Now;
            }

            Debug.WriteLine("FileHandler: Files in directory " + mDirPath + " obtained. {0} files founded", lFileData.Count);
            return lFileData;
        }

        /// <summary>
        /// This method updates files list which are stored in data directory.
        /// If new file exists in directory and it is not contained in the file queue
        /// then it is enqueued to the end of the queue.
        /// </summary>
        /// <param name="pFileList">Queue containing gathered files to read</param>
        private void UpdateFilesList(Queue<FileInfo> pFileList)
        {
            Debug.WriteLine("FileHandler: Updating files to read list");

            // get last file modification date
            if(pFileList.Count != 0 )
                mLastModification = pFileList.Last().LastWriteTime;

            // add all files modified after given modification date
            IEnumerable<FileInfo> lNewFiles = Directory.GetFiles(mDirPath, cTxt)
                                              .Select(x => new FileInfo(x))
                                              .Where(x => x.LastWriteTime > mLastModification);

            foreach (FileInfo lFile in lNewFiles)
            {
                pFileList.Enqueue(lFile);
            }

            Debug.WriteLine("FileHandler: {0} new files found", lNewFiles.Count());
        }

        /// <summary>
        /// This method checks if the selected file is not blocked by another process.
        /// </summary>
        /// <param name="pFile">File to check</param>
        /// <returns>True if file is locked by another process and false otherwise</returns>
        private bool IsFileLocked(FileInfo pFile)
        {
            try
            {
                using (File.Open(pFile.FullName, FileMode.Open)) { }
            }
            catch (IOException)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// This method starts scanning directory for matching files to read.
        /// </summary>
        public void StartScan(string pFilePattern)
        {
            if (mScanThread.Status == TaskStatus.Running)
            {
                throw new FileHandlerException("Task is already running");
            }

            if ((mReadMode == ReadMode.Scan) &&
                (mScanThread.Status == TaskStatus.RanToCompletion || mScanThread.Status == TaskStatus.Created))
            {
                Debug.WriteLine("FileHandler: Starting scanning for files matching pattern: " + pFilePattern);

                mFilePattern = pFilePattern;
                mWriterIsBusy = true;

                // start scanning and reading files
                mScanThread.Start();
                Thread.Sleep(10); // some startup time

                Debug.WriteLine("FileHandler: Scan task has started");
            }
            else
            {
                Debug.WriteLine("FileHandler: Cannot start scanning. Read mode set is {0} or Task malfunction, Task status: {1}", mReadMode, mScanThread.Status);
                throw new FileHandlerException("Change object read mode to Scan to use this option or Task malfunction occured");
            }
        }

        /// <summary>
        /// This method stops scanning thread on user demand.
        /// </summary>
        public void StopScan()
        {
            if ((mReadMode == ReadMode.Scan) && (mScanThread.Status == TaskStatus.Running))
            {
                Debug.WriteLine("FileHandler: Stopping scanning task");

                // cancel thread
                mTokenSource.Cancel();

                try
                {
                    Debug.WriteLine("FileHandler: Sending cancellation request to the running task");
                    mScanThread.Wait();
                }
                finally
                {
                    mScanThread.Dispose();
                    mWriterIsBusy = false;

                    Debug.WriteLine("FileHandler: Task cancelled and succesfully disposed");
                }
            }
            else
            {
                Debug.WriteLine("FileHandler: Cannot stop task. It is already stopped.");
                throw new FileHandlerException("Cannot stop task. It is already stopped");
            }
        }

        /// <summary>
        /// This method reads data from shared memory between threads.
        /// It should be invoked when another process throws event that
        /// shared memory is ready to read.
        /// </summary>
        /// <param name="pMemoryAddress">Address of the shared memory</param>
        unsafe public void ReadSharedMemory(int* pMemoryAddress)
        {
            // TODO future feature
        }

        #endregion
    }
}
