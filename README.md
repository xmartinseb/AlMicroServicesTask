# Alza

## Orchestration approach

                ┌─────────────────────────────┐
                │ Aggregation Backend Service │
                └───────────┬───┬───┬──logs───┘
                            │   │   │      
             ┌───required───┘   │   └─────────────────┐
             │                  │                     │
        ┌────▼──────────────────▼─────────────────────▼─────┐
        │        CACHE - shared in memory cache             │
        └────▼──────────────────▼─────────────────────▼─────┘
             │                  │                     │
      circuit breaker    circuit breaker       circuit breaker 
             │                  │                     │
             │ *http            │ *http               │ *http
    ┌────────▼────────┐  ┌──────▼────────┐   ┌────────▼────────┐
    │ Product Service │  │ Stock Service │   │ Pricing Service │
    └────────┬───logs─┘  └───────┬──logs─┘   └────────┬───logs─┘
             │                   │                    │
           cache** 		       cache**              cache**   
             │                   │                    │
         ┌───▼───┐           ┌───▼───┐            ┌───▼───┐
         │  DB   │           │   DB  │            │  DB   │
         └───────┘           └───────┘            └───────┘
     
     *  HTTP with retry strategies and Correlation ID and with HISTOGRAM METRICS
     ** If we use SQL servers, caches are especially useful, because relational databases often become throughput bottlenecks

This project consists of one aggregation API service and three microservices: Product Service, Stock Service, and Pricing Service. 
Each of the microservices provides some partial information about the products and the aggregation service is responsible for calling them and combining the results into a single response.

- Product service is **critical**. It is required to get the product information. If it fails, the whole request will fail, because the customer would not be able to identify the product.
- Stock service and pricing service are **not critical**. If they fail, the aggregation service will return the product information with null values for stock or price (**Partial failure**). This way, the customer can still see the product.
- Aggr. service calls the microservices **in parallel**, because the requests are independent operations. It significantly **reduces the latency**.
- Data fetching from microservices uses several steps.
  1. Aggr. service fetches the data from the local cache if it's stored and not expired. The data mutates rarely, hence it is useful to provide the loaded data to more requests. It leads to better latency (less HTTP communication, less queries into the database)
  2. If the cache doesn't provide the required data, the system checks whether the microservice isn't blocked by circuit breaker. It prevents the distributed system from useless communication with an unavailable service
  3. If the microservice isn't blocked, the aggr. service sends a HTTP request to the microservice. If the request fails with a **transient error** (timeout, temporal unavailability, etc), the aggr. service sends it again using the retry strategy from appsettings.json
  4. When the aggr. service gets the response, it stores the data to the cache
- HTTP requests and responses between the services uses **Correlation ID**. It binds the related communication (one client's request can consist of four single requests) and allows better investigation in the logs
- Every service uses logs (console and files) with **Serilog** library. It automatically uses log retention policy, which prevents us from extreme disk consumption
- Aggregation service has **/metrics** endpoint. It shows **latency histograms** of the microservices and **cache hits/misses** in Prometheus, which is a human-readable format, but it is typically used for Grafana or other chart generators. 

## Trade-offs of this solution 
### Microservices architecture
Even the approach with microservices has its pros and cons

**Pros:**
+ Better organization of the code and responsibilities within the company. Each team is able to work independently on their own service with clear responsibilities, which simplifies the communication and development (less git conflicts etc.).
+ Each microservice could be written in different programming language or with different libraries or architecture, depending on the team
+ Each microservice could be deployed independently without restarting the whole system
+ When a microservice fails, it usually doesn't break the whole system -- it fails independently 

**Cons:**
- More communication between processes, which leads to higher latency (Therefore I use in-memory caches)
- More complexity (more running services, more RAM consumption, more network communication)

### Caches
**Pros:**
+ Much less http communication and queries into DB. The more requests we have, the more useful the caching is -- it serves the loaded data to many incoming requests
  + This leads to **shorter latency** and potentially **higher throughput** because there are less requests.
+ It prevents the distributed system from **potential bottlenecks** (SQL databases can become a bottleneck very easily because it guarantees consistency)

**Cons:**
- More RAM consumption. If we had more instances of the same service, I'd suggest to use **Redis** instead of in-memory cache in order to store the data only once.
- Irregular latency: much more milliseconds on cache miss.
- The distributed system with caches has **eventual consistency**
  - Data updates are not visible everywhere immediately, since the caches must be refreshed, invalidated or just wait for expiration.
    - We need to consider the proper TTL for every cache, because some data mutate rarely, some data could be mutated every second.

### Log files
Even though it is an essential part of any solution, it also has its cons:
- More disk space consumption. It is required to set its limits. Otherwise, it could consume the whole free disk space. Hence I use the **Serilog** library. It allows to configure every detail in appsettings (roll period, max used space, max days retention etc.) 


## What would change under 10x load (10x more requests)
- Aggregation service has to generate 10x more responses
  - There are some approaches that are especially useful in such scenario: 
    - async/await model leads to more effective CPU usage without active waiting or thread starvation
    - Caches provide the same data for many requests 
- Microservices and databases **don't** get 10x more requests thanks to the shared cache in the aggregation service.
  - It prevents us from potential bottlenecks
- Aggregation service generates 10x more logs. **Serilog** guarantees removing the oldest files when logs exceeds the configured size limit
- Rate limiter is bound to IP addresses. If any client exceeds the limit, he is prevented from other communication for a few seconds


## What I intentionally simplified 
- Metrics only for the aggregation service. I'd use them in every service if I had a real system
- Circuit breaker: uses lock instead of some lock-free approaches
- Every microservice has many special service types registered. There could be a nicer approach
- In real system, I'd introduce more microservice error states for API consumers
- I implemented integration tests only for the aggr. service, not for every service
- I used OAuth only in the aggr. service and without real external OAuth provider, I just used demo fake provider that accepts **any token**
- I used in memory caches with per item locks (semaphores). It guarantees that every record is created with a single http request even in high concurrency scenarios (many requests at the same time). However, **I haven't implemented** removing the semaphores.

## Failure scenarios

### DB failure (The microservices aren't able to load new data)
1. Aggr. service is working normally as long as there are valid cached data (no requests needed) AND microservice is working normally as long as it has valid cached data (no DB query needed)
2. Then: Microservice is trying to reconnect to the DB with timeout, exponential backoff and circuit breaker. If it fails, it returns 500.
  - OR: Is there a DB replica which works?
  - Then: Aggr. service is retrying to send a request to the microservice with exponential backoff, depending on the configuration.
3. If it exceeds the retry limit, the circuit breaker in the aggr. service is activated for the related microservice, which stops the communication with the microservice and prevents the network from overwhelming
4. Aggr. service will return partial failure (null values and error list) or full failure (error 500), depending on the data type: critical (product data) / non critical (pricing, stock)

Potential solutions:
- Is the DB a bottleneck? We could use more caching or use horizontal scalable non-SQL systems (e.g. MongoDB)
- Do we have SQL timeouts? We may have inefficient db index.
- In some scenarios, it could be useful to refresh some hot data in the cache regularly with one batch DB query

### Microservice is unavailable
1. Aggr. service is working normally as long as there are valid cached data (no requests needed)
2. Then: Microservice returns 500 and Aggr. service is retrying to send a request to the microservice with exponential backoff, depending on the configuration.
3. If it exceeds the retry limit, the circuit breaker in the aggr. service is activated for the related microservice, which stops the communication with the microservice and prevents the network from overwhelming
4. Aggr. service will return partial failure (null values and error list) or full failure (error 500), depending on the data type: critical (product data) / non critical (pricing, stock)


## Resilience mechanisms
Retry policies with exponential backoff
- Circuit breakers
- Graceful degradation
- In-memory caching
- Request correlation
- Rate limiting
- Timeout

## Observability
- Structured logging with Serilog
- Correlation ID propagation
- Prometheus/OpenTelemetry metrics
- Latency histograms
- Cache hit/miss metrics