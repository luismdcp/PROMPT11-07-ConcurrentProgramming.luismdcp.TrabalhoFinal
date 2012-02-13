
namespace UC07_TrabalhoFinal.Models
{
    public class NyTimesCritic
    {
        #region Properties

        public string Critic { get; set; }
        public string CapsuleReview { get; set; }
        public string FullCriticUrl { get; set; }

        #endregion Properties

        #region Constructor

        public NyTimesCritic(string critic, string capsuleReview, string fullCriticUrl)
        {
            this.Critic = critic;
            this.CapsuleReview = capsuleReview;
            this.FullCriticUrl = fullCriticUrl;
        }

        #endregion
    }
}