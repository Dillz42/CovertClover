using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;

namespace CloverLibrary
{
    class WebTools
    {
        public static async Task<byte[]> httpRequestByteArry(string url,
            CancellationToken cancellationToken = new CancellationToken())
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (var client = new System.Net.Http.HttpClient())
            {
                System.Net.Http.HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsByteArrayAsync();
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new Exception("404-NotFound");
                }
                return null;
            }
        }

        public static async Task<string> httpRequest(string url,
            CancellationToken cancellationToken = new CancellationToken())
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (var client = new System.Net.Http.HttpClient())
            {
                System.Net.Http.HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new Exception("404-NotFound");
                }
                return "";
            }
        }

        public static async Task<object> httpRequestParse(string url, Func<string, object> parse,
            CancellationToken cancellationToken = new CancellationToken())
        {
            cancellationToken.ThrowIfCancellationRequested();

            string str = await httpRequest(url, cancellationToken);
            return parse(str);
        }
    }
}
