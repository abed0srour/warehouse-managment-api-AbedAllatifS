## Challenge 1 — GET /api/products/out-of-stock
## What I asked

Wanted a new endpoint that lists products completely out of stock, following the same query-handler pattern the rest of this codebase uses (same style as the stuff from Exercise 03). Asked for the full package — handler, DTO, controller route, unit tests, and integration tests.

## What came back

It's live: GET /api/products/out-of-stock. Added 25 new tests (13 unit + 12 integration), everything still passes — 492 tests total now, up from 467, no new build warnings.

## Decisions I thought were worth noting

It put the handler in the Infrastructure layer, not Application. Apparently this codebase already has two different styles for handlers — some pull everything from the repo and filter in C# afterward, others query the database directly. It pointed out that pulling every single product just to throw away all but the zero-stock ones would be wasteful, so it went with the direct-database-query style instead, since the filter can actually run in SQL that way.

It made a separate, smaller DTO instead of reusing the existing product view model. Made sense when I thought about it — this is basically a reorder list, so you want the product name, SKU, and which supplier to reorder from. You don't need price or expiry date cluttering that up. It also mentioned this sidesteps a weird bug from earlier in the lab where a null "last updated" date silently turns into a fake year-1 date through the existing mapper — the new DTO just reports null honestly instead.

Results are sorted by name. Reasonable for something a person is actually going to read off a screen.

It's strict about "zero," not "low stock." There's already a separate "low stock" concept elsewhere in the app — this one only returns products where the count is exactly 0, not "getting low." It even wrote a test to make sure a product with 1 unit doesn't show up here.

Archived products are excluded, everything else counts. So a product with no assigned supplier, or one that's expired, still shows up in this list if it's out of stock — makes sense, since it's still something you'd need to restock regardless.

 ## Small thing I appreciated ##

The new endpoint actually shows up properly in Swagger docs (summary, response codes) — turns out that only started working because of the Swagger XML doc setup that got fixed earlier during the stock-adjustment work in Exercise 09. Nice that it's carrying forward instead of being a one-off fix.