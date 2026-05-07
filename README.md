# Alza

## orchestration approach



                    ┌─────────────────────────────┐
                    │ Aggregation Backend Service │
                    └───────────┬───┬───┬─────────┘
                                *   *   *      *HTTP with retry strategies
                 ┌──────────────┘   │   └─────────────────┐
               cache              cache                 cache
                 │                  │                     │
        ┌────────▼────────┐  ┌──────▼────────┐   ┌────────▼────────┐
        │ Product Service │  │ Stock Service │   │ Pricing Service │
        └────────┬────────┘  └───────┬───────┘   └────────┬────────┘
                 │                   │                    │
               cache 		       cache                cache
                 │                   │                    │
             ┌───▼───┐           ┌───▼───┐            ┌───▼───┐
             │  DB   │           │   DB  │            │  DB   │
             └───────┘           └───────┘            └───────┘





## Trade-offs of this solution 

## What would change under 10x load 

## What I intentionally simplified 


## Failure scenarios

### AAA

### BBB