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
        public const string SAVE_DIR = "D:\\Downloads\\PicsAndVids\\FromChan-TESTS\\";
        public const string THUMBS_FOLDER_NAME = "thumbs\\";
        private const string WATCH_FILE_PATH = "watchFile.dat";
        private const string LOG_FILE = "Clover.log";

        private static SortedDictionary<int, ChanThread> threadDictionary = new SortedDictionary<int, ChanThread>();
        private static Mutex threadDictionaryMutex = new Mutex();

        public async static Task<List<Tuple<string, string, string>>> getBoardList(CancellationToken cancellationToken = new CancellationToken())
        {
            List<Tuple<string, string, string>> retVal = new List<Tuple<string, string, string>>();

            JArray boardList =
                (JArray)(
                    (JObject)(
                        await WebTools.httpRequestParse(BASE_URL + "boards.json", JObject.Parse, cancellationToken)
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

        public async static Task loadBoard(string board = "b", CancellationToken cancellationToken = new CancellationToken())
        {
            string address = BASE_URL + board + "/catalog.json";
            JArray jsonArray = (JArray)await WebTools.httpRequestParse(address, JArray.Parse);

            foreach (JObject boardPage in jsonArray)
            {
                foreach (JObject jsonThread in boardPage["threads"])
                {
                    if (threadDictionary.ContainsKey((int)jsonThread["no"]) == false)
                    {
                        ChanPost op = new ChanPost(jsonThread);
                        ChanThread thread = new ChanThread(board, op.no);
                        thread.addPost(op);
                        threadDictionaryMutex.WaitOne();
                        threadDictionary.Add((int)jsonThread["no"], thread);
                        threadDictionaryMutex.ReleaseMutex();
                    }
                    else
                    {
                        threadDictionary[(int)jsonThread["no"]].updateThread(jsonThread);
                    }
                }
            }
        }

        public static List<ChanThread> getBoard(string board, CancellationToken cancellationToken = new CancellationToken())
        {
            List<ChanThread> retVal = new List<ChanThread>();

            threadDictionaryMutex.WaitOne();
            foreach (var post in threadDictionary)
            {
                if(cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                if (post.Value.board == board)
                {
                    retVal.Add(post.Value);
                }
            }
            threadDictionaryMutex.ReleaseMutex();
            retVal.Sort();
            return retVal;
        }

        public async static Task loadThread(ChanThread thread, CancellationToken cancellationToken = new CancellationToken())
        {
            string address = BASE_URL + thread.board + "/thread/" + thread.id + ".json";
            JObject jsonObject = (JObject)await WebTools.httpRequestParse(address, JObject.Parse);

            foreach (JObject jsonPost in (JArray)jsonObject["posts"])
            {
                ChanPost post = new ChanPost(jsonPost);
                if (thread.postDictionary.ContainsKey(post.no) == false)
                {
                    try
                    {
                        thread.addPost(post);
                        jsonPost.Remove("last_replies");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debugger.Break();
                        throw;
                    }

                    Regex regex = new Regex("<a href=\"#p(?<reply>\\d+)\" class=\"quotelink\">>>\\d+</a>");
                    MatchCollection matches = regex.Matches(post.com);
                    foreach (Match match in matches)
                    {
                        int replyTo = int.Parse(match.Groups["reply"].ToString());
                        thread.postDictionary[replyTo].addReplyNum(post.no);
                    }
                    post.com = regex.Replace(post.com, ">>$1");
                }
                else
                {
                    thread.postDictionary[post.no].update(post);
                }
            }
        }

        public async static Task<ChanThread> loadThread(string board, int no, bool autoReload = false, bool saveImages = false, string title = "unknown",
            CancellationToken cancellationToken = new CancellationToken())
        {
            ChanThread retVal = new ChanThread(board, no, title);
            threadDictionary.Add(no, retVal);
            await loadThread(retVal, cancellationToken);
            retVal.autoRefresh = autoReload;
            retVal.saveImages = saveImages;
            return retVal;
        }

        public static List<ChanPost> getThread(int threadNumber, string board = "b", CancellationToken cancellationToken = new CancellationToken())
        {
            return threadDictionary[threadNumber].postDictionary.Values.ToList();
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

        public static void watchFileAdd(ChanThread thread)
        {
            watchFileMutex.WaitOne();
            string fileContent = System.IO.File.ReadAllText(WATCH_FILE_PATH);
            if (fileContent.Contains(thread.id.ToString()))
            {
                fileContent = Regex.Replace(fileContent, thread.id + "\t" + "\\w\\w", 
                    thread.id.ToString() + "\t" + (thread.autoRefresh ? "R" : "r") + (thread.saveImages ? "I" : "i"));
                System.IO.File.WriteAllText(WATCH_FILE_PATH, fileContent);
            }
            else
            {
                using (System.IO.StreamWriter writer = System.IO.File.AppendText(WATCH_FILE_PATH))
                {
                    writer.WriteLine(thread.board + "/" + thread.id + "\t" + 
                        (thread.autoRefresh ? "R" : "r") + (thread.saveImages ? "I" : "i") + 
                        "\t" + thread.threadName);
                }
            }
            watchFileMutex.ReleaseMutex();
        }

        public static void watchFileRemove(ChanThread thread)
        {
            watchFileMutex.WaitOne();
            string fileContent = System.IO.File.ReadAllText(WATCH_FILE_PATH);
            fileContent = Regex.Replace(fileContent, thread.board + "/" + thread.id + "\t" + "\\w\\w.*\\r?\\n", "");
            System.IO.File.WriteAllText(WATCH_FILE_PATH, fileContent);
            watchFileMutex.ReleaseMutex();
        }

        public async static Task<List<ChanThread>> watchFileLoad()
        {
            List<ChanThread> retVal = new List<ChanThread>();
            if (System.IO.File.Exists(WATCH_FILE_PATH))
            {
                string fileContent = System.IO.File.ReadAllText(WATCH_FILE_PATH);
                MatchCollection matches = Regex.Matches(fileContent, "(\\w+)/(\\d+)\t(\\w)(\\w)(.*)");
                foreach (Match match in matches)
                {
                    System.Diagnostics.Debug.WriteLine("Loading thread: " +
                        match.Groups[1] + "\t" +
                        match.Groups[2] + "\t");

                    try
                    {
                        await loadThread(match.Groups[1].ToString(), int.Parse(match.Groups[2].ToString()),
                                        match.Groups[3].ToString() == "R", match.Groups[4].ToString() == "I",
                                        match.Groups[5].ToString());
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
            foreach (ChanThread thread in threadDictionary.Values)
            {
                retVal.Add(thread);
            }
            return retVal;
        }

        static Mutex logMutex = new Mutex();
        public static void log(string message,
            [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
            [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        {
            string logMessage = DateTime.Now.ToString("yyyy-MM-dd-HH:mm:ss:ffff") + "|" + 
                sourceFilePath.Substring(sourceFilePath.LastIndexOf('\\') + 1) + "|" + 
                memberName + "|" + sourceLineNumber + "|" + message + "\n";
            System.Diagnostics.Debug.WriteLine(logMessage);
            logMutex.WaitOne();
            System.IO.File.AppendAllText(LOG_FILE, logMessage);
            logMutex.ReleaseMutex();
        }
        public static void log(ChanThread thread, string message,
            [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
            [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        {
            log(thread.id + ": " + message, memberName, sourceFilePath, sourceLineNumber);
        }
        public static void log(ChanPost post, string message,
            [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
            [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        {
            System.Diagnostics.StackTrace t = new System.Diagnostics.StackTrace();
            log(post.thread.id + "-" + post.no + ": " + message, memberName, sourceFilePath, sourceLineNumber);
        }
    }
}
