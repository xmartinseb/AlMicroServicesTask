# Alza

## orchestration approach

                ┌─────────────────────────────┐
                │ Aggregation Backend Service │
                └───────────┬───┬───┬──logs───┘
                            *   *   *      *HTTP with retry strategies and Correlation ID
             ┌───required───┘   │   └─────────────────┐
           cache              cache                 cache
             │                  │                     │
    ┌────────▼────────┐  ┌──────▼────────┐   ┌────────▼────────┐
    │ Product Service │  │ Stock Service │   │ Pricing Service │
    └────────┬───logs─┘  └───────┬──logs─┘   └────────┬───logs─┘
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
- Aggr. service calls the microservices **in parallel**, because the requests are fully independent operations. It reduces the latency heavily.
- Fetching the data from microservices uses several steps.
  1. Aggr. service fetches the data from the local cache if it's stored and not expired. The data usually mutates rarely, hence it is useful to provide the loaded data to more requests. It leads to better latency (less HTTP communication, less queries into the database)
  2. If the cache doesn't provide the required data, the aggr. service sends a HTTP request to the microservice. If the request fails with a transient error (timeout, temporal unavailability, etc), the aggr. service sends it again using the retry strategy from appsettings.json
  3. When the aggr. service gets the response, it stores the data to the cache
- HTTP requests and responses between the services uses **Correlation ID**. It helps to understand their relationships and allows better investigation in the logs

## Trade-offs of this solution 
Even the approach with microservices has its pros and cons:

**Pros:**
+ Better organization of the code and responsibilities within the company. Each team is able to work independently on their own service, which allows for faster development and deployment.
+ Each microservice could be written in different programming language or with different libraries or achitecture, depending on the team
+ Each microservice could be deployed independently without restarting the whole system
+ When a microservice fails, it usually doesn't break the whole system -- it fails independently 

**Cons:**
- More communication between processes, which leads to higher latency (Therefore I use in memory caches)
- More complexity

## What would change under 10x load 

## What I intentionally simplified 


## Failure scenarios

### AAA

### BBB