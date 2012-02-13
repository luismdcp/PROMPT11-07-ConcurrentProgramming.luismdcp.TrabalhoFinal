using System;
using System.Collections.Generic;
using System.IO;
using System.Json;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Mvc;
using UC07_TrabalhoFinal.Models;

namespace UC07_TrabalhoFinal.Controllers
{
    public class HomeController : AsyncController
    {
        const string FlickrApiKey = "7b5b23c5612668928d0a39cb422fba00";
        const string NyTimesApiKey = "8784a3b88260ed39c7a5986371e09673:7:65631985";
        const string BingApiKey = "A2FD7C4C25F75629FC1AF1780796744062365577";
        readonly List<Task> tasks = new List<Task>();

        // GET: /Home/
        public void IndexAsync(string t, int? y, string l)
        {
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

                            var bingPlotResultTask = GetBingInfoAsync(movieInfo.Synopsis).ContinueWith(
                                    bingTask =>
                                        {
                                            if (bingTask.IsCompleted)
                                            {
                                                FillValuesFromBingPlotResult(bingTask.Result, movieInfo);
                                            }
                                        }
                            );

                            tasks.Add(bingPlotResultTask);


                            var flickrResultTask = GetFlickrInfoAsync(movieInfo.FullTitle, movieInfo.Director).ContinueWith(
                                    flickrTask =>
                                        {
                                            if (flickrTask.IsCompleted)
                                            {
                                                FillValuesFromFlickrResult(flickrTask.Result, movieInfo);
                                            }
                                        }
                            );

                            tasks.Add(flickrResultTask);

                            if (y.HasValue)
                            {
                                var nYTimesResultTask = GetNyTimesInfoAsync(movieInfo.FullTitle, y.Value).ContinueWith(
                                    nyTimesTask =>
                                    {
                                        if (nyTimesTask.IsCompleted)
                                        {
                                            var nyTimesResult = nyTimesTask.Result;
                                            FillValuesFromNYTimesResultAsyn(nyTimesResult, movieInfo);  
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

        private Task<string> GetBingInfoAsync(string textToTranslate)
        {
            string bingUri = string.Format("http://api.bing.net/json.aspx?AppId={0}&Query={1}&Sources=Translation&Version=2.2&Translation.SourceLanguage=en&Translation.TargetLanguage=pt", BingApiKey, textToTranslate);
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

        private void FillValuesFromNYTimesResultAsyn(string nyTimesResult, AggregatedMovieInfo movieInfo)
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

                    var returnTask = GetBingInfoAsync(capsuleReview).ContinueWith(
                        bingTask =>
                        {
                            var parseTranslatedReview = JsonValue.Parse(bingTask.Result).AsDynamic();
                            string capsuleReviewTranslation = parseTranslatedReview.SearchResponse.Translation.Results[0].TranslatedTerm;
                            movieInfo.Critics.Add(new NyTimesCritic(critic, capsuleReviewTranslation, fullReviewUrl));
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
            var parseValue = JsonValue.Parse(bingPlotResult).AsDynamic();
            movieInfo.Synopsis = parseValue.SearchResponse.Translation.Results[0].TranslatedTerm;
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

        #endregion Helper methods
    }
}