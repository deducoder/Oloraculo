using Oloraculo.Web.Models;
using Oloraculo.Web.Probability;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oloraculo.Web.Predictors
{
    public class MarkovMomentumPredictor : IPredictor
    {
        public string Name => "Inercia de Markov";
        public int Priority => 3;

        private enum MatchOutcome
        {
            Win = 0,
            Draw = 1,
            Loss = 2
        }

        public MatchPrediction Predict(MatchContext context)
        {
            var homeHistory = context.HomeRecentMatchHistory;
            var awayHistory = context.AwayRecentMatchHistory;

            var homeProb = GetTeamTransitionProbabilities(homeHistory, context.HomeTeam.Id);
            var awayProb = GetTeamTransitionProbabilities(awayHistory, context.AwayTeam.Id);

            // Symmetrical merging:
            // Home Win = (Home_W + Away_L) / 2.0
            // Draw = (Home_D + Away_D) / 2.0
            // Away Win = (Home_L + Away_W) / 2.0
            double homeWin = (homeProb.HomeWin + awayProb.AwayWin) / 2.0;
            double draw = (homeProb.Draw + awayProb.Draw) / 2.0;
            double awayWin = (homeProb.AwayWin + awayProb.HomeWin) / 2.0;

            var outcome = new OutcomeProbabilities(homeWin, draw, awayWin).Normalize();

            bool degraded = homeHistory.Count < 5 || awayHistory.Count < 5 ||
                            IsUniformFallback(homeHistory, context.HomeTeam.Id) ||
                            IsUniformFallback(awayHistory, context.AwayTeam.Id);

            string explanation = degraded
                ? "Inercia de Markov degradada debido a datos insuficientes o estado no visitado."
                : $"Inercia de Markov calculada. Historial local: {homeHistory.Count} partidos, visita: {awayHistory.Count} partidos.";

            return new MatchPrediction
            {
                PredictorName = Name,
                PredictorPriority = Priority,
                FixtureId = context.Fixture.Id,
                HomeTeamId = context.HomeTeam.Id,
                AwayTeamId = context.AwayTeam.Id,
                Outcome = outcome,
                ExpectedHomeGoals = null,
                ExpectedAwayGoals = null,
                Scoreline = null,
                MostLikelyScore = null,
                Explanation = explanation,
                Drivers = new[] { "Inercia de Markov" },
                FeaturesUsed = new[] { "Resultados recientes" },
                FeaturesMissing = (homeHistory.Count < 5 || awayHistory.Count < 5) ? new[] { "historial de partidos suficiente" } : Array.Empty<string>(),
                Sources = new[] { SourceMetadata.HistoricalResultsCsv },
                Degraded = degraded
            };
        }

        private OutcomeProbabilities GetTeamTransitionProbabilities(IReadOnlyList<MatchResult> history, string teamId)
        {
            if (history == null || history.Count < 5)
            {
                return OutcomeProbabilities.Uniform;
            }

            var chronologicalMatches = history.OrderBy(m => m.Date).ToList();
            var outcomes = new List<MatchOutcome>();
            foreach (var match in chronologicalMatches)
            {
                var goalsFor = match.HomeTeamId == teamId ? match.HomeGoals : match.AwayGoals;
                var goalsAgainst = match.HomeTeamId == teamId ? match.AwayGoals : match.HomeGoals;
                if (goalsFor > goalsAgainst)
                    outcomes.Add(MatchOutcome.Win);
                else if (goalsFor == goalsAgainst)
                    outcomes.Add(MatchOutcome.Draw);
                else
                    outcomes.Add(MatchOutcome.Loss);
            }

            // Transition counts matrix: [previous, next]
            var counts = new int[3, 3];
            for (int i = 0; i < outcomes.Count - 1; i++)
            {
                var prev = outcomes[i];
                var next = outcomes[i + 1];
                counts[(int)prev, (int)next]++;
            }

            var currentState = outcomes.Last();
            int row = (int)currentState;
            int totalTransitions = counts[row, 0] + counts[row, 1] + counts[row, 2];

            if (totalTransitions == 0)
            {
                return OutcomeProbabilities.Uniform;
            }

            // Return OutcomeProbabilities from team's perspective:
            // HomeWin represents Win, Draw represents Draw, AwayWin represents Loss
            double pWin = (double)counts[row, 0] / totalTransitions;
            double pDraw = (double)counts[row, 1] / totalTransitions;
            double pLoss = (double)counts[row, 2] / totalTransitions;

            return new OutcomeProbabilities(pWin, pDraw, pLoss);
        }

        private bool IsUniformFallback(IReadOnlyList<MatchResult> history, string teamId)
        {
            if (history == null || history.Count < 5)
                return true;

            var chronologicalMatches = history.OrderBy(m => m.Date).ToList();
            var outcomes = new List<MatchOutcome>();
            foreach (var match in chronologicalMatches)
            {
                var goalsFor = match.HomeTeamId == teamId ? match.HomeGoals : match.AwayGoals;
                var goalsAgainst = match.HomeTeamId == teamId ? match.AwayGoals : match.HomeGoals;
                if (goalsFor > goalsAgainst)
                    outcomes.Add(MatchOutcome.Win);
                else if (goalsFor == goalsAgainst)
                    outcomes.Add(MatchOutcome.Draw);
                else
                    outcomes.Add(MatchOutcome.Loss);
            }

            var counts = new int[3, 3];
            for (int i = 0; i < outcomes.Count - 1; i++)
            {
                counts[(int)outcomes[i], (int)outcomes[i + 1]]++;
            }

            var currentState = outcomes.Last();
            int row = (int)currentState;
            int totalTransitions = counts[row, 0] + counts[row, 1] + counts[row, 2];
            return totalTransitions == 0;
        }
    }
}
