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

        public SortedDictionary<int, ChanPost> postDictionary = new SortedDictionary<int, ChanPost>();

        public Thread autoRefreshThread;

        private bool _autoRefresh;
        public bool saveImages;
        public bool autoRefresh
        {
            get
            {
                return _autoRefresh;
            }
            set
            {
                if (value == true &&
                    autoRefreshThread.ThreadState != ThreadState.Running &&
                    autoRefreshThread.ThreadState != ThreadState.Stopped &&
                    autoRefreshThread.ThreadState != ThreadState.Aborted)
                    autoRefreshThread.Start();

                _autoRefresh = value;
            }
        }

        public event EventHandler<UpdateThreadEventArgs> raiseUpdateThreadEvent;
        protected virtual void OnRaiseUpdateThreadEvent(UpdateThreadEventArgs e)
        {
            EventHandler<UpdateThreadEventArgs> handler = raiseUpdateThreadEvent;
            handler(this, e);
        }

        public ChanThread(string board, int id)
        {
            this.board = board;
            this.id = id;
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
                        if (System.Diagnostics.Debugger.IsAttached)
                        {
                            await Task.Delay(5000);
                        }
                        else
                        {
                            await Task.Delay(30000);
                        }

                    }
                }
                catch (ThreadAbortException ex)
                {

                }
            });
        }

        public int CompareTo(ChanThread other)
        {
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

        public async Task saveThread(CancellationToken cancellationToken = new CancellationToken())
        {
            string dir = "D:\\Downloads\\PicsAndVids\\FromChan\\" +
                board + "-" + id + "-" + dirName + "\\";

            if (System.IO.Directory.Exists(dir) == false)
            {
                System.IO.Directory.CreateDirectory(dir);
            }

            if (saveImages)
            {
                foreach (ChanPost post in postDictionary.Values)
                {
                    await post.saveImage(dir, cancellationToken);
                } 
            }

            System.IO.File.WriteAllText(dir + "thread.json", json.ToString());
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
        public List<ChanPost> postList;
        public UpdateThreadEventArgs(UpdateEvent s)
        {
            _updateEvent = s;
        }

        public UpdateThreadEventArgs(UpdateEvent s, List<ChanPost> postList)
        {
            _updateEvent = s;
            this.postList = postList;
        }

        public UpdateEvent updateEvent
        {
            get { return _updateEvent; }
            set { _updateEvent = value; }
        }
    }
}
