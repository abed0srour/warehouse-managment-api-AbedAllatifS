## Weak prompt: "Create endpoint for products."

Tried this one first. It didn't actually build anything — it went and checked the repo and found we already have POST /api/products (SKU uniqueness check, 409 on duplicates, 201 with Location header, already tested). It told me adding another POST on the same route would just crash with an ambiguous-match error, so instead of guessing what I meant, it stopped and asked me to clarify.

Honestly wasn't expecting that. I figured a vague prompt would just make something up.

## Strong prompt: "Create ASP.NET Core endpoint for warehouse products using controller-service pattern, DTO validation, unit tests, integration tests, and Swagger summary."

This one actually shipped something — a new stock-adjustment endpoint (POST /api/products/stock-adjustments). 41 new tests, all 467 passing, no new warnings.

What I found interesting: it noticed we already had a CreateStockAdjustmentRequest DTO sitting in the codebase, fully validated, just never used anywhere. So instead of writing a new DTO from scratch it just wired the endpoint around the one already there.

It added the actual stock-adjust logic to the Product domain class (adds/subtracts instead of overwriting like our other endpoint does), added the command handler, hooked up the controller, and — this was a nice surprise — actually got Swagger docs rendering, which apparently wasn't even configured properly in this project before now (no XML doc generation set up at all).

It also caught two things I definitely wouldn't have thought of on my own: the enum wouldn't have deserialized properly from JSON without a converter it added, and there was an integer overflow risk in the stock math it fixed by widening to long before the check.

One thing it was upfront about NOT doing it didn't persist the adjustment reason anywhere, just logs it. Said actually storing it properly would mean a new table and migration, which is more than "add an endpoint" implies, and left that as a suggestion for later rather than just doing it unasked.

What I noticed comparing the two

The weak prompt wasn't "wrong" exactly — it did the safe thing by asking instead of guessing and possibly colliding with existing code. But that means a lazy prompt just costs you a back-and-forth, it doesn't save you time.

The strong prompt's value wasn't really about being longer — it was that giving it the pattern, testing expectations, and doc requirements up front meant it could make good calls on its own (like not touching the DB for the audit trail, or not forcing in a "service" class even though I literally said "service" in the prompt — it stuck to how this codebase is actually built instead).

Basically: vague prompt = safe but stalls. Detailed prompt = it just goes and builds the thing, and makes reasonable judgment calls along the way instead of me having to catch mistakes after.