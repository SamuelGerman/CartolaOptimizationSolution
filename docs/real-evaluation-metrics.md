# Métricas de avaliação REAIS (out-of-sample)

> "REAL" = assertividade medida em dados que **não** estavam no conjunto de treino.
> Atualizado em 2026-09-29 (modelo `athlete-score-v1-full-context`, 28 rodadas / 8.797 linhas).

Há duas camadas independentes de avaliação out-of-sample, ambas honestas.

## Camada 1 — Validação walk-forward da API (holdout das últimas 5 rodadas)

O treino separa as últimas 5 rodadas como holdout; o modelo **nunca vê os labels
dessas rodadas**. Métrica por atleta (não por escalação):

| Métrica | Valor |
|---|---|
| MAE global | **2.97** |
| RMSE global | **4.00** |
| R² global | **0.050** |
| Linhas de validação | 1.644 (5 rodadas distintas) |

Por posição (validação out-of-sample):

| Pos | Linhas | MAE | RMSE | Viés |
|-----|-------:|----:|-----:|-----:|
| GOL | 21 | 3.54 | 5.31 | -1.67 |
| LAT | 53 | 2.92 | 3.95 | -0.90 |
| ZAG | 50 | 3.11 | 4.06 | -1.52 |
| MEI | 102 | 2.58 | 3.52 | -0.17 |
| ATA | 90 | 3.18 | 4.56 | -0.52 |
| TEC | 20 | 2.10 | 2.60 | -0.29 |

- Meio-campo e técnico são os mais previsíveis; goleiro o menos.
- **Viés negativo em todas as posições** → o modelo tende a subestimar levemente
  (é conservador). Coerente com escalações que pontuam acima do previsto.

## Camada 2 — Backtest prospectivo da escalação inteira (pipeline real)

Cada escalação foi gerada **antes** da rodada acontecer, por um modelo treinado
apenas em rodadas anteriores. É o teste mais próximo do uso real (12 jogadores):

| Rodada | Previsto | Real | Erro | Modelo (treino até) |
|-------:|---------:|-----:|-----:|---------------------|
| 25 | 58.62 | 56.45 | -2.17 | 2026-08-29 (≤24) |
| 27 | 56.01 | 47.20 | -8.81 | 2026-08-29 (≤26) |
| 28 | 56.50 | 59.56 | +3.06 | 2026-09-19 (≤27) |

- **Erro absoluto médio do total da escalação: 4.68 pts** (sobre ~56 previstos ≈ 8%).
- MAE por atleta na rodada 28 (out-of-sample): **3.52** — em linha com a Camada 1.
- Rodada 27 foi o pior caso (vários prováveis pontuaram ~1.0); rodada 28 o time
  superou a previsão puxado por Colidio, Pedro e Gustavo Gómez.

## Leitura para o TCC

O R² baixo (~0.05 no holdout) reflete a **variância intrínseca** da pontuação do
Cartola por rodada, não um erro de modelagem — depois que o vazamento das features
GatoMestre foi removido, o teto honesto do sinal puro-Cartola é modesto. O que
importa para a aplicação é o **erro absoluto** (MAE ~3 pts/atleta, ~4.7 pts na
escalação de 12), que mantém a otimização de escalação competitiva.
