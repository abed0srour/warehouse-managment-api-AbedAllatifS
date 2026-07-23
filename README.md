# Session 2

Built products and suppliers 

# Session 3 

Split the one folder project into layers: Domain, Application, Infrastructure, Presentation layers.
Rules like (price cant be zero, archived products cant be edited) moved into the Domain layer (buisness logic layer)
Controllers became smaller, pass requests along them, no logic inside them (moved into domain layer)

# Session 4 

Connected the API to Postgres using ef core
Tried two approaches: database-first (build DB, generate code) and code-first (write code, generate DB)
Added LINQ queries — grouping products by expiry year, filtering by supplier, pagination
Used AutoMapper to return clean view models instead of raw database entities

# Session 5 

Added a consistent error format so failures always look the same to the client
Added custom exceptions (not found, business rule errors) instead of generic ones
Added validation on incoming requests
Added middleware to catch unhandled errors and return safe messages
Added middleware to tag every request with an ID and track how long it took
Added filters for logging and model validation
Cleaned up async code so nothing blocks unnecessarily
Added a dashboard endpoint that runs multiple things at once

# Session 6

Added support for multiple languages
Added structured logging saved to files (Serilog)
Added Redis caching so repeated requests dont always hit the database
Added a /health endpoint to check if database and Redis are alive
Added a daily background job that checks for expired/expiring products

# Session 7 

Firebase checks who is logging in and hands back a token
The API reads a role from that token (onlyadmin or regular user) to decide what someone can do
MinIO stores actual files (product images)
The database only stores info about the file
Admins can upload/delete files, everyone signed in can view or download them