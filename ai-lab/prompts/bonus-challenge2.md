## Bonus Challenge 2 — Shipment Tracking Module ## 
What I asked

Sent Claude Code the full spec — build out the shipment tracking module for real this time (create shipment, assign products, track status, update delivery state, notify supplier), told it to follow the exact CQRS pattern already in the codebase instead of inventing its own structure, and to build it all the way through: domain, EF Core, commands/queries, controller, and tests at every level.

## What came back ##

It actually built the whole thing end to end. 134 new tests, all passing, and the existing 292 tests still pass too (426 total, nothing broken).

Domain side: Shipment is the main entity, owning its line items (products assigned to it) and a status history log. Every change goes through a proper method on the entity itself (AssignProduct, UpdateDeliveryState, etc.) instead of just exposing public setters — same pattern as how Product already works in this codebase.

Database side: new EF models, a real migration, a repository — followed the existing style.

Application side: four commands (create, assign products, update delivery state, notify supplier) and two queries (get shipment, get status/tracking history).

API side: a new ShipmentsController with six endpoints — get shipment, get status, create, assign products, update delivery state, and manually re-trigger a supplier notification.

## The decisions I actually care about ##

A few things it did that I think were the right calls:

It merged "track status" and "update delivery state" into one thing. The lab brief listed them as two separate features, but if you model them separately you end up with two fields that can disagree with each other. Instead there's one status field with a proper state machine (Draft → Dispatched → InTransit → Delivered/Cancelled), and "tracking" just means reading that status plus the full history of how it got there.

Supplier notification happens automatically, not manually. When delivery state changes, it fires an event and a separate handler sends the notification — so the "update state" logic doesn't also have to know about notifying suppliers. It kept the manual notify endpoint too though, in case someone needs to resend one.

It caught a real bug risk on its own — when assigning multiple products to a shipment, a naive approach would look each product up one at a time (N+1 query problem). It added a batch lookup method instead and wrote a test specifically confirming the batch method gets used, not the one-at-a-time version.

No cache on shipment status. Other reads in this app get cached for a few minutes, but status is exactly the kind of thing that changes and you'd want a real-time answer for, so it skipped caching here on purpose.

It flagged something it deliberately didn't do: assigning products to a shipment checks that you're not shipping more than what's in stock, but it doesn't actually reserve/hold that stock. So two people could still both "assign" the same units to different shipments if they're not careful. It called this out as the obvious next thing to build, not something it tried to solve now.

## Something weird it ran into and fixed ##

Apparently this project has two separate WarehouseDbContext classes and two migration folders (legacy leftover from earlier code), and the dotnet ef migrations add command put the new migration in the wrong one. It caught this, moved the migration to the right folder, and restored the other one from git so nothing got corrupted. Worth knowing about since it could bite again on the next migration if we're not careful.

## What I checked myself ##

Ran dotnet build and dotnet test — clean build, no new warnings, all 426 tests passing. Didn't just take its summary at face value.