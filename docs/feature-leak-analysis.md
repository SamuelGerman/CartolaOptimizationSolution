# Análise de Vazamento Temporal nas Features do GatoMestre

> Estudo de ablação controlada que identificou, quantificou e corrigiu um vazamento
> temporal do alvo (*target leakage*) no modelo de predição de pontuação de atletas.

## Contexto

O modelo `athlete-score-v1-full-context` apresentava um paradoxo:

- **Holdout (walk-forward CV):** R² ≈ 0,87 — aparentemente excelente.
- **Uso real (escalações):** erro médio de **−82 pts** por rodada — catastrófico.

Essa divergência entre métrica de validação e desempenho real é a assinatura clássica
de *train/serve skew* causado por uma feature com vazamento.

## Metodologia

Estudo de ablação com **variável única**: mesmo trainer (LightGBM), mesmos
hiperparâmetros (300 iter, LR 0,05, 63 folhas) e mesma validação walk-forward
(5 folds, rodadas 10–14). Apenas o **conjunto de features** muda entre os braços.

As 262 features numéricas do `FullContextAthleteScoreTrainingRow` têm 3 origens:

| Origem | Nº features | Tabelas |
|--------|------------|---------|
| Cartola (atleta + partidas + engenharia local) | 231 | `athleterounds`, `athleteroundscouts`, `matches` |
| GatoMestre `*Previous` (valor até rodada N−1) | 10 | `athleteroundcontexts` |
| GatoMestre `*Current`/`Index` (inclui rodada N) | 21 | `athleteroundcontexts` |

## Experimento 1 — Ablação por tier

| Tier | Features | R² (CV) | MAE (CV) |
|------|----------|---------|----------|
| A) Cartola cru | 10 | 0,120 | 2,68 |
| B) Cartola + engenharia | 231 | 0,262 | 2,32 |
| C) Full (B + GatoMestre) | 262 | **0,875** | 0,89 |

221 features engenheiradas sobre Cartola mal moveram o R² (0,12→0,26), mas
31 features do GatoMestre dispararam para 0,87 — resultado fisicamente implausível
que motivou a investigação de vazamento.

## Evidência direta (SQL)

Inspeção de `GrossOverallMovingAverageCurrent` por rodada revelou:

- `…Previous` na rodada N **=** `…Current` na rodada N−1 (média defasada).
- Quando o atleta **não joga** (DNP), `Current` **=** `Previous` (a média não muda).
- Quando o atleta joga, `Current` incorpora o score da rodada N.

**Conclusão:** `…Current` da rodada N **inclui o resultado da própria rodada N** —
vazamento direto do alvo.

## Experimento 2 — Diagnóstico do vazamento

Separando as features GatoMestre em `Previous` (limpas) vs `Current` (suspeitas):

| Braço | Features | R² (CV) | Ganho de R² |
|-------|----------|---------|-------------|
| B) Cartola engenheirado | 231 | 0,262 | *(base)* |
| D) B + GatoMestre **Previous** | 241 | 0,255 | **−0,007** |
| E) B + GatoMestre **Current** | 252 | 0,764 | **+0,502** |
| C) Full | 262 | 0,875 | — |

As features `Previous` (comprovadamente livres de vazamento) **não agregam nada**.
Todo o salto de R² vem das features `Current`, que contêm o resultado da rodada.
**Vazamento confirmado.**

## Correção

Todas as 31 features do GatoMestre foram excluídas do pipeline
(`FullContextAthleteScoreModelTrainingService.GetNumericFeatureNames`), tornando o
modelo **puro-Cartola**. Justificativa:

- As `Current`/`Index` vazam o alvo → devem ser removidas.
- As `Previous` são limpas mas não agregam valor (−0,007) → removidas por simplicidade.
- **Bônus operacional:** elimina a dependência do sync do GatoMestre (lento, frágil, sujeito a timeouts).

## Validação no mundo real (retrospectiva de escalações, 130 cartoletas)

| Métrica | Modelo vazado | Modelo honesto | Mudança |
|---------|---------------|----------------|---------|
| MAE da escalação | 82,5 pts | **42,5 pts** | −48% |
| **Pontuação REAL média** | **61,3 pts** | **87,6 pts** | **+43%** |

A remoção do vazamento não apenas corrigiu as métricas — **melhorou as escolhas reais**:
as escalações passaram a marcar +26 pts reais por rodada. O vazamento fazia o otimizador
priorizar quem **já havia** pontuado, não quem **iria** pontuar.

## Conclusão

- A capacidade preditiva **real e honesta** do modelo é R² ≈ 0,26 (não 0,87).
- Prever pontuação de fantasy uma rodada à frente é genuinamente difícil (alta variância);
  o vazamento mascarava essa dificuldade.
- **Resíduo restante:** o modelo honesto ainda superprevê devido a atletas que não entram
  em campo (DNP). Próximo passo: aplicar a probabilidade de participação
  (`ComputePlayProbability`, já calculada) como multiplicador do score.
