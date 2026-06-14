using Oloraculo.Web.Models;
using Oloraculo.Web.Probability;

namespace Oloraculo.Web.Predictors
{
    public class NullModel : IPredictor
    {
        public string Name => "Modelo base";
        public int Priority => 0;
        public MatchPrediction Predict(MatchContext context)
        {
            return new MatchPrediction
            {
                PredictorName = Name,
                PredictorPriority = Priority,
                FixtureId = context.Fixture.Id,
                HomeTeamId = context.HomeTeam.Id,
                AwayTeamId = context.AwayTeam.Id,
                Outcome = OutcomeProbabilities.Uniform,
                ExpectedHomeGoals = null,
                ExpectedAwayGoals = null,
                Scoreline = null,
                MostLikelyScore = null,
                Explanation = "Probabilidad uniforme sin señales adicionales.",
                Drivers = Array.Empty<string>(),
                FeaturesUsed = Array.Empty<string>(),
                FeaturesMissing = Array.Empty<string>(),
                Sources = Array.Empty<SourceMetadata>(),
                Degraded = false
            };
        }
    }
}
