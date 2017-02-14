using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json.Linq;

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

        private bool _autoRefresh;
        private bool _saveImages;
        public bool autoRefresh
        {
            get
            {
                return _autoRefresh;
            }
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
                Global.watchFileAdd(this);
            }
        }

        public bool saveImages
        {
            get
            {
                return _saveImages;
            }
            set
            {
                _saveImages = value;
                Global.watchFileAdd(this);
            }
        }

        public event EventHandler<UpdateThreadEventArgs> raiseUpdateThreadEvent;
        protected virtual void OnRaiseUpdateThreadEvent(UpdateThreadEventArgs e)
        {
            raiseUpdateThreadEvent?.Invoke(this, e);
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
                    await saveThread();
                    while (true)
                    {
                        if (_autoRefresh)
                        {
                            int oldReplyCount = postDictionary.Count;
                            try
                            {
                                await Global.loadThread(this);
                            }
                            catch (Exception e)
                            {
                                if (e.Message == "404-NotFound")
                                {
                                    System.Diagnostics.Debug.WriteLine("Thread " + id + " has 404'd");
                                    OnRaiseUpdateThreadEvent(new UpdateThreadEventArgs(UpdateThreadEventArgs.UpdateEvent.thread404));
                                    break;
                                }
                                else
                                    throw;
                            }
                            if (postDictionary.Count > oldReplyCount)
                            {
                                List<ChanPost> postList = postDictionary.Values.Skip(oldReplyCount).ToList();

                                OnRaiseUpdateThreadEvent(new UpdateThreadEventArgs(UpdateThreadEventArgs.UpdateEvent.newPosts, postList));
                                await saveThread();
                            }
                        }
                        refreshRate = calculateRefreshRate();
                        await Task.Delay(refreshRate);

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

        public void addPost(ChanPost post)
        {
            postDictionary.Add(post.no, post);
            ((JArray)json["posts"]).Add(post.json);
            post.thread = this;
            if(post.resto == 0)
            {
                threadName = post.sub != "" ? post.sub : 
                    post.com.Substring(0, (post.com.Length > 50 ? 50 : post.com.Length)).Replace("\n", " ");
                dirName = post.semantic_url;
            }
        }

        public void updateThread(JObject jsonThread)
        {
            //System.Diagnostics.Debugger.Break();
        }

        public string getDir()
        {
            return Global.SAVE_DIR + board + "-" + id + "-" + dirName + "\\";
        }

        public async Task saveThread(CancellationToken cancellationToken = new CancellationToken())
        {
            string dir = getDir();

            if (System.IO.Directory.Exists(dir) == false)
            {
                System.IO.Directory.CreateDirectory(dir);
            }
            if (System.IO.Directory.Exists(dir + Global.THUMBS_FOLDER_NAME) == false)
            {
                System.IO.Directory.CreateDirectory(dir + Global.THUMBS_FOLDER_NAME);
            }

            foreach (ChanPost post in postDictionary.Values)
            {
                await post.saveThumb(dir, cancellationToken);
                if (saveImages)
                {
                    await post.loadImage();
                    await post.saveImage(dir, cancellationToken);
                }
            }
            
            System.IO.File.WriteAllText(dir + "thread.json", json.ToString());
        }

        public int calculateRefreshRate()
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

        public void on404()
        {
            autoRefresh = false;
            saveImages = false;
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

        public UpdateEvent updateEvent
        {
            get { return _updateEvent; }
            set { _updateEvent = value; }
        }
        public object context
        {
            get { return _context; }
            set { _context = value; }
        }
    }
}
