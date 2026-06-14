# Epic Retrospective: E6 Markov Momentum Prediction

**Completed:** 2026-06-14
**Duration:** 1 day (started 2026-06-14)
**Stories:** 3 stories delivered

---

## Summary

Se diseñó e implementó un predictor basado en cadenas de Markov de primer orden para capturar la inercia y el momentum (secuencias de victorias, empates y derrotas) de los equipos. El modelo se integró en la capa de ajuste de goles esperados (xG) y en el selector final para resolver empates técnicos cerrados mediante un sesgo probabilístico del 15% (Momentum Bias), mejorando la precisión decisiva general del Oráculo.

## Metrics

| Metric | Value | Notes |
|--------|-------|-------|
| Stories Delivered | 3 | S6.1, S6.2, S6.3 |
| Story Points | 3 SP | 1 SP por historia |
| Tests Added | 14 | Pruebas unitarias de Markov, xG y sesgo de momentum |
| Average Velocity | 1.0x | Consistente con estimaciones |
| Calendar Days | 1 | |

### Story Breakdown

| Story | Size | SP | Velocity | Key Learning |
|-------|:----:|:--:|:--------:|--------------|
| S6.1 | S | 1 | 1.0x | El cálculo simétrico de transiciones cruzadas (ej. Home_W + Away_L) modela el partido de forma neutra y robusta. |
| S6.2 | S | 1 | 1.0x | Ajustar xG hasta un máximo de +-10% en Dixon-Coles permite modelar inercia sin desestabilizar la calibración base. |
| S6.3 | S | 1 | 1.0x | El orden de registro en colecciones es crítico cuando dos predictores comparten prioridad, requiriendo que el principal (RecentFormModel) preceda al auxiliar (Markov). |

## What Went Well

- La modularidad del diseño de Markov permitió integrarlo como un predictor auxiliar (Inercia de Markov) y utilizar sus outputs tanto en la modificación de xG como en el selector final.
- Los tests de cobertura unitarios e integrados validan casos de borde de asimetría de forma exhaustiva.
- Sincronización exitosa entre `PredictionService` y `SimulationPredictionContext` asegurando la coherencia en simulaciones.

## What Could Be Improved

- La duplicación de los registros de los modelos en `PredictionService` y `SimulationPredictionContext` representa un riesgo de deriva técnica. En futuros epics, se debería centralizar el registro en una clase común de configuración.

## Patterns Discovered

| ID | Pattern | Context |
|----|---------|---------|
| PAT-DEDU-38 | Sincronización de registros de predictores | Al registrar nuevos predictores, duplicar el registro en la clase de simulación para mantener paridad de lógica. |

## Process Insights

- La metodología RaiSE permitió dividir una funcionalidad compleja en tres historias completamente independientes y modulares (S6.1 base, S6.2 xG modifier, S6.3 selector bias), facilitando su implementación ordenada y robusta.

## Artifacts

- **Scope:** `work/epics/e6-markov-momentum/scope.md`
- **Stories:** `work/epics/e6-markov-momentum/stories/`
- **ADRs:** ADR-006 (Markov Transition modeling)
- **Tests:** `PredictorTests.cs`, `GoalPlusRecentContextModelTests.cs`, `SimulationServiceTests.cs`

## Next Steps

- Monitorear la calibración histórica general y el impacto del sesgo del 15% sobre las predicciones de empates.
- Refactorizar las colecciones de predictores para compartir una única fuente de verdad en el registro.
