using Oloraculo.Web.Models;
using Oloraculo.Web.Probability;

namespace Oloraculo.Web.Predictors
{
    public static class FinalPredictionSelector
    {
        private const double RankingBiasWeight = 0.15;
        private const string EloPredictorName = "Elo";
        private const string FifaPredictorName = "Ranking FIFA";

        public static MatchPrediction Select(IReadOnlyList<MatchPrediction> ladder)
        {
            if (ladder.Count == 0)
                return EmptyFinal();

            var ordered = ladder.OrderBy(p => p.PredictorPriority).ToList();
            var selected = ordered.LastOrDefault(p => !p.Degraded) ?? ordered.First();
            var skippedHigher = ordered
                .Where(p => p.PredictorPriority > selected.PredictorPriority && p.Degraded)
                .OrderByDescending(p => p.PredictorPriority)
                .ToList();
            var rankingBias = TryBuildRankingBias(ordered, selected);
            var baseOutcome = rankingBias?.Outcome ?? selected.Outcome;

            var momentumBias = TryBuildMomentumBias(ordered, selected, baseOutcome);

            var drivers = new List<string>
            {
                $"Seleccionó {selected.PredictorName} como el escalón usable más alto."
            };
            drivers.AddRange(skippedHigher.Select(p => $"Omitió {p.PredictorName}: {Reason(p)}"));
            drivers.AddRange(selected.Drivers);
            if (rankingBias is not null)
            {
                drivers.Add(
                    $"Aplicó una calibración Elo/FIFA de {RankingBiasWeight:P0} hacia {OutcomeLabel(rankingBias.ConsensusTopPick)} porque ambos modelos de ranking coincidieron contra {selected.PredictorName}.");
            }
            if (momentumBias is not null)
            {
                drivers.Add(
                    $"Aplicó un sesgo de momentum de 15% hacia {OutcomeLabel(momentumBias.Target)} para resolver el empate debido a inercia favorable de Markov.");
            }

            var sources = selected.Sources
                .Concat(rankingBias?.Sources ?? [])
                .Concat(momentumBias?.Sources ?? [])
                .Concat([new SourceMetadata("model ladder", "derived", Notes: selected.PredictorName)])
                .Distinct()
                .ToList();

            return new MatchPrediction
            {
                PredictorName = "Oráculo final",
                PredictorPriority = selected.PredictorPriority,
                FixtureId = selected.FixtureId,
                HomeTeamId = selected.HomeTeamId,
                AwayTeamId = selected.AwayTeamId,
                Outcome = momentumBias?.Outcome ?? baseOutcome,
                ExpectedHomeGoals = selected.ExpectedHomeGoals,
                ExpectedAwayGoals = selected.ExpectedAwayGoals,
                Scoreline = selected.Scoreline,
                MostLikelyScore = selected.MostLikelyScore,
                Explanation = BuildExplanation(selected, skippedHigher, rankingBias, momentumBias),
                Drivers = drivers,
                FeaturesUsed = selected.FeaturesUsed,
                FeaturesMissing = selected.FeaturesMissing,
                Sources = sources,
                Degraded = selected.Degraded
            };
        }

        private static RankingBias? TryBuildRankingBias(IReadOnlyList<MatchPrediction> ordered, MatchPrediction selected)
        {
            var elo = ordered.LastOrDefault(p => p.PredictorName == EloPredictorName && !p.Degraded);
            var fifa = ordered.LastOrDefault(p => p.PredictorName == FifaPredictorName && !p.Degraded);
            if (elo is null || fifa is null)
                return null;

            var consensusTopPick = elo.Outcome.TopPick;
            if (consensusTopPick != fifa.Outcome.TopPick || consensusTopPick == selected.Outcome.TopPick)
                return null;

            var consensus = new OutcomeProbabilities(
                (elo.Outcome.HomeWin + fifa.Outcome.HomeWin) / 2.0,
                (elo.Outcome.Draw + fifa.Outcome.Draw) / 2.0,
                (elo.Outcome.AwayWin + fifa.Outcome.AwayWin) / 2.0).Normalize();

            var selectedWeight = 1.0 - RankingBiasWeight;
            var outcome = new OutcomeProbabilities(
                selected.Outcome.HomeWin * selectedWeight + consensus.HomeWin * RankingBiasWeight,
                selected.Outcome.Draw * selectedWeight + consensus.Draw * RankingBiasWeight,
                selected.Outcome.AwayWin * selectedWeight + consensus.AwayWin * RankingBiasWeight).Normalize();

            return new RankingBias(
                outcome,
                consensusTopPick,
                elo.Sources.Concat(fifa.Sources).ToList());
        }

        private static MomentumBias? TryBuildMomentumBias(
            IReadOnlyList<MatchPrediction> ordered,
            MatchPrediction selected,
            OutcomeProbabilities baseOutcome)
        {
            if (baseOutcome.TopPick != "Draw")
                return null;

            var markov = ordered.LastOrDefault(p => p.PredictorName == "Inercia de Markov" && !p.Degraded);
            if (markov is null)
                return null;

            double homeWin = markov.Outcome.HomeWin;
            double awayWin = markov.Outcome.AwayWin;

            string? target = null;
            if (homeWin > 0.40 && awayWin < 0.25)
                target = "Home";
            else if (awayWin > 0.40 && homeWin < 0.25)
                target = "Away";

            if (target is null)
                return null;

            const double bias = 0.15;
            double h = baseOutcome.HomeWin;
            double d = baseOutcome.Draw;
            double a = baseOutcome.AwayWin;

            if (target == "Home")
            {
                h += bias;
                d -= bias;
            }
            else
            {
                a += bias;
                d -= bias;
            }

            var outcome = new OutcomeProbabilities(h, d, a).Normalize();
            return new MomentumBias(outcome, target, markov.Sources);
        }

        private static string BuildExplanation(
            MatchPrediction selected,
            IReadOnlyList<MatchPrediction> skippedHigher,
            RankingBias? rankingBias,
            MomentumBias? momentumBias)
        {
            var rankingSentence = rankingBias is null
                ? ""
                : $" Aplicó una calibración Elo/FIFA de {RankingBiasWeight:P0} hacia {OutcomeLabel(rankingBias.ConsensusTopPick)}.";

            var momentumSentence = momentumBias is null
                ? ""
                : $" Aplicó un sesgo de momentum de 15% hacia {OutcomeLabel(momentumBias.Target)} para resolver el empate debido a inercia favorable de Markov.";

            if (skippedHigher.Count == 0)
                return $"El Oráculo final seleccionó {selected.PredictorName}, el escalón usable más alto. {selected.Explanation}{rankingSentence}{momentumSentence}";

            var skipped = string.Join("; ", skippedHigher.Select(p => $"{p.PredictorName} {Reason(p)}"));
            return $"El Oráculo final seleccionó {selected.PredictorName} porque {skipped}. {selected.Explanation}{rankingSentence}{momentumSentence}";
        }

        private static string Reason(MatchPrediction prediction)
        {
            if (prediction.FeaturesMissing.Count == 0)
                return "no era usable";

            var missingVerb = prediction.FeaturesMissing.Count == 1 ? "faltaba" : "faltaban";
            return $"no era usable: {missingVerb} {string.Join(", ", prediction.FeaturesMissing)}";
        }

        private static MatchPrediction EmptyFinal() => new()
        {
            PredictorName = "Oráculo final",
            PredictorPriority = 0,
            Outcome = OutcomeProbabilities.Uniform,
            Explanation = "El Oráculo final no tenía predicciones de la escalera, así que devolvió la base.",
            Drivers = ["No había predicciones disponibles en la escalera."],
            FeaturesMissing = ["predicciones de la escalera"],
            Sources = [new SourceMetadata("model ladder", "derived")],
            Degraded = true
        };

        private static string OutcomeLabel(string outcome) => outcome switch
        {
            "Home" => "equipo A",
            "Away" => "equipo B",
            "Draw" => "empate",
            _ => outcome
        };

        private sealed record RankingBias(
            OutcomeProbabilities Outcome,
            string ConsensusTopPick,
            IReadOnlyList<SourceMetadata> Sources);

        private sealed record MomentumBias(
            OutcomeProbabilities Outcome,
            string Target,
            IReadOnlyList<SourceMetadata> Sources);
    }
}
