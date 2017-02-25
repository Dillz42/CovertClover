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
        private static Semaphore webRequestSemaphore = new Semaphore(2, 10);
        public static async Task<byte[]> httpRequestByteArry(string url,
            CancellationToken cancellationToken = new CancellationToken())
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (var client = new System.Net.Http.HttpClient())
            {
                webRequestSemaphore.WaitOne();
                System.Net.Http.HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
                webRequestSemaphore.Release();
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsByteArrayAsync();
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
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
                webRequestSemaphore.WaitOne();
                System.Net.Http.HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
                webRequestSemaphore.Release();
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
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
