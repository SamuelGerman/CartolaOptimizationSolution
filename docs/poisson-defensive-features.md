# Features Poisson para pontuação defensiva — resultado negativo

> Teste de adicionar um modelo de Poisson (probabilidade de clean sheet + gols esperados
> sofridos) como features para melhorar a previsão de defensores. **Conclusão: não agrega —
> o modelo de árvore já captura o sinal implicitamente.**

## Hipótese

A pontuação defensiva do Cartola depende fortemente do clean sheet (SG +5) e gols sofridos
(GS −1), que são contagens — domínio natural da distribuição de Poisson. Modelar os gols do
adversário como Poisson(λ) daria `P(clean sheet) = e^(−λ)` diretamente, potencialmente
melhorando a previsão de GOL/ZAG/LAT (posições mais fracas do modelo honesto).

## Etapa 1 — Poisson standalone vs baselines (walk-forward, rodadas 6–17)

λ_conceded = matchup ataque-adversário × defesa-clube (com shrinkage K≈7, leak-free).

| Métrica | global | club-hist | **poisson** |
|---|---|---|---|
| Clean sheet (Brier ↓) | 0.212 | 0.222 | **0.196 (−8%)** |
| Gols sofridos (MAE ↓) | 0.895 | 0.904 | **0.868 (−3%)** |
| Pts defensivos (MAE) | 2.556 | — | 2.556 (igual) |
| Pts defensivos (viés) | −0.503 | — | **−0.230** |

**O Poisson melhora calibração (Brier −8%) e viés (−54%), mas NÃO o MAE em pontos** — porque
o clean sheet de uma partida é evento de variância irredutível (+5 ou 0).

## Etapa 2 — Integrado como features no modelo (ablação com/sem)

Adicionadas `ExpectedGoalsConcededPoisson` (λ) e `CleanSheetProbabilityPoisson` (e^−λ) ao
`FullContextAthleteScoreTrainingRow`, computadas no resolver a partir de
`OpponentLocal.GoalsForPerMatchLast5` e `ClubLocal.GoalsAgainstPerMatchLast5`. Mesmo
LightGBM + walk-forward (5 folds).

| Medição | Resultado |
|---|---|
| PFI rank (de 233) | CleanSheet **153**, λ **166** → fora do top-10, NÃO selecionadas |
| MAE geral | 2.323 → 2.357 (pior) |
| MAE defensivo (GOL/LAT/ZAG) | 2.602 → 2.644 (pior em todas) |

## Conclusão

As features Poisson são **redundantes e levemente prejudiciais**. O LightGBM já possui os
ingredientes do matchup (`OpponentGoalsForPerMatch`, `ClubGoalsAgainstPerMatch`,
`OpponentCleanSheetRate`, `ClubDefenseStrength`, etc.) e modela a interação por conta própria;
o transform explícito não adiciona informação e introduz ruído correlacionado.

**Decisão: NÃO integradas** (revertidas). Resultado metodológico válido: *engenharia de
feature baseada em conhecimento de domínio (Poisson) não supera o que o gradient boosting já
extrai dos componentes brutos quando estes já estão presentes.* O valor do Poisson ficaria em
calibração/ranking/risco (ex.: escolha de capitão), não em reduzir o erro de pontuação.
