using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace CloverLibrary
{
    public class ChanPost : IComparable<ChanPost>
    {
        public JObject json;
        public string board;
        
        public int no;
        public int resto;
        public int sticky;
        public int closed;
        public int archived;
        public int archived_on;
        public string now;
        public int time;
        public string name;
        public string trip;
        public string id;
        public string capcode;
        public string country;
        public string country_name;
        public string sub;
        public string com;
        public long tim;
        public string filename;
        public string ext;
        public int fsize;
        public string md5;
        public int w;
        public int h;
        public int tn_w;
        public int tn_h;
        public int filedeleted;
        public int spoiler;
        public int custom_spoiler;
        public int omitted_posts;
        public int omitted_images;
        public int replies;
        public int images;
        public int bumplimit;
        public int imagelimit;
        //string[] capcode_replies;
        public int last_modified;
        public string tag;
        public string semantic_url;

        public List<int> replyList = new List<int>();
        public void addReplyNum(int reply)
        {
            if (replyList.Contains(reply) == false)
            {
                replyList.Add(reply);
            }
        }
        public SortedDictionary<int, ChanPost> replyPosts = new SortedDictionary<int, ChanPost>();

        public string thumbUrl;
        public string imageUrl;
        public byte[] imageData = null;
        public byte[] thumbData = null;
        private bool _autoRefresh;
        public bool autoRefresh {
            get
            {
                return _autoRefresh;
            }
            set
            {
                if (autoRefreshThread.ThreadState != ThreadState.Running &&
                    autoRefreshThread.ThreadState != ThreadState.Stopped &&
                    autoRefreshThread.ThreadState != ThreadState.Aborted)
                    autoRefreshThread.Start();

                _autoRefresh = value;
            }
        }
        public bool _saveImages;
        public bool saveImages
        {
            get
            {
                return _saveImages;
            }
            set
            {
                if(resto == 0)
                {
                    foreach (ChanPost replyPost in replyPosts.Values)
                    {
                        replyPost.saveImages = value;
                    }
                }
                _saveImages = value;
            }
        }

        public event EventHandler<UpdateThreadEventArgs> raiseUpdateThreadEvent;
        protected virtual void OnRaiseUpdateThreadEvent(UpdateThreadEventArgs e)
        {
            EventHandler<UpdateThreadEventArgs> handler = raiseUpdateThreadEvent;
            if(handler != null)
            {
                handler(this, e);
            }
        }

        public Thread autoRefreshThread;

        public ChanPost()
        {
            
        }
        public ChanPost(JObject jsonObject, string board)
        {
            this.board = board;

            no = (int)(jsonObject["no"] != null ? jsonObject["no"] : 0);
            resto = (int)(jsonObject["resto"] != null ? jsonObject["resto"] : 0);
            sticky = (int)(jsonObject["sticky"] != null ? jsonObject["sticky"] : 0);
            closed = (int)(jsonObject["closed"] != null ? jsonObject["closed"] : 0);
            archived = (int)(jsonObject["archived"] != null ? jsonObject["archived"] : 0);
            archived_on = (int)(jsonObject["archived_on"] != null ? jsonObject["archived_on"] : 0);
            now = (string)(jsonObject["now"] != null ? jsonObject["now"] : "");
            time = (int)(jsonObject["time"] != null ? jsonObject["time"] : 0);
            name = (string)(jsonObject["name"] != null ? jsonObject["name"] : "");
            trip = (string)(jsonObject["trip"] != null ? jsonObject["trip"] : "");
            id = (string)(jsonObject["id"] != null ? jsonObject["id"] : "");
            capcode = (string)(jsonObject["capcode"] != null ? jsonObject["capcode"] : "");
            country = (string)(jsonObject["country"] != null ? jsonObject["country"] : "");
            country_name = (string)(jsonObject["country_name"] != null ? jsonObject["country_name"] : "");
            sub = (string)(jsonObject["sub"] != null ? jsonObject["sub"] : "");
            com = (string)(jsonObject["com"] != null ? jsonObject["com"] : "");
            tim = (long)(jsonObject["tim"] != null ? jsonObject["tim"] : 0);
            filename = (string)(jsonObject["filename"] != null ? jsonObject["filename"] : "");
            ext = (string)(jsonObject["ext"] != null ? jsonObject["ext"] : "");
            fsize = (int)(jsonObject["fsize"] != null ? jsonObject["fsize"] : 0);
            md5 = (string)(jsonObject["md5"] != null ? jsonObject["md5"] : "");
            w = (int)(jsonObject["w"] != null ? jsonObject["w"] : 0);
            h = (int)(jsonObject["h"] != null ? jsonObject["h"] : 0);
            tn_w = (int)(jsonObject["tn_w"] != null ? jsonObject["tn_w"] : 0);
            tn_h = (int)(jsonObject["tn_h"] != null ? jsonObject["tn_h"] : 0);
            filedeleted = (int)(jsonObject["filedeleted"] != null ? jsonObject["filedeleted"] : 0);
            spoiler = (int)(jsonObject["spoiler"] != null ? jsonObject["spoiler"] : 0);
            custom_spoiler = (int)(jsonObject["custom_spoiler"] != null ? jsonObject["custom_spoiler"] : 0);
            omitted_posts = (int)(jsonObject["omitted_posts"] != null ? jsonObject["omitted_posts"] : 0);
            omitted_images = (int)(jsonObject["omitted_images"] != null ? jsonObject["omitted_images"] : 0);
            replies = (int)(jsonObject["replies"] != null ? jsonObject["replies"] : 0);
            images = (int)(jsonObject["images"] != null ? jsonObject["images"] : 0);
            bumplimit = (int)(jsonObject["bumplimit"] != null ? jsonObject["bumplimit"] : 0);
            imagelimit = (int)(jsonObject["imagelimit"] != null ? jsonObject["imagelimit"] : 0);
            //string[] capcode_replies;;
            last_modified = (int)(jsonObject["last_modified"] != null ? jsonObject["last_modified"] : 0);
            tag = (string)(jsonObject["tag"] != null ? jsonObject["tag"] : "");
            semantic_url = (string)(jsonObject["semantic_url"] != null ? jsonObject["semantic_url"] : "");
            
            if (ext != "")
            {
                thumbUrl = (Global.BASE_IMAGE_URL + board + "/" + tim + "s.jpg");
                imageUrl = Global.BASE_IMAGE_URL + board + "/" + tim + ext;
            }
            else
            {
                thumbUrl = "http://s.4cdn.org/image/fp/logo-transparent.png";
                imageUrl = "http://s.4cdn.org/image/fp/logo-transparent.png";
            }

            com = com.Replace("<br>", "\n");
            com = System.Net.WebUtility.HtmlDecode(com);

            if (resto == 0)
            {
                json = new JObject();
                json.Add("posts", new JArray());

                autoRefreshThread = new Thread(async () =>
                    {
                        try
                        {
                            while (true)
                            {
                                if (_autoRefresh)
                                {
                                    System.Diagnostics.Debug.WriteLine("Loading thread " + no);
                                    int oldReplyCount = replyPosts.Count;
                                    try
                                    {
                                        await Global.loadThread(this);
                                    }
                                    catch (Exception e)
                                    {
                                        if (e.Message == "404-NotFound")
                                        {
                                            System.Diagnostics.Debug.WriteLine("Thread " + no + " has 404'd");
                                            OnRaiseUpdateThreadEvent(new UpdateThreadEventArgs(UpdateThreadEventArgs.UpdateEvent.thread404));
                                            break;
                                        }
                                        else
                                            throw;
                                    }
                                    if (replyPosts.Count > oldReplyCount)
                                    {
                                        List<ChanPost> postList = replyPosts.Values.Skip(oldReplyCount).ToList<ChanPost>();

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
        }

        public void update(ChanPost newData)
        {
            filedeleted = newData.filedeleted;
        }

        public async Task saveThread(CancellationToken cancellationToken = new CancellationToken())
        {
            string dir = "D:\\Downloads\\PicsAndVids\\FromChan\\" + 
                board + "-" + (resto == 0 ? no : resto) + "-" + semantic_url + "\\";

            if (semantic_url != "" && System.IO.Directory.Exists(dir) == false)
            {
                System.IO.Directory.CreateDirectory(dir);
            }

            if (resto == 0)
            {
                foreach (ChanPost replyPost in replyPosts.Values)
                {
                    replyPost.saveImages = saveImages;
                    replyPost.semantic_url = semantic_url;
                    await replyPost.saveThread();
                }
                System.IO.File.WriteAllText(dir + "thread.json", json.ToString());
            }

            if (saveImages && ext != "")
            {
                string fullFileName = dir + Global.MakeSafeFilename(tim + "-" + filename + ext);
                await loadImage(cancellationToken);
                if (System.IO.File.Exists(fullFileName) == false)
                {
                    System.IO.File.WriteAllBytes(fullFileName, imageData);
                } 
            }
        }

        public async Task loadImage(CancellationToken cancellationToken = new CancellationToken())
        {
            if (imageData == null)
            {
                //System.Diagnostics.Debug.WriteLine("Loading image " + imageUrl);
                try
                {
                    imageData = await WebTools.httpRequestByteArry(imageUrl, cancellationToken);
                }
                catch (TaskCanceledException)
                {

                }
                catch (Exception e)
                {
                    if (e.Message == "404-NotFound")
                    {
                        imageData = await WebTools.httpRequestByteArry("http://is2.4chan.org/" + board + "/" + tim + ext, cancellationToken);
                    }
                    else
                    {
                        System.Diagnostics.Debugger.Break();
                        throw;
                    }
                }
            }
        }

        public async Task loadThumb(CancellationToken cancellationToken = new CancellationToken())
        {
            if (thumbData == null)
            {
                //System.Diagnostics.Debug.WriteLine("Loading thumb " + thumbUrl);
                try
                {
                    thumbData = await WebTools.httpRequestByteArry(thumbUrl, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    if (e.Message == "404-NotFound")
                    {
                        thumbData = await WebTools.httpRequestByteArry(Global.DEFAULT_IMAGE, cancellationToken);
                    }
                    else
                    {
                        System.Diagnostics.Debugger.Break();
                        throw;
                    }
                }
            }
        }

        public void on404()
        {
            autoRefresh = false;
            saveImages = false;
            autoRefreshThread.Abort();
        }

        public int CompareTo(ChanPost other)
        {
            if (other.sticky == sticky)
            {
                return other.last_modified - last_modified;
            }
            return other.sticky - sticky;
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
