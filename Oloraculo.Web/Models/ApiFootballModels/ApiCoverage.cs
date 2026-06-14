namespace Oloraculo.Web.Models.ApiFootballModels
{
    public class ApiCoverage
    {
        public ApiFixtureCoverage Fixtures { get; set; } = new();
        public bool Injuries { get; set; }
        public bool Odds { get; set; }
    }
}
