---
epic_id: "E6"
title: "E6: markov-momentum scope"
status: "active"
created: "2026-06-14"
---

# Epic E6: Markov Momentum Prediction — Scope

> **Status:** IN PROGRESS
> **Created:** 2026-06-14

## Objective
Enhance match prediction accuracy and explainability by introducing a sequential 1st-order Markov chain predictor that models team momentum (W-D-L inertia), adjusts expected goals (xG), and resolves close draw predictions in the final selector.

**Value:** Unlocks sequential performance analysis that traditional rolling-average form predictors miss.

## Stories

| ID | Story | Size | Status | Description |
|----|-------|:----:|:------:|-------------|
| S6.1 | Markov Predictor Foundation | S | Completed | Implement MarkovMomentumPredictor.cs and unit tests |
| S6.2 | Inertia xG Modifier | S | Completed | Integrate Markov probabilities as +-10% adjustments in GoalPlusRecentContextModel.cs |
| S6.3 | Selector Momentum Bias | S | Pending | Implement Momentum Bias in FinalPredictionSelector.cs to resolve close technical draws |

**Total:** 3 stories, 3 SP

## Scope

**In scope (MUST):**
- MarkovMomentumPredictor class conforming to IPredictor.
- Transition matrix mapping for W, D, L states based on recent team matches.
- Expected goals (xG) modification of +-10% in GoalPlusRecentContextModel.
- Selector logic to adjust probabilities when away team is in collapse or home team has momentum.

**Out of scope:**
- Multi-step ahead Markov forecasting.
- Replacing the baseline Elo model entirely.

## Done Criteria

**Per story:**
- [ ] Code compiles and passes all dotnet tests
- [ ] Code adheres to nullable contexts
- [ ] Tests verify core behaviors

**Epic complete:**
- [ ] All stories complete (S6.1–S6.3)
- [ ] Predictor integrated into the overall Oloraculo selection process
- [ ] Epic retrospective done
- [ ] Merged to main

## Dependencies

```
S6.1 (foundation)
  ↓
S6.2 (xG modifier)
  ↓
S6.3 (selector bias)
```

**External:** None

## Architecture

| Decision | ADR | Summary |
|----------|-----|---------|
| Markov Transition | ADR-006 | Model 1st-order transition probabilities from W-D-L sequences |

## Risks

| Risk | L/I | Mitigation |
|------|:---:|------------|
| Small sample size for transition matrix | H/M | Fall back to uniform probabilities if recent match count is less than 5 |

## Parking Lot
- Integration into historical backtesting run -> Post-release improvement
