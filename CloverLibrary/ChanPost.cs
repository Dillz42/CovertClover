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

        public List<int> replyList = new List<int>();
        public void AddReplyNum(int reply)
        {
            if (replyList.Contains(reply) == false)
            {
                replyList.Add(reply);
            }
        }

        private byte[] imageData = null;
        private byte[] thumbData = null;

        public bool thumbInMem = false;
        public bool thumbSaved = false;
        public bool imageInMem = false;
        public bool imageSaved = false;

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
            
            com = com.Replace("<br>", "\n");
            com = System.Net.WebUtility.HtmlDecode(com);

            this.thread = thread;
            if (System.IO.File.Exists(ImagePath))
            {
                imageSaved = true;
            }

            json = jsonObject;
        }

        public void Update(ChanPost newData)
        {
            filedeleted = newData.filedeleted;
        }

        public string ThumbPath
        {
            get
            {
                return thread.GetDir() + Global.THUMBS_FOLDER_NAME + Global.MakeSafeFilename(tim + "s.jpg");
            }
        }

        public string ImagePath
        {
            get
            {
                return thread.GetDir() + Global.MakeSafeFilename(tim + "-" + filename + ext);
            }
        }

        public void LoadThumb(CancellationToken cancellationToken = new CancellationToken())
        { Task t = LoadThumbAsync(cancellationToken);}
        public async Task LoadThumbAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            if (ext != "" && thumbData == null)
            {
                thumbData = await GetThumbDataAsync();
                thumbInMem= true;
            }
        }

        public void LoadImage(CancellationToken cancellationToken = new CancellationToken())
        { Task t = LoadImageAsync(cancellationToken); }
        public async Task LoadImageAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            if (ext != "" && imageData == null)
            {
                imageData = await GetImageDataAsync();
                imageInMem = true;
            }
        }

        public async Task<byte[]> GetThumbDataAsync()
        {
            if (thumbInMem)
            {
                return thumbData;
            }
            else if (thumbSaved || System.IO.File.Exists(ThumbPath))
            {
                thumbSaved = true;
                return System.IO.File.ReadAllBytes(ThumbPath);
            }
            else
            {
                return await WebTools.HttpRequestByteArryAsync(
                            Global.BASE_IMAGE_URL + thread.board + "/" + tim + "s.jpg");
            }
        }

        public async Task<byte[]> GetImageDataAsync()
        {
            if (imageInMem)
            {
                return imageData;
            }
            else if (imageSaved || System.IO.File.Exists(ImagePath))
            {
                imageSaved = true;
                return System.IO.File.ReadAllBytes(ImagePath);
            }
            else
            {
                try
                {
                    return await WebTools.HttpRequestByteArryAsync(Global.BASE_IMAGE_URL + thread.board + "/" + tim + ext);
                }
                catch (Exception)
                {
                    return await WebTools.HttpRequestByteArryAsync(Global.BACK_IMAGE_URL + thread.board + "/" + tim + ext);
                }
            }
        }

        public async Task SaveThumbAsync(string dir, CancellationToken cancellationToken = new CancellationToken())
        {
            await LoadThumbAsync(cancellationToken);
            if (ext != "" && thumbData != null)
            {
                if (System.IO.File.Exists(ThumbPath) == false)
                {
                    Global.Log(this, "Saving thumb '" + ThumbPath + "'");
                    System.IO.File.WriteAllBytes(ThumbPath, thumbData);
                    System.IO.File.SetAttributes(ThumbPath, System.IO.FileAttributes.ReadOnly);
                }
                else
                {
                    Global.Log(this, "Thumb exists '" + ThumbPath + "'");
                }
                thumbSaved = true;
                ClearThumbData();
            }
        }

        public async Task SaveImageAsync(string dir, CancellationToken cancellationToken = new CancellationToken())
        {
            if (ext != "" && imageData != null)
            {
                await LoadImageAsync(cancellationToken);
                if (System.IO.File.Exists(ImagePath) == false)
                {
                    Global.Log(this, "Saving image '" + ImagePath + "'");
                    System.IO.File.WriteAllBytes(ImagePath, imageData);
                    System.IO.File.SetAttributes(ImagePath, System.IO.FileAttributes.ReadOnly);
                } 
                else
                {
                    Global.Log(this, "Image exists '" + ImagePath + "'");
                }
                imageSaved = true;
                ClearImageData();
            }
        }

        public void ClearThumbData()
        {
            thumbData = null;
            thumbInMem = false;
        }

        public void ClearImageData()
        {
            imageData = null;
            imageInMem = false;
        }
    }
}
