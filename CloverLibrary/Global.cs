using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace CloverLibrary
{
    public class Global
    {
        public const string BASE_URL = "http://a.4cdn.org/";
        public const string BASE_IMAGE_URL = "http://i.4cdn.org/";
        public const string BACK_IMAGE_URL = "http://is2.4chan.org/";
        public const string DEFAULT_IMAGE = "http://s.4cdn.org/image/fp/logo-transparent.png";
        public const string THUMBS_FOLDER_NAME = "thumbs\\";
        private const string WATCH_FILE_PATH = "watchFile.dat";
        private const string LOG_FILE = "Clover.log";

#if DEBUG
        public const string SAVE_DIR = "D:\\Downloads\\PicsAndVids\\FromChan-TESTS\\";
#else
        public const string SAVE_DIR = "D:\\Downloads\\PicsAndVids\\FromChan\\";
#endif
        
        
        public async static Task<List<Tuple<string, string, string>>> GetBoardListAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            List<Tuple<string, string, string>> retVal = new List<Tuple<string, string, string>>();

            JArray boardList =
                (JArray)(
                    (JObject)(
                        await WebTools.HttpRequestParseAsync(BASE_URL + "boards.json", JObject.Parse, cancellationToken)
                    )
                )["boards"];

            foreach (JObject board in boardList)
            {
                retVal.Add(Tuple.Create(
                    board["board"].ToString(),
                    board["title"].ToString(),
                    System.Net.WebUtility.HtmlDecode(board["meta_description"].ToString())));
            }

            return retVal;
        }

        public async static Task<List<ChanThread>> GetBoard(string board, CancellationToken cancellationToken = new CancellationToken())
        {
            List<ChanThread> retVal = new List<ChanThread>();
            string address = BASE_URL + board + "/catalog.json";
            JArray jsonArray = (JArray)await WebTools.HttpRequestParseAsync(address, JArray.Parse, cancellationToken);

            foreach (JObject boardPage in jsonArray)
            {
                foreach (JObject jsonThread in boardPage["threads"])
                {
                    ChanThread thread = new ChanThread(board, (int)jsonThread["no"]);
                    ChanPost op = new ChanPost(jsonThread, thread);
                    thread.AddPost(op);
                    retVal.Add(thread);
                }
            }

            retVal.Sort();
            return retVal;
        }

        public static string MakeSafeFilename(string filename, char replaceChar = '_')
        {
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            {
                filename = filename.Replace(c, replaceChar);
            }
            if(filename.Length > 100)
            {
                filename = filename.Substring(0, 75) + filename.Substring(filename.LastIndexOf('.'));
            }
            return filename;
        }

        private static Mutex watchFileMutex = new Mutex();

        public static void WatchFileAdd(ChanThread thread)
        {
            watchFileMutex.WaitOne();
            string fileContent = System.IO.File.ReadAllText(WATCH_FILE_PATH);
            if (fileContent.Contains(thread.id.ToString()))
            {
                fileContent = Regex.Replace(fileContent, thread.id + "\t" + "\\w\\w", 
                    thread.id.ToString() + "\t" + (thread.AutoRefresh ? "R" : "r") + (thread.SaveImages ? "I" : "i"));
                System.IO.File.WriteAllText(WATCH_FILE_PATH, fileContent);
            }
            else
            {
                using (System.IO.StreamWriter writer = System.IO.File.AppendText(WATCH_FILE_PATH))
                {
                    writer.WriteLine(thread.board + "/" + thread.id + "\t" + 
                        (thread.AutoRefresh ? "R" : "r") + (thread.SaveImages ? "I" : "i") + 
                        "\t" + thread.threadName);
                }
            }
            watchFileMutex.ReleaseMutex();
        }

        public static void WatchFileRemove(ChanThread thread)
        {
            watchFileMutex.WaitOne();
            string fileContent = System.IO.File.ReadAllText(WATCH_FILE_PATH);
            fileContent = Regex.Replace(fileContent, thread.board + "/" + thread.id + "\t" + "\\w\\w.*\\r?\\n", "");
            System.IO.File.WriteAllText(WATCH_FILE_PATH, fileContent);
            watchFileMutex.ReleaseMutex();
        }

        public static List<ChanThread> WatchFileLoad()
        {
            List<ChanThread> retVal = new List<ChanThread>();
            if (System.IO.File.Exists(WATCH_FILE_PATH))
            {
                string fileContent = System.IO.File.ReadAllText(WATCH_FILE_PATH);
                MatchCollection matches = Regex.Matches(fileContent, "(\\w+)/(\\d+)\t(\\w)(\\w)\t([^\r]*)");
                foreach (Match match in matches)
                {
                    System.Diagnostics.Debug.WriteLine("Loading thread: " +
                        match.Groups[1] + "\t" +
                        match.Groups[2] + "\t");

                    try
                    {
                        ChanThread thread = new ChanThread(match.Groups[1].ToString(), int.Parse(match.Groups[2].ToString()), match.Groups[5].ToString())
                        {
                            SaveImages = (match.Groups[4].ToString() == "I"),
                            AutoRefresh = (match.Groups[3].ToString() == "R")
                        };
                        retVal.Add(thread);
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message == "404-NotFound")
                        {
                            continue;
                        }
                        else
                        {
                            System.Diagnostics.Debugger.Break();
                            throw;
                        }
                    }
                } 
            }
            return retVal;
        }

        static Mutex logMutex = new Mutex();
        public static void Log(string message,
            [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
            [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        {
            string logMessage = DateTime.Now.ToString("yyyy-MM-dd-HH:mm:ss:ffff") + "|" + 
                sourceFilePath.Substring(sourceFilePath.LastIndexOf('\\') + 1) + "|" + 
                memberName + "|" + sourceLineNumber + "|" + message + "\n";
            if (System.Diagnostics.Debugger.IsAttached)
            {
                System.Diagnostics.Debug.WriteLine(logMessage); 
            }
            logMutex.WaitOne();
            System.IO.File.AppendAllText(LOG_FILE, logMessage);
            logMutex.ReleaseMutex();
        }
        public static void Log(ChanThread thread, string message,
            [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
            [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        {
            Log(thread.id + ": " + message, memberName, sourceFilePath, sourceLineNumber);
        }
        public static void Log(ChanPost post, string message,
            [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
            [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        {
            System.Diagnostics.StackTrace t = new System.Diagnostics.StackTrace();
            Log(post.thread.id + "-" + post.no + ": " + message, memberName, sourceFilePath, sourceLineNumber);
        }
    }
}
