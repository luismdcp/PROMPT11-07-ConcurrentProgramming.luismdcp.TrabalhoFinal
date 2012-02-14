using System;
using System.Collections.Generic;
using System.IO;
using System.Json;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Mvc;
using Async;
using UC07_TrabalhoFinal.Caching;
using UC07_TrabalhoFinal.Models;

namespace UC07_TrabalhoFinal.Controllers
{
    public class HomeController : AsyncController
    {
        private const int MaxRetries = 8;
        const string FlickrApiKey = "7b5b23c5612668928d0a39cb422fba00";
        const string NyTimesApiKey = "8784a3b88260ed39c7a5986371e09673:7:65631985";
        const string BingApiKey = "A2FD7C4C25F75629FC1AF1780796744062365577";
        readonly List<Task> tasks = new List<Task>();
        private readonly Random randomizer = new Random();

        // GET: /Home/
        public void IndexAsync(string t, int? y, string l = "pt")
        {
            AggregatedMovieInfo movieInfo = new AggregatedMovieInfo();
            AsyncManager.OutstandingOperations.Increment();

            if (string.IsNullOrEmpty(t))
            {
                AsyncManager.Parameters["movieInfo"] = movieInfo;
                AsyncManager.OutstandingOperations.Decrement();
            }
            else
            {
                //var imdbResultTask = GetImdbInfoAsync(t, y, l);
                var imdbResultTask = GetCachedImdbInfoAsync(t, y, l);
                tasks.Add(imdbResultTask);

                imdbResultTask.ContinueWith(
                    _ =>
                    {
                        if (imdbResultTask.IsCompleted)
                        {
                            var imdbResult = imdbResultTask.Result;
                            FillValuesFromImdbResult(imdbResult, movieInfo);

                            if (!String.IsNullOrEmpty(movieInfo.Synopsis))
                            {
                                //var bingPlotResultTask = GetBingInfoWithRetriesAsync(movieInfo.Synopsis, l).Run<string>().ContinueWith(
                                var bingPlotResultTask = GetCachedBingInfoWithRetriesAsync(movieInfo.Synopsis, t, l).Run<string>().ContinueWith(
                                    bingTask =>
                                    {
                                        if (bingTask.IsCompleted)
                                        {
                                            FillValuesFromBingPlotResult(bingTask.Result, movieInfo);
                                        }
                                    }
                                );

                                tasks.Add(bingPlotResultTask);
                            }

                            if (!string.IsNullOrEmpty(movieInfo.FullTitle) && !string.IsNullOrEmpty(movieInfo.Director))
                            {
                                //var flickrResultTask = GetFlickrInfoAsync(movieInfo.FullTitle, movieInfo.Director).ContinueWith(
                                var flickrResultTask = GetCachedFlickrInfoAsync(movieInfo.FullTitle, movieInfo.Director, l).ContinueWith(
                                    flickrTask =>
                                    {
                                        if (flickrTask.IsCompleted)
                                        {
                                            FillValuesFromFlickrResult(flickrTask.Result, movieInfo);
                                        }
                                    }
                                );

                                tasks.Add(flickrResultTask);
                            }

                            if (y.HasValue)
                            {
                                //var nYTimesResultTask = GetNyTimesInfoAsync(movieInfo.FullTitle, y.Value).ContinueWith(
                                var nYTimesResultTask = GetCachedNyTimesInfoAsync(movieInfo.FullTitle, y.Value, l).ContinueWith(
                                    nyTimesTask =>
                                    {
                                        if (nyTimesTask.IsCompleted)
                                        {
                                            var nyTimesResult = nyTimesTask.Result;
                                            FillValuesFromNYTimesResult(nyTimesResult, l, movieInfo);
                                        }
                                    }
                                );

                                tasks.Add(nYTimesResultTask);
                            }

                            Task.Factory.ContinueWhenAll(tasks.ToArray(), tsks =>
                            {
                                AsyncManager.Parameters["movieInfo"] = movieInfo;
                                AsyncManager.OutstandingOperations.Decrement();
                            });
                        }
                        else
                        {
                            AsyncManager.Parameters["movieInfo"] = movieInfo;
                            AsyncManager.OutstandingOperations.Decrement();
                        }
                    });   
            }
        }

        public JsonResult IndexCompleted(AggregatedMovieInfo movieInfo)
        {
            return Json(movieInfo, JsonRequestBehavior.AllowGet);
        }

        #region Cached methods

        private Task<string> GetCachedImdbInfoAsync(string title, int? year, string language)
        {
            StringBuilder imdbUriBuffer = new StringBuilder();
            imdbUriBuffer.Append(String.Format("http://imdbapi.com/?t={0}&plot=full", title));

            if (year.HasValue)
            {
                imdbUriBuffer.Append(string.Format("&y={0}", year));
            }

            if (!string.IsNullOrEmpty(language))
            {
                imdbUriBuffer.Append(string.Format("&i={0}", language));
            }

            return ConcurrentCache.GetOrAdd(title, language, imdbUriBuffer.ToString());
        }

        private Task<string> GetCachedFlickrInfoAsync(string title, string director, string language)
        {
            string flickrUri = string.Format("http://api.flickr.com/services/rest/?method=flickr.photos.search&api_key={0}&format=json&nojsoncallback=1&text={1}+{2}&sort=interestingness-desc", FlickrApiKey, title, director);
            return ConcurrentCache.GetOrAdd(title, language, flickrUri);
        }

        private Task<string> GetCachedNyTimesInfoAsync(string title, int year, string language)
        {
            string nyTimesUri = string.Format("http://api.nytimes.com/svc/movies/v2/reviews/search.json?query={0}&api-key={1}&opening-date={2}-01-01;{3}-12-31", title, NyTimesApiKey, year, year + 1);
            return ConcurrentCache.GetOrAdd(title, language, nyTimesUri);
        }

        private Task<string> GetCachedBingInfoAsync(string textToTranslate, string title, string language)
        {
            string bingUri = string.Format("http://api.bing.net/json.aspx?AppId={0}&Query={1}&Sources=Translation&Version=2.2&Translation.SourceLanguage=en&Translation.TargetLanguage={2}", BingApiKey, textToTranslate, language);
            return ConcurrentCache.GetOrAdd(title, language, bingUri);
        }

        private IEnumerator<Task> GetCachedBingInfoWithRetriesAsync(string textToTranslate, string title, string language)
        {
            var tcs = new TaskCompletionSource<string>();

            for (int retries = 0; retries < MaxRetries; ++retries)
            {
                var bingRequestTask = GetCachedBingInfoAsync(textToTranslate, title, language);
                yield return bingRequestTask;

                if (bingRequestTask.IsCompleted)
                {
                    var parseResult = JsonValue.Parse(bingRequestTask.Result).AsDynamic();
                    var translationResult = parseResult.SearchResponse.Translation;

                    if (translationResult.Count > 0)
                    {
                        string translatedResult = translationResult.Results[0].TranslatedTerm;
                        tcs.SetResult(translatedResult);
                        yield return tcs.Task;
                        yield break;
                    }
                }

                yield return DelayAsync(1000 + 1000 * retries + randomizer.Next(2000));
            }

            tcs.SetResult(string.Empty);
            yield return tcs.Task;
        }

        #endregion Cached methods

        #region Non cached methods

        private Task<string> GetImdbInfoAsync(string title, int? year, string language)
        {
            StringBuilder imdbUriBuffer = new StringBuilder();
            imdbUriBuffer.Append(String.Format("http://imdbapi.com/?t={0}&plot=full", title));

            if (year.HasValue)
            {
                imdbUriBuffer.Append(string.Format("&y={0}", year));
            }

            if (!string.IsNullOrEmpty(language))
            {
                imdbUriBuffer.Append(string.Format("&i={0}", language));
            }

            return GetInfoAsync(imdbUriBuffer.ToString());
        }

        private Task<string> GetFlickrInfoAsync(string title, string director)
        {
            string flickrUri = string.Format("http://api.flickr.com/services/rest/?method=flickr.photos.search&api_key={0}&format=json&nojsoncallback=1&text={1}+{2}&sort=interestingness-desc", FlickrApiKey, title, director);
            return GetInfoAsync(flickrUri);
        }

        private Task<string> GetNyTimesInfoAsync(string title, int year)
        {
            string nyTimesUri = string.Format("http://api.nytimes.com/svc/movies/v2/reviews/search.json?query={0}&api-key={1}&opening-date={2}-01-01;{3}-12-31", title, NyTimesApiKey, year, year + 1);
            return GetInfoAsync(nyTimesUri);
        }

        private Task<string> GetBingInfoAsync(string textToTranslate, string language)
        {
            string bingUri = string.Format("http://api.bing.net/json.aspx?AppId={0}&Query={1}&Sources=Translation&Version=2.2&Translation.SourceLanguage=en&Translation.TargetLanguage={2}", BingApiKey, textToTranslate, language);
            return GetInfoAsync(bingUri);
        }

        private IEnumerator<Task> GetBingInfoWithRetriesAsync(string textToTranslate, string language)
        {
            var tcs = new TaskCompletionSource<string>();

            for (int retries = 0; retries < MaxRetries; ++retries)
            {
                var bingRequestTask = GetBingInfoAsync(textToTranslate, language);
                yield return bingRequestTask;

                if (bingRequestTask.IsCompleted)
                {
                    var parseResult = JsonValue.Parse(bingRequestTask.Result).AsDynamic();
                    var translationResult = parseResult.SearchResponse.Translation;

                    if (translationResult.Count > 0)
                    {
                        string translatedResult = translationResult.Results[0].TranslatedTerm;
                        tcs.SetResult(translatedResult);
                        yield return tcs.Task;
                        yield break;
                    }
                }

                yield return DelayAsync(1000 + 1000 * retries + randomizer.Next(2000));
            }

            tcs.SetResult(string.Empty);
            yield return tcs.Task;
        }

        #endregion Non cached methods

        #region Fill methods

        private void FillValuesFromNYTimesResult(string nyTimesResult, string language, AggregatedMovieInfo movieInfo)
        {
            var parseValue = JsonValue.Parse(nyTimesResult).AsDynamic();

            if (parseValue.status == "OK")
            {
                var criticsResults = parseValue.results;

                foreach (var criticsResult in criticsResults)
                {
                    string critic = criticsResult.byline;
                    string capsuleReview = criticsResult.capsule_review;
                    string fullReviewUrl = criticsResult.link.url;

                    var returnTask = GetBingInfoWithRetriesAsync(capsuleReview, language).Run<string>().ContinueWith(
                        bingTask =>
                        {
                            if (bingTask.IsCompleted)
                            {
                                movieInfo.Reviews.Add(new NyTimesReview(critic, bingTask.Result, fullReviewUrl));
                            }

                        }, TaskContinuationOptions.AttachedToParent
                    );

                    tasks.Add(returnTask);
                }
            }
        }

        private void FillValuesFromImdbResult(string imdbResult, AggregatedMovieInfo movieInfo)
        {
            var parseValue = JsonValue.Parse(imdbResult).AsDynamic();

            if (parseValue.Response == "True")
            {
                movieInfo.FullTitle = parseValue.Title;
                movieInfo.Year = parseValue.Year;
                movieInfo.Director = parseValue.Director;
                movieInfo.Synopsis = parseValue.Plot;
                movieInfo.PosterUrl = parseValue.Poster;
            }
        }

        private void FillValuesFromFlickrResult(string flickrResult, AggregatedMovieInfo movieInfo)
        {
            var parseValue = JsonValue.Parse(flickrResult).AsDynamic();

            if (parseValue.stat == "ok")
            {
                var photos = parseValue.photos.photo;

                foreach (var photo in photos)
                {
                    var farm = photo.farm.Value;
                    var server = photo.server.Value;
                    var id = photo.id.Value;
                    var secret = photo.secret.Value;
                    var template = String.Format("http://farm{0}.static.flickr.com/{1}/{2}_{3}.jpg", farm, server, id, secret);

                    movieInfo.FlickrPhotosUrls.Add(template);
                }
            }
        }

        private void FillValuesFromBingPlotResult(string bingPlotResult, AggregatedMovieInfo movieInfo)
        {
            movieInfo.Synopsis = bingPlotResult;
        }

        #endregion Fill methods

        #region Helper methods

        private Task<string> GetInfoAsync(string uri)
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

        private Task DelayAsync(int waitTime)
        {
            TaskCompletionSource<object> source = new TaskCompletionSource<object>();

            new Timer(state => source.SetResult(new object()),
                      null,
                      waitTime,
                      0
            );

            return source.Task;
        }

        #endregion Helper methods
    }
}