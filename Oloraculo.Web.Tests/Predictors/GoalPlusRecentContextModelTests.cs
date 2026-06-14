using Xunit;
using Oloraculo.Web.Models;
using Oloraculo.Web.Predictors;
using Oloraculo.Web.Probability;
using Oloraculo.Web.Services;
using System.Collections.Generic;
using System;

namespace Oloraculo.Web.Tests
{
    public class GoalPlusRecentContextModelTests : TestFixtures
    {
        [Fact]
        public void Predict_NoAdjustment_WhenMarkovPredictorIsDegraded()
        {
            // Set up GoalModel with some matches
            var goalModel = new GoalModel(
            [
                Result("A", "B", 2, 0),
                Result("A", "B", 1, 0),
                Result("B", "A", 1, 2)
            ]);
            var model = new GoalPlusRecentContextModel(goalModel);

            var context = TestContext("A", "B");
            context.HomeRecentMatchHistory = new List<MatchResult>(); // Empty -> Degraded
            context.AwayRecentMatchHistory = new List<MatchResult>(); // Empty -> Degraded

            var (baseHomeGoals, baseAwayGoals, _) = goalModel.ExpectedGoals(context);
            var prediction = model.Predict(context);

            // Expect base expected goals (unmodified since Markov is degraded)
            Assert.Equal(Math.Round(baseHomeGoals, 2), prediction.ExpectedHomeGoals);
            Assert.Equal(Math.Round(baseAwayGoals, 2), prediction.ExpectedAwayGoals);
        }

        [Fact]
        public void Predict_AppliesPositiveAndNegativeMomentumAdjustment_WhenMarkovIsValid()
        {
            var goalModel = new GoalModel(
            [
                Result("A", "B", 2, 0),
                Result("A", "B", 1, 0),
                Result("B", "A", 1, 2)
            ]);
            var model = new GoalPlusRecentContextModel(goalModel);

            var context = TestContext("A", "B");
            var startDate = DateTimeOffset.UtcNow.AddDays(-20);

            // Home (A): sequence W, W, D, L, L, W, D, W, L, W
            // Last match is W, transition from W: W->W (1), W->D (2), W->L (1).
            // P(W) = 0.25, P(D) = 0.50, P(L) = 0.25. (Home perspective)
            var homeHistory = new List<MatchResult>();
            var outcomes = new[] { (2, 0), (3, 0), (1, 1), (0, 2), (1, 3), (2, 1), (0, 0), (4, 2), (0, 1), (2, 0) };
            for (int i = 0; i < outcomes.Length; i++)
            {
                var match = Result("A", $"X{i}", outcomes[i].Item1, outcomes[i].Item2);
                match.Date = startDate.AddDays(i);
                homeHistory.Add(match);
            }
            context.HomeRecentMatchHistory = homeHistory;

            // Away (B): L, L, L, L, L (5 matches).
            // Last match is L, transition from L: L->L (4 times).
            // P(W) = 0.00, P(D) = 0.00, P(L) = 1.00. (Away perspective)
            var awayHistory = new List<MatchResult>();
            for (int i = 0; i < 5; i++)
            {
                var match = Result("B", $"Y{i}", 0, 2);
                match.Date = startDate.AddDays(i);
                awayHistory.Add(match);
            }
            context.AwayRecentMatchHistory = awayHistory;

            // Combined Markov results (from A's perspective):
            // Home Win (Home Win prob) = (A_Win + B_Loss) / 2.0 = (0.25 + 1.0) / 2.0 = 0.625
            // Draw = (A_Draw + B_Draw) / 2.0 = (0.50 + 0.0) / 2.0 = 0.25
            // Away Win (Away Win prob) = (A_Loss + B_Win) / 2.0 = (0.25 + 0.0) / 2.0 = 0.125
            // 
            // Multipliers:
            // Home multiplier: 1.0 + (0.625 - 0.3333) * 0.3 = 1.0 + 0.2917 * 0.3 = 1.0875. Clamped to [0.90, 1.10] -> 1.0875.
            // Away multiplier: 1.0 + (0.125 - 0.3333) * 0.3 = 1.0 - 0.2083 * 0.3 = 0.9375. Clamped to [0.90, 1.10] -> 0.9375.

            var (baseHomeGoals, baseAwayGoals, _) = goalModel.ExpectedGoals(context);
            var expectedHomeGoals = Math.Round(baseHomeGoals * 1.0875, 2);
            var expectedAwayGoals = Math.Round(baseAwayGoals * 0.9375, 2);

            var prediction = model.Predict(context);

            Assert.Equal(expectedHomeGoals, prediction.ExpectedHomeGoals);
            Assert.Equal(expectedAwayGoals, prediction.ExpectedAwayGoals);
            Assert.Contains("Inercia de Markov", prediction.FeaturesUsed);
            Assert.Contains(prediction.Drivers, d => d.Contains("Inercia de Markov aplicada"));
        }

        [Fact]
        public async Task Predict_IntegrationWithPredictionService_AppliesMarkovInertia()
        {
            await using var db = await NewDb();

            // Seed teams
            var homeTeam = new Team { Id = "T1", Name = "HomeTeam" };
            var awayTeam = new Team { Id = "T2", Name = "AwayTeam" };
            db.Teams.AddRange(homeTeam, awayTeam);

            // Seed historical results so that both teams have at least 5 matches
            // We want Home to have win momentum, Away to have loss momentum
            var startDate = DateTimeOffset.UtcNow.AddDays(-20);

            // Home (T1): W, W, W, W, W (5 matches) -> transition Win probability = 100%
            for (int i = 0; i < 5; i++)
            {
                db.Results.Add(new MatchResult
                {
                    Id = $"R1_{i}",
                    HomeTeamId = "T1",
                    AwayTeamId = "OpponentH",
                    HomeGoals = 2,
                    AwayGoals = 0,
                    Date = startDate.AddDays(i),
                    Tournament = "test",
                    Source = "test"
                });
            }

            // Away (T2): L, L, L, L, L (5 matches) -> transition Loss probability = 100%
            for (int i = 0; i < 5; i++)
            {
                db.Results.Add(new MatchResult
                {
                    Id = $"R2_{i}",
                    HomeTeamId = "OpponentA",
                    AwayTeamId = "T2",
                    HomeGoals = 2,
                    AwayGoals = 0,
                    Date = startDate.AddDays(i),
                    Tournament = "test",
                    Source = "test"
                });
            }

            // Also seed some general results for the GoalModel base calculation so it compiles/runs without degrading
            for (int i = 0; i < 10; i++)
            {
                db.Results.Add(new MatchResult
                {
                    Id = $"RG_{i}",
                    HomeTeamId = "T1",
                    AwayTeamId = "T2",
                    HomeGoals = 1,
                    AwayGoals = 1,
                    Date = startDate.AddDays(i),
                    Tournament = "test",
                    Source = "test"
                });
            }

            await db.SaveChangesAsync();

            var service = new PredictionService(db, SimulationOptions(1, 1));
            var predictionResult = await service.PredictPairAsync("T1", "T2");

            // Extract the prediction for "Goles + contexto reciente"
            var prediction = predictionResult.Predictions.Single(p => p.PredictorName == "Goles + contexto reciente");

            // Verify that the integration worked:
            // 1. The prediction is not degraded
            Assert.False(prediction.Degraded);
            // 2. The features used include "Inercia de Markov"
            Assert.Contains("Inercia de Markov", prediction.FeaturesUsed);
            // 3. The drivers indicate that Markov momentum was applied
            Assert.Contains(prediction.Drivers, d => d.Contains("Inercia de Markov aplicada"));
        }
    }
}
