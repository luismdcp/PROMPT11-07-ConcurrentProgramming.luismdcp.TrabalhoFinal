
namespace UC07_TrabalhoFinal.Models
{
    public class NyTimesReview
    {
        #region Properties

        public string Critic { get; set; }
        public string CapsuleReview { get; set; }
        public string FullReviewUrl { get; set; }

        #endregion Properties

        #region Constructor

        public NyTimesReview(string critic, string capsuleReview, string fullReviewUrl)
        {
            this.Critic = critic;
            this.CapsuleReview = capsuleReview;
            this.FullReviewUrl = fullReviewUrl;
        }

        #endregion
    }
}