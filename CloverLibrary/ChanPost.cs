using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace CloverLibrary
{
    public class ChanPost
    {
        public JObject json;

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
        public long last_modified;
        public string tag;
        public string semantic_url;

        public List<ChanPost> last_replies = new List<ChanPost>();

        public List<int> replyList = new List<int>();
        public void addReplyNum(int reply)
        {
            if (replyList.Contains(reply) == false)
            {
                replyList.Add(reply);
            }
        }

        public byte[] imageData = null;
        public byte[] thumbData = null;

        public ChanThread thread;

        public ChanPost()
        {
            
        }
        public ChanPost(JObject jsonObject, ChanThread thread)
        {
            no = (int)(jsonObject["no"] ?? 0);
            resto = (int)(jsonObject["resto"] ?? 0);
            sticky = (int)(jsonObject["sticky"] ?? 0);
            closed = (int)(jsonObject["closed"] ?? 0);
            archived = (int)(jsonObject["archived"] ?? 0);
            archived_on = (int)(jsonObject["archived_on"] ?? 0);
            now = (string)(jsonObject["now"] ?? "");
            time = (int)(jsonObject["time"] ?? 0);
            name = (string)(jsonObject["name"] ?? "");
            trip = (string)(jsonObject["trip"] ?? "");
            id = (string)(jsonObject["id"] ?? "");
            capcode = (string)(jsonObject["capcode"] ?? "");
            country = (string)(jsonObject["country"] ?? "");
            country_name = (string)(jsonObject["country_name"] ?? "");
            sub = (string)(jsonObject["sub"] ?? "");
            com = (string)(jsonObject["com"] ?? "");
            tim = (long)(jsonObject["tim"] ?? 0);
            filename = (string)(jsonObject["filename"] ?? "");
            ext = (string)(jsonObject["ext"] ?? "");
            fsize = (int)(jsonObject["fsize"] ?? 0);
            md5 = (string)(jsonObject["md5"] ?? "");
            w = (int)(jsonObject["w"] ?? 0);
            h = (int)(jsonObject["h"] ?? 0);
            tn_w = (int)(jsonObject["tn_w"] ?? 0);
            tn_h = (int)(jsonObject["tn_h"] ?? 0);
            filedeleted = (int)(jsonObject["filedeleted"] ?? 0);
            spoiler = (int)(jsonObject["spoiler"] ?? 0);
            custom_spoiler = (int)(jsonObject["custom_spoiler"] ?? 0);
            omitted_posts = (int)(jsonObject["omitted_posts"] ?? 0);
            omitted_images = (int)(jsonObject["omitted_images"] ?? 0);
            replies = (int)(jsonObject["replies"] ?? 0);
            images = (int)(jsonObject["images"] ?? 0);
            bumplimit = (int)(jsonObject["bumplimit"] ?? 0);
            imagelimit = (int)(jsonObject["imagelimit"] ?? 0);
            //string[] capcode_replies;;
            last_modified = (int)(jsonObject["last_modified"] ?? 0);
            tag = (string)(jsonObject["tag"] ?? "");
            semantic_url = (string)(jsonObject["semantic_url"] ?? "");

            if (jsonObject["last_replies"] != null)
            {
                foreach (JObject post in jsonObject["last_replies"])
                {
                    ChanPost reply_post = new ChanPost(post, thread);
                    last_replies.Add(reply_post);
                } 
            }

            com = com.Replace("<br>", "\n");
            com = System.Net.WebUtility.HtmlDecode(com);

            this.thread = thread;
            json = jsonObject;
        }

        public void update(ChanPost newData)
        {
            filedeleted = newData.filedeleted;
        }

        public string getThumbPath()
        {
            return thread.getDir() + Global.THUMBS_FOLDER_NAME + Global.MakeSafeFilename(tim + "s.jpg");
        }

        public string getImagePath()
        {
            return thread.getDir() + Global.MakeSafeFilename(tim + "-" + filename + ext);
        }

        public async Task loadThumb(CancellationToken cancellationToken = new CancellationToken())
        {
            if (ext != "" && thumbData == null)
            {

                if (System.IO.File.Exists(getThumbPath()) == true)
                {
                    Global.log(this, "Loading thumb from web '" + getThumbPath() + "'");
                    thumbData = System.IO.File.ReadAllBytes(getThumbPath());
                }
                else
                {
                    Global.log(this, "Loading thumb from web '" + getThumbPath() + "'");
                    try
                    {
                        thumbData = await WebTools.httpRequestByteArry(
                            Global.BASE_IMAGE_URL + thread.board + "/" + tim + "s.jpg", cancellationToken);
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
        }

        public async Task loadImage(CancellationToken cancellationToken = new CancellationToken())
        {
            if (ext != "" && imageData == null)
            {
                if (System.IO.File.Exists(getImagePath()) == true)
                {
                    Global.log(this, "Loading image from file '" + getImagePath() + "'");
                    imageData = System.IO.File.ReadAllBytes(getImagePath());
                }
                else
                {
                    Global.log(this, "Loading image from web '" + getImagePath() + "'");
                    try
                    {
                        imageData = await WebTools.httpRequestByteArry(
                            Global.BASE_IMAGE_URL + thread.board + "/" + tim + ext, cancellationToken);
                    }
                    catch (TaskCanceledException)
                    {

                    }
                    catch (Exception e)
                    {
                        if (e.Message == "404-NotFound")
                        {
                            imageData = await WebTools.httpRequestByteArry(
                                Global.BACK_IMAGE_URL + thread.board + tim + ext, cancellationToken);
                        }
                        else
                        {
                            System.Diagnostics.Debugger.Break();
                            throw;
                        }
                    }
                }
            }
        }

        public async Task saveThumb(string dir, CancellationToken cancellationToken = new CancellationToken())
        {
            await loadThumb(cancellationToken);
            if (ext != "" && thumbData != null)
            {
                if (System.IO.File.Exists(getThumbPath()) == false)
                {
                    Global.log(this, "Saving thumb '" + getThumbPath() + "'");
                    System.IO.File.WriteAllBytes(getThumbPath(), thumbData);
                    System.IO.File.SetAttributes(getThumbPath(), System.IO.FileAttributes.ReadOnly);
                }
                else
                {
                    Global.log(this, "Thumb exists '" + getThumbPath() + "'");
                }
            }
        }

        public async Task saveImage(string dir, CancellationToken cancellationToken = new CancellationToken())
        {
            if (ext != "" && imageData != null)
            {
                await loadImage(cancellationToken);
                if (System.IO.File.Exists(getImagePath()) == false)
                {
                    Global.log(this, "Saving image '" + getImagePath() + "'");
                    System.IO.File.WriteAllBytes(getImagePath(), imageData);
                    System.IO.File.SetAttributes(getImagePath(), System.IO.FileAttributes.ReadOnly);
                } 
                else
                {
                    Global.log(this, "Image exists '" + getImagePath() + "'");
                }
            }
        }
    }
}
