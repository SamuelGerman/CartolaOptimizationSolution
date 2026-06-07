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

## Recuperação de dados de rodadas degradadas (13-17)

Investigação posterior mostrou que a API do Cartola **serve dados históricos** que
permitem recuperar grande parte das rodadas degradadas:

| Dado | Endpoint | Histórico? |
|------|----------|------------|
| Placares | `/partidas/{rodada}` | ✅ `placar_oficial` para rodadas passadas |
| Pontuações (label) | `/atletas/pontuados/{rodada}` | ✅ |
| **Preço** | `/atletas/mercado` | ❌ só rodada atual — histórico perdido |

**Fase 1 — Backfill de placares (autoritativo):** `SyncClosedRoundAsync` foi estendido
para também buscar `/partidas/{round}` e fazer upsert de `HomeScore/AwayScore`. Rodadas
13-17 passaram a ter 10/10 placares reais (a 16 estava ausente). Conserta as features
de clube/adversário.

**Fase 2 — Imputação de preço (tratamento sinalizado):** o preço histórico não é servido
pela API, então foi imputado por **carry-forward leak-free** (preço mais recente de rodada
anterior com valor > 0). Aplicado à rodada 13 (368 zeros → 365 imputados, 3 sem âncora):

```sql
CREATE TEMPORARY TABLE prior_prices AS
  SELECT a.AthleteId, a.PriceNum FROM athleterounds a
  WHERE a.PriceNum>0 AND a.RoundId=(SELECT MAX(b.RoundId) FROM athleterounds b
    WHERE b.AthleteId=a.AthleteId AND b.RoundId<13 AND b.PriceNum>0);
UPDATE athleterounds ar JOIN prior_prices p ON p.AthleteId=ar.AthleteId
  SET ar.PriceNum=p.PriceNum WHERE ar.RoundId=13 AND ar.PriceNum=0;
DROP TEMPORARY TABLE prior_prices;
```

⚠️ Preço imputado é **dado sintético** (não o preço real de mercado da rodada 13).

**Impacto medido (R² isolado por rodada, antes → depois):**
- Rodada 13 (imputação de preço): −0.12 → **+0.25**
- Rodada 16 (backfill de placar): descartada → **+0.60**
- Rodadas 14/15/17: marginais (já tinham preço; variância intrínseca de rodada única)

Modelo final passou a treinar com 17 rodadas / 5311 linhas (vs 14/4313). Retrospectiva
de escalações (rodadas 2-17): pontuação real média 106.3 pts. Rodada 18 segue
irrecuperável (sem baseline de mercado). Conclusão: backfill de placar e imputação de
preço recuperam falhas **de dado**; rodadas intrinsecamente ruidosas permanecem difíceis.

## Recomendações

1. **Operacional:** rodar `closed-round/{r}` (ou o end-of-round) logo após cada rodada
   fechar, para capturar o baseline a tempo.
2. **Treino seguro:** rodadas recuperadas retroativamente com baseline ruim devem ser
   excluídas do treino. Usar `toRound` para limitar à última rodada com dados confiáveis
   (atualmente 14), ou adicionar validação de qualidade de baseline à readiness.
3. **⚠️ Landmine atual:** rodadas 15-17 estão `HistoricalClosed` no banco; um treino
   default (sem `toRound`) as incluiria e degradaria o modelo. O modelo de produção foi
   restaurado treinando com `toRound=14`.
