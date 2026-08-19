# Exercise 10 — AI Risk Review for Backend Teams

| Backend Scenario              | AI Risk | Mitigation Strategy |
|-------------------------------|---|---|
| AI generates wrong validation | Business logic bypass / bug | Multi-scenario automated manual testing |
| AI generates insecure file upload | Remote code execution vulnerability | Senior developer code review & static analysis |
| AI generates incorrect EF logic | Data corruption / performance leak | Human execution plan verification & database profiling |
| AI generates missing auth mappings | Broken object-level security risk | Explicit automated integration security tests |

## Real examples from this lab

-Insecure file upload — Exercise 08 found 12 real vulnerabilities in a 
  sample upload endpoint, and 2 real ones in our actual `UploadProductImage` 
  endpoint (public read access via middleware ordering, no content validation).
-Incorrect EF logic — Exercise 05's refactor required verifying SQL 
  translation directly via `ToQueryString()`, since EF InMemory tests alone 
  wouldn't have caught a broken query.
-Wrong validation — Exercise 03's edge-case tests surfaced a real 
  integer overflow bug in paging logic that silently returned wrong data 
  instead of erroring.