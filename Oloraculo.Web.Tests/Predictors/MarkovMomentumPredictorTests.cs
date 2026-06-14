using Xunit;
using Oloraculo.Web.Models;
using Oloraculo.Web.Predictors;
using Oloraculo.Web.Probability;
using System.Collections.Generic;

namespace Oloraculo.Web.Tests
{
    public class MarkovMomentumPredictorTests : TestFixtures
    {
        [Fact]
        public void Predict_ReturnsUniform_WhenHistoryIsEmpty()
        {
            var predictor = new MarkovMomentumPredictor();
            var context = TestContext("A", "B");
            context.HomeRecentMatchHistory = new List<MatchResult>();
            context.AwayRecentMatchHistory = new List<MatchResult>();

            var prediction = predictor.Predict(context);

            Assert.True(prediction.Degraded);
            Assert.Equal(OutcomeProbabilities.Uniform.HomeWin, prediction.Outcome.HomeWin, 5);
            Assert.Equal(OutcomeProbabilities.Uniform.Draw, prediction.Outcome.Draw, 5);
            Assert.Equal(OutcomeProbabilities.Uniform.AwayWin, prediction.Outcome.AwayWin, 5);
        }

        [Fact]
        public void Predict_ReturnsUniform_WhenHistoryIsTooShort()
        {
            var predictor = new MarkovMomentumPredictor();
            var context = TestContext("A", "B");

            // 4 matches is too short (< 5)
            context.HomeRecentMatchHistory = new List<MatchResult>
            {
                Result("A", "X", 2, 0),
                Result("A", "X", 1, 0),
                Result("A", "X", 3, 0),
                Result("A", "X", 0, 1)
            };
            context.AwayRecentMatchHistory = new List<MatchResult>
            {
                Result("B", "Y", 1, 0),
                Result("B", "Y", 1, 1),
                Result("B", "Y", 0, 2),
                Result("B", "Y", 2, 1)
            };

            var prediction = predictor.Predict(context);

            Assert.True(prediction.Degraded);
            Assert.Equal(OutcomeProbabilities.Uniform.HomeWin, prediction.Outcome.HomeWin, 5);
        }

        [Fact]
        public void Predict_ReturnsExpectedProbabilities_WhenHistoryIsValid()
        {
            var predictor = new MarkovMomentumPredictor();
            var context = TestContext("A", "B");

            // Home history (newest to oldest or oldest to newest: order in code should sort by Date)
            // Let's create a history where transition from Win:
            // W -> W (1 time), W -> D (2 times), W -> L (1 time). Current state (last match): W.
            // Under W state, P(W) = 1/4 = 0.25, P(D) = 2/4 = 0.50, P(L) = 1/4 = 0.25.
            var homeHistory = new List<MatchResult>();
            var startDate = DateTimeOffset.UtcNow.AddDays(-20);

            // Sequence: W, W, D, L, L, W, D, W, L, W (10 matches)
            // Outcomes in chronological order:
            // 1. W (A 2 - X 0)
            // 2. W (A 3 - X 0)  [W -> W]
            // 3. D (A 1 - X 1)  [W -> D]
            // 4. L (A 0 - X 2)  [D -> L]
            // 5. L (A 1 - X 3)  [L -> L]
            // 6. W (A 2 - X 1)  [L -> W]
            // 7. D (A 0 - X 0)  [W -> D]
            // 8. W (A 4 - X 2)  [D -> W]
            // 9. L (A 0 - X 1)  [W -> L]
            // 10. W (A 2 - X 0) [L -> W] (Current State = W)
            var outcomes = new[] { (2, 0), (3, 0), (1, 1), (0, 2), (1, 3), (2, 1), (0, 0), (4, 2), (0, 1), (2, 0) };
            for (int i = 0; i < outcomes.Length; i++)
            {
                var match = Result("A", $"X{i}", outcomes[i].Item1, outcomes[i].Item2);
                match.Date = startDate.AddDays(i);
                homeHistory.Add(match);
            }
            context.HomeRecentMatchHistory = homeHistory;

            // Away history (B)
            // Let's make Away history such that current state is Loss (L).
            // Let's make it simple: L, L, L, L, L (5 matches)
            // Transitions from L: L -> L (4 times). Current state = L.
            // P(W) = 0%, P(D) = 0%, P(L) = 100%. (From B's perspective)
            // Note: from B's perspective: Win = B win, Loss = B loss.
            // Since last match was L, B's next state probabilities are: P(W)=0, P(D)=0, P(L)=1.0
            var awayHistory = new List<MatchResult>();
            for (int i = 0; i < 5; i++)
            {
                var match = Result("B", $"Y{i}", 0, 2); // B loses 0-2
                match.Date = startDate.AddDays(i);
                awayHistory.Add(match);
            }
            context.AwayRecentMatchHistory = awayHistory;

            var prediction = predictor.Predict(context);

            // Let's verify combined math:
            // Home (A) transition probabilities (from A's perspective):
            // - Win: 0.25
            // - Draw: 0.50
            // - Loss: 0.25
            // Away (B) transition probabilities (from B's perspective):
            // - Win: 0.00
            // - Draw: 0.00
            // - Loss: 1.00
            // Combined outcome:
            // - Home Win = (A_Win + B_Loss) / 2.0 = (0.25 + 1.0) / 2.0 = 0.625
            // - Draw = (A_Draw + B_Draw) / 2.0 = (0.50 + 0.0) / 2.0 = 0.25
            // - Away Win = (A_Loss + B_Win) / 2.0 = (0.25 + 0.0) / 2.0 = 0.125
            // Total = 0.625 + 0.25 + 0.125 = 1.0 (already normalized)

            Assert.False(prediction.Degraded);
            Assert.Equal(0.625, prediction.Outcome.HomeWin, 5);
            Assert.Equal(0.25, prediction.Outcome.Draw, 5);
            Assert.Equal(0.125, prediction.Outcome.AwayWin, 5);
        }

        [Fact]
        public void Predict_ReturnsUniform_WhenStateHasNoTransitions()
        {
            var predictor = new MarkovMomentumPredictor();
            var context = TestContext("A", "B");

            // Home history: W, W, W, W, D (5 matches)
            // Transitions: W -> W (3 times), W -> D (1 time).
            // Current state: D (last match). But there are no transitions starting from D in history!
            var homeHistory = new List<MatchResult>();
            var startDate = DateTimeOffset.UtcNow.AddDays(-20);
            var outcomes = new[] { (2, 0), (3, 0), (1, 0), (2, 0), (1, 1) };
            for (int i = 0; i < outcomes.Length; i++)
            {
                var match = Result("A", $"X{i}", outcomes[i].Item1, outcomes[i].Item2);
                match.Date = startDate.AddDays(i);
                homeHistory.Add(match);
            }
            context.HomeRecentMatchHistory = homeHistory;
            context.AwayRecentMatchHistory = new List<MatchResult>(); // Away is empty -> fallback to uniform

            var prediction = predictor.Predict(context);

            // Home falls back to uniform because D has no transition. Away is empty so falls back to uniform.
            // Combined is uniform.
            Assert.True(prediction.Degraded);
            Assert.Equal(OutcomeProbabilities.Uniform.HomeWin, prediction.Outcome.HomeWin, 5);
        }
    }
}
