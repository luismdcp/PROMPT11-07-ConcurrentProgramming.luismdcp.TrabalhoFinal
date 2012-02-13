using System.Collections.Concurrent;
using System.Collections.Generic;

namespace UC07_TrabalhoFinal.Models
{
    public class AggregatedMovieInfo
    {
        #region Properties

        public string FullTitle { get; set; }
        public int Year { get; set; }
        public string Director { get; set; }
        public string Synopsis { get; set; }
        public string PosterUrl { get; set; }
        public List<string> FlickrPhotosUrls { get; set; }
        public ConcurrentBag<NyTimesReview> Reviews { get; set; }

        #endregion Properties

        #region Constructor

        public AggregatedMovieInfo()
        {
            this.FlickrPhotosUrls = new List<string>();
            this.Reviews = new ConcurrentBag<NyTimesReview>();
        }

        public AggregatedMovieInfo(string fullTitle, int year, string director, string synopsis, string posterUrl) : this()
        {
            this.FullTitle = fullTitle;
            this.Year = year;
            this.Director = director;
            this.Synopsis = synopsis;
            this.PosterUrl = posterUrl;
        }

        #endregion
    }
}