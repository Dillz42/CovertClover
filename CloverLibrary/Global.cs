using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace CloverLibrary
{
    public class Global
    {
        public const string BASE_URL = "http://a.4cdn.org/";
        public const string BASE_IMAGE_URL = "http://i.4cdn.org/";
        public const string DEFAULT_IMAGE = "http://s.4cdn.org/image/fp/logo-transparent.png";

        private static SortedDictionary<int, ChanPost> postDictionary = new SortedDictionary<int, ChanPost>();

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
                    if (postDictionary.ContainsKey((int)jsonThread["no"]) == false)
                    {
                        postDictionary.Add((int)jsonThread["no"], new ChanPost(jsonThread, board));
                    }
                }
            }
        }

        public static List<ChanPost> getBoard(string board, CancellationToken cancellationToken = new CancellationToken())
        {
            List<ChanPost> retVal = new List<ChanPost>();

            foreach (var post in postDictionary)
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
            retVal.Sort();
            return retVal;
        }

        public async static Task loadThread(int threadNumber, string board = "b", CancellationToken cancellationToken = new CancellationToken())
        {
            string address = BASE_URL + board + "/thread/" + threadNumber + ".json";
            JObject jsonObject = (JObject)await WebTools.httpRequestParse(address, JObject.Parse);

            foreach (JObject jsonThread in (JArray)jsonObject["posts"])
            {
                if (postDictionary.ContainsKey((int)jsonThread["no"]) == false)
                {
                    postDictionary.Add((int)jsonThread["no"], new ChanPost(jsonThread, board));
                }
            }
        }

        public static List<ChanPost> getThread(int threadNumber, string board = "b", CancellationToken cancellationToken = new CancellationToken())
        {
            List<ChanPost> retVal = new List<ChanPost>();

            foreach (var post in postDictionary)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                if (post.Value.resto == threadNumber || post.Value.no == threadNumber)
                {
                    retVal.Add(post.Value);
                }
            }
            retVal.Sort();
            return retVal;
        }
    }
}
