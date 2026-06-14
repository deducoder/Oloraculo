---
epic_id: "E6"
title: "E6: markov-momentum brief"
status: "active"
created: "2026-06-14"
---

# Epic Brief: E6: markov-momentum brief

## Hypothesis
For football analysts who need explainable momentum-based predictions, the Markov Momentum Predictor is a transition-matrix model that calculates the probability of team performance states based on sequential wins, draws, and losses. Unlike recent form averages, this solution models state-transition probability directly to capture true sequence inertia.

## Success Metrics
- **Leading:** Transition matrix calculation compiles and passes validation with test vectors.
- **Lagging:** Unified final selector includes Momentum Bias to resolve close predictions, improving historical prediction Brier score.

## Appetite
S — 3 stories

## Scope Boundaries
### In (MUST)
- `MarkovMomentumPredictor.cs` implementing `IPredictor` interface.
- 1st-order Markov chain (W-D-L transitions) using last 10-15 matches.
- Multiplier of +-10% xG in `GoalPlusRecentContextModel.cs` based on state probabilities.
- Momentum Bias shift in `FinalPredictionSelector.cs`.

### In (SHOULD)
- Compare predictor output in `/lab` page.

### No-Gos
- Higher-order Markov chains (2nd-order or 3rd-order) which would overfit small datasets.
- Direct Poisson replacement (it should remain an adjustment factor).

### Rabbit Holes
- Building complex time-decay factorings into the state history (stick to simple raw counts).
