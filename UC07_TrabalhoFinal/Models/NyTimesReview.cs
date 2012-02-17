
namespace UC07_TrabalhoFinal.Models
{
    public class NyTimesReview
    {
        #region Properties

        public string reviewer { get; set; }
        public string resume { get; set; }
        public string url { get; set; }

        #endregion Properties

        #region Constructor

        public NyTimesReview(string reviewer, string resume, string url)
        {
            this.reviewer = reviewer;
            this.resume = resume;
            this.url = url;
        }

        #endregion
    }
}