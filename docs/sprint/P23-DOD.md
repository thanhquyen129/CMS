# P23 — Outbox worker + Redis rate-limit + metrics DoD

**Ngày:** 2026-09-13 · E14–E16

## Done
1. `OutboxProcessorHostedService` poll pending → processed.
2. Redis optional rate-limit (`RateLimiting:RedisConnection`); compose `redis` service.
3. Metrics export: Prometheus `/metrics` (OTLP deferred — exporter advisory).

## Verify
- Build; Redis fail-open → in-process limiter.

## Non-goals
- Kafka/Rabbit broker; OTLP until advisory-cleared package.
