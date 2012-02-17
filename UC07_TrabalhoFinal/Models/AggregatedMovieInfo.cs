using System.Collections.Concurrent;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace UC07_TrabalhoFinal.Models
{
    public class AggregatedMovieInfo
    {
        #region Properties

        public string title { get; set; }
        public int year { get; set; }
        public string director_name { get; set; }
        public string plot { get; set; }
        public string poster_url { get; set; }
        public List<string> photos { get; set; }
        [JsonIgnore]
        public ConcurrentBag<NyTimesReview> ConcurrentReviews { get; set; }
        public NyTimesReview[] reviews { get; set; }

        #endregion Properties

        #region Constructor

        public AggregatedMovieInfo()
        {
            this.photos = new List<string>();
            this.ConcurrentReviews = new ConcurrentBag<NyTimesReview>();
        }

        public AggregatedMovieInfo(string title, int year, string director_name, string plot, string poster_url) : this()
        {
            this.title = title;
            this.year = year;
            this.director_name = director_name;
            this.plot = plot;
            this.poster_url = poster_url;
        }

        #endregion

        #region Helper methods

        public void NormalizeReviewsForJson()
        {
            this.reviews = this.ConcurrentReviews.ToArray();
        }

        #endregion
    }
}