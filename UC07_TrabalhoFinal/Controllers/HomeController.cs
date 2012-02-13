using System;
using System.IO;
using System.Json;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Mvc;
using UC07_TrabalhoFinal.Models;
using System.Collections.Generic;

namespace UC07_TrabalhoFinal.Controllers
{
    public class HomeController : AsyncController
    {
        // GET: /Home/
        public void IndexAsync(string t, int? y, string l)
        {
            const string flickrApiKey = "7b5b23c5612668928d0a39cb422fba00";
            const string nyTimesApiKey = "8784a3b88260ed39c7a5986371e09673:7:65631985";
            const string bingApiKey = "A2FD7C4C25F75629FC1AF1780796744062365577";

            Task<string> flickrResultTask;
            Task bingPlotResultTask;

            AggregatedMovieInfo movieInfo = new AggregatedMovieInfo();
            AsyncManager.OutstandingOperations.Increment();

            var imdbResultTask = GetImdbInfoAsync(t, y, l);

            imdbResultTask.ContinueWith(
                _ =>
                    {
                        if (imdbResultTask.IsCompleted)
                        {
                            var imdbResult = imdbResultTask.Result;
                            FillValuesFromImdbResult(imdbResult, movieInfo);

                            bingPlotResultTask = GetBingInfoAsync(movieInfo.Synopsis, bingApiKey).ContinueWith(
                                    bingTask => FillValuesFromBingPlotResult(bingTask.Result, movieInfo)
                                );


                            flickrResultTask = GetFlickrInfoAsync(movieInfo.FullTitle, movieInfo.Director, flickrApiKey);

                            //if (y.HasValue)
                            //{
                            //    var nyTimesTaskResult = GetNyTimesInfoAsync(movieInfo.FullTitle, y.Value, nyTimesApiKey);

                            //    nyTimesTaskResult.ContinueWith(
                            //        task =>
                            //            {
                            //                var nyTimesResult = nyTimesTaskResult.Result;
                            //            }
                            //        );
                            //}

                            Task.Factory.ContinueWhenAll(new Task[] { flickrResultTask, bingPlotResultTask }, tasks =>
                                                                                                                {
                                                                                                                    AsyncManager.Parameters["movieInfo"] = movieInfo;
                                                                                                                    AsyncManager.OutstandingOperations.Decrement();
                                                                                                                });
                        }
                        else
                        {
                            AsyncManager.OutstandingOperations.Decrement();
                        }
                    });
        }

        public JsonResult IndexCompleted(AggregatedMovieInfo movieInfo)
        {
            return Json(movieInfo, JsonRequestBehavior.AllowGet);
        }

        #region Helper methods

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

        private Task<string> GetFlickrInfoAsync(string title, string director, string flickrApiKey)
        {
            string flickrUri = string.Format("http://api.flickr.com/services/rest/?method=flickr.photos.search&api_key={0}&format=json&nojsoncallback=1&text={1}+{2}&sort=interestingness-desc", flickrApiKey, title, director);
            return GetInfoAsync(flickrUri);
        }

        private Task<string> GetNyTimesInfoAsync(string title, int year, string nyTimesApiKey)
        {
            string nyTimesUri = string.Format("http://api.nytimes.com/svc/movies/v2/reviews/search.json?query={0}&api-key={1}&opening-date={2}-01-01;{3}-12-31", title, nyTimesApiKey, year, year + 1);
            return GetInfoAsync(nyTimesUri);
        }

        private Task<string> GetBingInfoAsync(string textToTranslate, string bingApiKey)
        {
            string bingUri = string.Format("http://api.bing.net/json.aspx?AppId={0}&Query={1}&Sources=Translation&Version=2.2&Translation.SourceLanguage=en&Translation.TargetLanguage=pt", bingApiKey, textToTranslate);
            return GetInfoAsync(bingUri);
        }

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
                        var flickrStringResult = new StreamReader(result).ReadToEnd();
                        tcs.TrySetResult(flickrStringResult);
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

        private void FillValuesFromImdbResult(string imdbResult, AggregatedMovieInfo info)
        {
            var parseValue = JsonValue.Parse(imdbResult).AsDynamic();

            if (parseValue.Response == "True")
            {
                info.FullTitle = parseValue.Title;
                info.Year = parseValue.Year;
                info.Director = parseValue.Director;
                info.Synopsis = parseValue.Plot;
                info.PosterUrl = parseValue.Poster;   
            }
        }

        private void FillValuesFromFlickrResult(string flickrResult, AggregatedMovieInfo info)
        {
            var parseValue = JsonValue.Parse(flickrResult).AsDynamic();
        }

        private void FillValuesFromBingPlotResult(string bingPlotResult, AggregatedMovieInfo info)
        {
            var parseValue = JsonValue.Parse(bingPlotResult).AsDynamic();

            info.Synopsis = parseValue.SearchResponse.Translation.Results[0].TranslatedTerm;
        }

        private Task DelayAsync(int waitTime)
        {
            TaskCompletionSource<object> source = new TaskCompletionSource<object>();

            new Timer((state) => source.SetResult(new object()),
                      null,
                      waitTime,
                      0
                );

            return source.Task;
        }

        private IEnumerable<Task> ProcessBingRequest(string textToTranslate, string bingApiKey)
        {
            var task = GetBingInfoAsync(textToTranslate, bingApiKey);
            yield return task;

            if (task.IsCompleted)
            {
                var taskResult = task.Result;


            }
        }

        #endregion Helper methods
    }
}