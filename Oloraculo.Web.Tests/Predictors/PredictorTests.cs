using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Oloraculo.Web;
using Oloraculo.Web.DAL;
using Oloraculo.Web.Helpers;
using Oloraculo.Web.Models;
using Oloraculo.Web.Models.ApiFootballModels;
using Oloraculo.Web.Models.CsvModels;
using Oloraculo.Web.Predictors;
using Oloraculo.Web.Probability;
using Oloraculo.Web.Services;
using Oloraculo.Web.Services.Simulation;
using System.Globalization;
using System.Net;
using System.Text.Json;

namespace Oloraculo.Web.Tests;

public class PredictorTests : TestFixtures
{
    [Fact]
    public void GoalModel_ProducesUsableScorelineWhenTeamsHaveEnoughHistory()
    {
        var model = new GoalModel(
        [
            Result("a", "b", 2, 0),
            Result("a", "b", 1, 0),
            Result("b", "a", 1, 2)
        ]);

        var prediction = model.Predict(TestContext());

        Assert.False(prediction.Degraded);
        Assert.NotNull(prediction.Scoreline);
        Assert.True(prediction.ExpectedHomeGoals > 0.1);
        Assert.True(prediction.Outcome.IsValid);
    }

    [Fact]
    public void ContextModel_DoesNotClaimLineupsOrOddsWereUsedWithoutConversionLogic()
    {
        var goal = new GoalModel(
        [
            Result("a", "b", 2, 0),
            Result("a", "b", 1, 0),
            Result("b", "a", 1, 2)
        ]);
        var context = TestContext(fixtureContext: new FixtureContext
        {
            FixtureId = "test",
            HasLineups = true,
            HasOdds = true
        });

        var prediction = new GoalPlusRecentContextModel(goal).Predict(context);

        Assert.DoesNotContain(nameof(FeaturesEnum.Lineups), prediction.FeaturesUsed);
        Assert.DoesNotContain(nameof(FeaturesEnum.Odds), prediction.FeaturesUsed);
        Assert.Contains("modelo de impacto de alineaciones", prediction.FeaturesMissing);
        Assert.Contains("calibración por cuotas", prediction.FeaturesMissing);
        Assert.True(prediction.Degraded);
    }

    [Fact]
    public void ContextModel_BecomesUsableWhenAvailabilityActuallyAdjustsGoals()
    {
        var goal = new GoalModel(
        [
            Result("a", "b", 2, 0),
            Result("a", "b", 1, 0),
            Result("b", "a", 1, 2)
        ]);
        var context = TestContext(fixtureContext: new FixtureContext
        {
            FixtureId = "test",
            UnavailableHomePlayers = 2
        });

        var prediction = new GoalPlusRecentContextModel(goal).Predict(context);

        Assert.False(prediction.Degraded);
        Assert.Contains("Disponibilidad de jugadores", prediction.FeaturesUsed);
    }

    [Fact]
    public void FinalSelector_ChoosesHighestUsableRungWithoutAveraging()
    {
        var form = Prediction(3, "Forma reciente", .05, .05, .90);
        var goal = Prediction(4, "Goal", .90, .05, .05, scoreline: ProbabilityHelper.PoissonScoreline(3.0, .4));
        var context = Prediction(5, "Context", .10, .80, .10, degraded: true, missing: ["availability"]);

        var final = FinalPredictionSelector.Select([form, goal, context]);

        Assert.Equal("Oráculo final", final.PredictorName);
        Assert.Equal(4, final.PredictorPriority);
        Assert.Equal(goal.Outcome, final.Outcome);
        Assert.NotEqual(.475, final.Outcome.HomeWin, 3);
    }

    [Fact]
    public void FinalSelector_AppliesMomentumBiasToHome_WhenDrawIsSelectedAndMarkovFavorsHome()
    {
        var markov = Prediction(3, "Inercia de Markov", .60, .20, .20); // home Win 0.60, Draw 0.20, away Win 0.20
        var goal = Prediction(4, "Goal", .30, .40, .30); // TopPick is Draw

        var final = FinalPredictionSelector.Select([markov, goal]);

        Assert.Equal(0.45, final.Outcome.HomeWin, 5);
        Assert.Equal(0.25, final.Outcome.Draw, 5);
        Assert.Equal(0.30, final.Outcome.AwayWin, 5);
        Assert.Contains(final.Drivers, d => d.Contains("sesgo de momentum"));
    }

    [Fact]
    public void FinalSelector_AppliesMomentumBiasToAway_WhenDrawIsSelectedAndMarkovFavorsAway()
    {
        var markov = Prediction(3, "Inercia de Markov", .20, .20, .60); // home Win 0.20, Draw 0.20, away Win 0.60
        var goal = Prediction(4, "Goal", .30, .40, .30); // TopPick is Draw

        var final = FinalPredictionSelector.Select([markov, goal]);

        Assert.Equal(0.30, final.Outcome.HomeWin, 5);
        Assert.Equal(0.25, final.Outcome.Draw, 5);
        Assert.Equal(0.45, final.Outcome.AwayWin, 5);
        Assert.Contains(final.Drivers, d => d.Contains("sesgo de momentum"));
    }

    [Fact]
    public void FinalSelector_DoesNotApplyMomentumBias_WhenMarkovIsDegradedOrNoAsymmetry()
    {
        // Case A: Markov degraded
        var markov1 = Prediction(3, "Inercia de Markov", .60, .20, .20, degraded: true);
        var goal1 = Prediction(4, "Goal", .30, .40, .30);
        var final1 = FinalPredictionSelector.Select([markov1, goal1]);
        Assert.Equal(goal1.Outcome.HomeWin, final1.Outcome.HomeWin, 5);

        // Case B: No asymmetry (Home Win 0.33, Away Win 0.33)
        var markov2 = Prediction(3, "Inercia de Markov", .33, .34, .33);
        var goal2 = Prediction(4, "Goal", .30, .40, .30);
        var final2 = FinalPredictionSelector.Select([markov2, goal2]);
        Assert.Equal(goal2.Outcome.HomeWin, final2.Outcome.HomeWin, 5);
    }

    [Fact]
    public void FinalSelector_AppliesLightRankingBiasWhenEloAndFifaAgreeAgainstSelected()
    {
        var fifa = Prediction(1, "Ranking FIFA", .15, .20, .65, sources: [SourceMetadata.FifaRankings]);
        var elo = Prediction(2, "Elo", .10, .20, .70, sources: [SourceMetadata.EloRatings]);
        var goalScoreline = ProbabilityHelper.PoissonScoreline(1.4, 1.1);
        var goal = Prediction(4, "Goal", .45, .35, .20, scoreline: goalScoreline);

        var final = FinalPredictionSelector.Select([fifa, elo, goal]);

        Assert.Equal("Oráculo final", final.PredictorName);
        Assert.Equal(4, final.PredictorPriority);
        Assert.Equal(.40125, final.Outcome.HomeWin, 5);
        Assert.Equal(.3275, final.Outcome.Draw, 5);
        Assert.Equal(.27125, final.Outcome.AwayWin, 5);
        Assert.Same(goalScoreline, final.Scoreline);
        Assert.Contains(final.Drivers, d => d.Contains("calibración Elo/FIFA"));
        Assert.Contains("calibración Elo/FIFA", final.Explanation);
        Assert.Contains(SourceMetadata.FifaRankings, final.Sources);
        Assert.Contains(SourceMetadata.EloRatings, final.Sources);
    }

    [Fact]
    public void FinalSelector_DoesNotApplyRankingBiasWhenRankingModelsDisagree()
    {
        var fifa = Prediction(1, "Ranking FIFA", .65, .20, .15, sources: [SourceMetadata.FifaRankings]);
        var elo = Prediction(2, "Elo", .10, .20, .70, sources: [SourceMetadata.EloRatings]);
        var goal = Prediction(4, "Goal", .45, .35, .20);

        var final = FinalPredictionSelector.Select([fifa, elo, goal]);

        Assert.Equal(goal.Outcome, final.Outcome);
        Assert.DoesNotContain(final.Drivers, d => d.Contains("calibración Elo/FIFA"));
        Assert.DoesNotContain(SourceMetadata.FifaRankings, final.Sources);
        Assert.DoesNotContain(SourceMetadata.EloRatings, final.Sources);
    }

    [Fact]
    public void FinalSelector_DoesNotApplyRankingBiasWhenRankingModelIsDegraded()
    {
        var fifa = Prediction(1, "Ranking FIFA", .15, .20, .65, degraded: true, sources: [SourceMetadata.FifaRankings]);
        var elo = Prediction(2, "Elo", .10, .20, .70, sources: [SourceMetadata.EloRatings]);
        var goal = Prediction(4, "Goal", .45, .35, .20);

        var final = FinalPredictionSelector.Select([fifa, elo, goal]);

        Assert.Equal(goal.Outcome, final.Outcome);
        Assert.DoesNotContain(final.Drivers, d => d.Contains("calibración Elo/FIFA"));
        Assert.DoesNotContain(SourceMetadata.FifaRankings, final.Sources);
        Assert.DoesNotContain(SourceMetadata.EloRatings, final.Sources);
    }

    [Fact]
    public async Task Predict_IntegrationWithPredictionService_AppliesMarkovInertia()
    {
        // 1. Setup in-memory DB
        await using var db = await NewDb();

        var homeId = "home-team";
        var awayId = "away-team";
        var dummyOpponentId = "dummy-opponent";

        // Add Teams
        db.Teams.Add(new Team { Id = homeId, Name = "Home Team" });
        db.Teams.Add(new Team { Id = awayId, Name = "Away Team" });
        db.Teams.Add(new Team { Id = dummyOpponentId, Name = "Dummy Opponent" });

        // Add Ratings
        var today = DateTimeOffset.UtcNow;
        db.Ratings.AddRange(
            new Rating { TeamId = homeId, Type = RatingTypeEnum.Elo, Value = 1400, Source = "test", AsOf = today },
            new Rating { TeamId = awayId, Type = RatingTypeEnum.Elo, Value = 1600, Source = "test", AsOf = today },
            new Rating { TeamId = homeId, Type = RatingTypeEnum.Fifa, Value = 1000, Source = "test", AsOf = today },
            new Rating { TeamId = awayId, Type = RatingTypeEnum.Fifa, Value = 1000, Source = "test", AsOf = today }
        );

        // Add a recent dummy match to make the goal model's latest date be today
        db.Results.Add(new MatchResult
        {
            Id = Guid.NewGuid().ToString("N"),
            HomeTeamId = "dummy-x",
            AwayTeamId = "dummy-y",
            HomeGoals = 1,
            AwayGoals = 1,
            Date = today,
            Tournament = "test",
            Neutral = true,
            Source = "test"
        });

        // Add 6 matches 4 years ago for home (wins) and away (losses)
        var fourYearsAgo = today.AddYears(-4);
        for (int i = 0; i < 6; i++)
        {
            // Home wins 2-0
            db.Results.Add(new MatchResult
            {
                Id = Guid.NewGuid().ToString("N"),
                HomeTeamId = homeId,
                AwayTeamId = dummyOpponentId,
                HomeGoals = 2,
                AwayGoals = 0,
                Date = fourYearsAgo.AddDays(-i),
                Tournament = "test",
                Neutral = true,
                Source = "test"
            });

            // Away loses 0-4
            db.Results.Add(new MatchResult
            {
                Id = Guid.NewGuid().ToString("N"),
                HomeTeamId = dummyOpponentId,
                AwayTeamId = awayId,
                HomeGoals = 4,
                AwayGoals = 0,
                Date = fourYearsAgo.AddDays(-i),
                Tournament = "test",
                Neutral = true,
                Source = "test"
            });
        }

        await db.SaveChangesAsync();

        // 2. Initialize PredictionService
        var service = new PredictionService(db, SimulationOptions(1, 1));
        var fixture = new Fixture
        {
            Id = "test-fixture-id",
            HomeTeamId = homeId,
            AwayTeamId = awayId,
            NeutralVenue = true
        };

        // 3. Run prediction
        var result = await service.PredictAsync(fixture);

        // 4. Assert
        Assert.NotNull(result);
        var final = result.BestPrediction;
        Assert.Equal("Oráculo final", final.PredictorName);

        // Assert that the base top pick (which was Draw for RecentFormModel) was shifted to Home Win due to Markov momentum
        Assert.Equal("Home", final.Outcome.TopPick);
        Assert.Contains(final.Drivers, d => d.Contains("sesgo de momentum"));
        Assert.Contains("sesgo de momentum", final.Explanation);
        Assert.Contains(SourceMetadata.HistoricalResultsCsv, final.Sources);
    }

}
