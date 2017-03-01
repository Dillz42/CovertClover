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
    public class ChanThread : IComparable<ChanThread>
    {
        public string board;
        public int id;
        public string threadName;
        public string dirName;
        public JObject json = new JObject();
        public int refreshRate = 120000; //reload every 2 minutes

        public SortedDictionary<int, ChanPost> postDictionary = new SortedDictionary<int, ChanPost>();

        public Thread autoRefreshThread;
        private CancellationTokenSource threadTokenSource = new CancellationTokenSource();

        private bool _autoRefresh;
        private bool _saveImages;
        public bool AutoRefresh
        {
            get => _autoRefresh;
            set
            {
                _autoRefresh = value;

                if (value == true)
                {
                    if (autoRefreshThread.ThreadState != ThreadState.Running &&
                        autoRefreshThread.ThreadState != ThreadState.Stopped &&
                        autoRefreshThread.ThreadState != ThreadState.Aborted)
                    {
                        autoRefreshThread.Start();
                    }
                    
                }
                Global.WatchFileAdd(this);
            }
        }

        public bool SaveImages
        {
            get => _saveImages;
            set
            {
                _saveImages = value;
                Global.WatchFileAdd(this);
                threadTokenSource.Cancel();
            }
        }

        public event EventHandler<UpdateThreadEventArgs> RaiseUpdateThreadEvent;
        protected virtual void OnRaiseUpdateThreadEvent(UpdateThreadEventArgs e)
        {
            RaiseUpdateThreadEvent?.Invoke(this, e);
        }

        public ChanThread(string board, int id, string title = "")
        {
            this.board = board;
            this.id = id;
            this.threadName = title;
            json.Add("posts", new JArray());

            autoRefreshThread = new Thread(async () =>
            {
                try
                {
                    await SaveThreadAsync();
                    while (true)
                    {
                        if (_autoRefresh)
                        {
                            int oldReplyCount = postDictionary.Count;
                            try
                            {
                                await UpdateThreadAsync();
                            }
                            catch (Exception e)
                            {
                                if (e.Message == "404-NotFound")
                                {
                                    Global.Log(this + "Thread has 404'd");
                                    OnRaiseUpdateThreadEvent(new UpdateThreadEventArgs(UpdateThreadEventArgs.UpdateEvent.thread404));
                                    break;
                                }
                                else
                                    throw;
                            }
                            if (postDictionary.Count > oldReplyCount)
                            {
                                Global.Log(this, "New posts found! Updating UI and saving thread");
                                List<ChanPost> postList = postDictionary.Values.Skip(oldReplyCount).ToList();
                                OnRaiseUpdateThreadEvent(new UpdateThreadEventArgs(UpdateThreadEventArgs.UpdateEvent.newPosts, postList));
                                await SaveThreadAsync();
                            }
                        }
                        refreshRate = CalculateRefreshRate();
                        Global.Log(this, "Sleeping watch thread for " + refreshRate);

                        try
                        {
                            await Task.Delay(refreshRate, threadTokenSource.Token);
                        } catch (TaskCanceledException){}
                        threadTokenSource = new CancellationTokenSource();
                    }
                }
                catch (ThreadAbortException ex)
                {
                    OnRaiseUpdateThreadEvent(new UpdateThreadEventArgs(UpdateThreadEventArgs.UpdateEvent.unknown, ex));
                }
            });
        }

        public int CompareTo(ChanThread other)
        {
            if (postDictionary.Count == 0 || other.postDictionary.Count == 0)
            {
                return id - other.id;
            }
            if (other.postDictionary.First().Value.sticky == postDictionary.First().Value.sticky)
            {
                return (int)(other.postDictionary.First().Value.last_modified - postDictionary.First().Value.last_modified);
            }
            return other.postDictionary.First().Value.sticky - postDictionary.First().Value.sticky;
        }

        public void AddPost(ChanPost post)
        {
            post.thread = this;
            Global.Log(post, "Adding post to thread");
            postDictionary.Add(post.no, post);
            ((JArray)json["posts"]).Add(post.json);
            if(post.resto == 0)
            {
                threadName = post.sub != "" ? post.sub : 
                    post.com.Substring(0, (post.com.Length > 50 ? 50 : post.com.Length)).Replace("\n", " ");
                dirName = post.semantic_url;
            }
        }

        public void LoadThread(CancellationToken cancellationToken = new CancellationToken())
        {
            Task t = LoadThreadAsync(cancellationToken);
        }
        public async Task LoadThreadAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            LoadThreadFile();
            await UpdateThreadAsync();
        }

        public void LoadThreadFile(CancellationToken cancellationToken = new CancellationToken())
        {
            string[] folders = System.IO.Directory.GetDirectories(Global.SAVE_DIR);
            string threadDir = folders.Where(s => s.Contains(id.ToString()) == true).Last();

            if (System.IO.File.Exists(threadDir + "\\thread.json"))
            {
                string fileContent = System.IO.File.ReadAllText(threadDir + "\\thread.json");
                JArray jsonArray = (JArray)JObject.Parse(fileContent)["posts"];
                AddPostsFromJsonArray(jsonArray);
            }
        }

        public void UpdateThread() { Task t = UpdateThreadAsync(); }
        public async Task UpdateThreadAsync()
        {
            try
            {
                string address = Global.BASE_URL + board + "/thread/" + id + ".json";
                JObject jsonObject = (JObject)await WebTools.HttpRequestParseAsync(address, JObject.Parse);
                AddPostsFromJsonArray((JArray)jsonObject["posts"]);
            }
            catch (Exception ex)
            {
                if (ex.Message == "404-NotFound" && postDictionary.Count != 0)
                {
                    On404();
                }
                else
                {
                    throw;
                }
            }
        }

        public void AddPostsFromJsonArray(JArray jsonArray)
        {
            foreach (JObject jsonPost in jsonArray)
            {
                ChanPost post = new ChanPost(jsonPost, this);
                if (postDictionary.ContainsKey(post.no) == false)
                {
                    try
                    {
                        AddPost(post);
                        jsonPost.Remove("last_replies");
                    }
                    catch (Exception)
                    {
                        System.Diagnostics.Debugger.Break();
                        throw;
                    }

                    Regex regex = new Regex("<a href=\"#p(?<reply>\\d+)\" class=\"quotelink\">>>\\d+</a>");
                    MatchCollection matches = regex.Matches(post.com);
                    foreach (Match match in matches)
                    {
                        int replyTo = int.Parse(match.Groups["reply"].ToString());
                        postDictionary[replyTo].AddReplyNum(post.no);
                    }
                    post.com = regex.Replace(post.com, ">>$1");
                }
                else
                {
                    postDictionary[post.no].Update(post);
                }
            }
        }

        public string GetDir()
        {
            return Global.SAVE_DIR + board + "-" + id + "-" + dirName + "\\";
        }

        public async Task SaveThreadAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            string dir = GetDir();

            if (System.IO.Directory.Exists(dir) == false)
            {
                System.IO.Directory.CreateDirectory(dir);
            }
            if (System.IO.Directory.Exists(dir + Global.THUMBS_FOLDER_NAME) == false)
            {
                System.IO.Directory.CreateDirectory(dir + Global.THUMBS_FOLDER_NAME);
            }

            System.IO.File.WriteAllText(dir + "thread.json", json.ToString());
            foreach (ChanPost post in postDictionary.Values)
            {
                await post.SaveThumbAsync(dir, cancellationToken);
                if (SaveImages)
                {
                    await post.LoadImageAsync();
                    await post.SaveImageAsync(dir, cancellationToken);
                }
            }
        }

        public async Task MemoryLoadAsync()
        {
            foreach (ChanPost post in postDictionary.Values)
            {
                await post.LoadThumbAsync();
            }
        }

        public void MemoryClear()
        {
            Global.Log(this, "Clearing thread from memory");
            foreach (ChanPost post in postDictionary.Values)
            {
                post.ClearImageData();
                post.ClearThumbData();
            }
            GC.Collect(0, GCCollectionMode.Forced);
        }

        public int CalculateRefreshRate()
        {
            int refreshRate = 0;
            if (postDictionary.Count == 1)
            {
                refreshRate = (this.refreshRate * 11)/10;
            }
            else
            {
                const int postsToCount = 10;
                for (int i = 0; i < (postDictionary.Count-1 < postsToCount ? postDictionary.Count-1 : postsToCount); i++)
                {
                    int timeDiff = postDictionary.Values.Reverse().ToList()[postDictionary.Count - (i + 1) - 1].time -
                        postDictionary.Values.Reverse().ToList()[postDictionary.Count - i - 1].time;

                    refreshRate += timeDiff * 1000;
                }
                refreshRate /= (postDictionary.Count - 1 < postsToCount ? postDictionary.Count - 1 : postsToCount);
            }
            return (refreshRate > 1800000? 1800000 : refreshRate);
        }

        public void On404()
        {
            AutoRefresh = false;
            SaveImages = false;
            autoRefreshThread.Abort();
        }
    }

    public class UpdateThreadEventArgs : EventArgs
    {
        public enum UpdateEvent
        {
            unknown = 0,
            thread404 = 1,
            newPosts = 2
        }

        private UpdateEvent _updateEvent;
        private object _context;
        public List<ChanPost> postList;
        public UpdateThreadEventArgs(UpdateEvent s, object context = null)
        {
            _updateEvent = s;
            _context = context;
        }

        public UpdateThreadEventArgs(UpdateEvent s, List<ChanPost> postList)
        {
            _updateEvent = s;
            this.postList = postList;
            _context = null;
        }

        public UpdateEvent Update_Event
        {
            get { return _updateEvent; }
            set { _updateEvent = value; }
        }
        public object Context
        {
            get { return _context; }
            set { _context = value; }
        }
    }
}
