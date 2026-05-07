# Alza

## orchestration approach

                ┌─────────────────────────────┐
                │ Aggregation Backend Service │
                └───────────┬───┬───┬─────────┘
                            *   *   *      *HTTP with retry strategies
             ┌───required───┘   │   └─────────────────┐
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


This project consists of one aggregation API service and three microservices: Product Service, Stock Service, and Pricing Service. 
Each of the microservices provides some partial information about the product and the aggregation service is responsible for calling them and combining the results into a single response.

- Product service is critical. It is required to get the product information. If it fails, the whole request will fail, because the customer would not be able to identify the product.
- Stock service and pricing service are not critical. If they fail, the aggregation service will return the product information with null values for stock or price. This way, the customer can still see the product.
- 


## Trade-offs of this solution 
Even the approach with microservices has its pros and cons:
+ Better organization of the code and responsibilities within the company. Each team is able to work independently on their own service, which allows for faster development and deployment.
+ Each microservice could be deployed independently without restarting the whole system
- More communication between processes, which leads to higher latency (Therefore I use in memory caches)
- More complexity

## What would change under 10x load 

## What I intentionally simplified 


## Failure scenarios

### AAA

### BBB