using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace UC07_TrabalhoFinal.Caching
{
    public static class ConcurrentCache
    {
        #region Fields

        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<string, Task<string>>>> Cache;

        #endregion Fields

        #region Constructors

        static ConcurrentCache()
        {
            Cache = new ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<string, Task<string>>>>();
        }

        #endregion Constructors

        #region Public methods

        public static Task<string> GetOrAdd(string title, string language, string uri)
        {
            Lazy<Task<string>> lazyTask = new Lazy<Task<string>>(
                () =>
                    {
                        var uriSubCache = new ConcurrentDictionary<string, Task<string>>();
                        var languageSubCache = new ConcurrentDictionary<string, ConcurrentDictionary<string, Task<string>>>();

                        if (!uriSubCache.ContainsKey(uri))
                        {
                            uriSubCache.TryAdd(uri, GetInfoAsync(uri));
                        }

                        if (!languageSubCache.ContainsKey(language))
                        {
                            languageSubCache.TryAdd(language, uriSubCache);
                        }

                        var returnTask = Cache.GetOrAdd(title, languageSubCache)
                                                .GetOrAdd(language, uriSubCache)
                                                .GetOrAdd(uri, GetInfoAsync(uri));

                        return returnTask;
                    }
                    , LazyThreadSafetyMode.PublicationOnly
            );

            return lazyTask.Value;
        }

        private static Task<string> GetInfoAsync(string uri)
        {
            TaskCompletionSource<string> tcs = new TaskCompletionSource<string>();
            var client = new HttpClient();
            var streamTask = client.GetStreamAsync(uri);

            streamTask.ContinueWith(
                _ =>
                {
                    if (streamTask.IsCompleted)
                    {
                        var result = streamTask.Result;
                        var stringResult = new StreamReader(result).ReadToEnd();
                        tcs.TrySetResult(stringResult);
                    }
                    else
                    {
                        if (streamTask.IsCanceled)
                        {
                            tcs.TrySetCanceled();
                        }
                        else
                        {
                            tcs.TrySetException(streamTask.Exception.InnerExceptions);
                        }
                    }
                }
            );

            return tcs.Task;
        }

        #endregion Public methods
    }
}