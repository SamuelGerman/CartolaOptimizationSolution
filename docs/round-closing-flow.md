# Fluxo de fechamento de rodada só-Cartola

> Como finalizar uma rodada fechada usando apenas dados do Cartola (sem GatoMestre,
> sem retreino), e por que a recuperação retroativa de rodadas antigas é limitada.

## Endpoint

`POST /api/cartola/sync/closed-round/{round}`

Expõe `ICartolaSyncService.SyncClosedRoundAsync(round)`:
1. Valida que `round < currentRound` (senão erro).
2. Busca o payload de pontuação final via `GetAthleteScoresAsync(round)`.
3. Aplica os scores aos baselines de atleta já persistidos (`FinalizeClosedRoundAthleteResultsAsync`).
4. Seta `DataPhase = HistoricalClosed` e recomputa a readiness.

**Tudo só-Cartola.** Não chama GatoMestre nem treina. É a versão enxuta do passo
"Finalize closed Cartola rounds" que vive dentro do `EndOfRoundMaintenanceService`
(que acopla GatoMestre + treino).

## Quando usar

**Tempestivamente, ao fim de cada rodada** — enquanto o baseline (preço, mercado,
partidas) ainda reflete o estado pré-rodada correto. Nesse caso a rodada entra no
treino com qualidade.

## Limite: recuperação retroativa não resgata baseline

Finalizar os *scores* de uma rodada que passou **sem sync tempestivo** NÃO recupera a
qualidade do baseline. Caso real (rodadas 15-17):

| toRound | Rodadas | R² (CV) |
|---------|---------|---------|
| 14 (limpo) | 14 | **0.305** |
| 15 | 15 | −0.22 |
| 16 | 15 (16 descartada) | −0.22 |
| 17 | 16 | −0.20 |

- O endpoint fechou 15-17 corretamente (DataPhase + scores reais).
- Mas os **baselines** dessas rodadas foram capturados de forma inconsistente quando
  elapsaram sem sync. Como o modelo é dominado pelo preço (`PriceDeltaVsHistoricalAverage`,
  PFI #1), baselines de preço não-confiáveis **envenenam** o treino → R² colapsa.
- Rodada 16: partidas ausentes → linhas descartadas pelo resolver.
- Rodada 18: baseline nunca capturado → **irrecuperável** (`SyncClosedRoundAsync` só
  finaliza linhas existentes, não cria).

## Recomendações

1. **Operacional:** rodar `closed-round/{r}` (ou o end-of-round) logo após cada rodada
   fechar, para capturar o baseline a tempo.
2. **Treino seguro:** rodadas recuperadas retroativamente com baseline ruim devem ser
   excluídas do treino. Usar `toRound` para limitar à última rodada com dados confiáveis
   (atualmente 14), ou adicionar validação de qualidade de baseline à readiness.
3. **⚠️ Landmine atual:** rodadas 15-17 estão `HistoricalClosed` no banco; um treino
   default (sem `toRound`) as incluiria e degradaria o modelo. O modelo de produção foi
   restaurado treinando com `toRound=14`.
