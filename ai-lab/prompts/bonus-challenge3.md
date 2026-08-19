## What I asked

Wanted a GitHub Actions workflow that actually builds and tests this thing on every push/PR to main and the session branches — nothing fancy, just restore, build, test, and fail loudly if anything breaks.

## What came back

.github/workflows/ci.yml — single job on Ubuntu, checks out the code, sets up .NET 8 (checked the project files, confirmed everything targets net8.0), restores, builds in Release, and runs the full test suite. Test results get uploaded as an artifact even if the run fails, so you can actually see what broke instead of just a red X. Also added a concurrency setting so if you push again while a run is still going, the old one gets cancelled instead of wasting runner time.

## The thing I didn't see coming

Turns out this would've just failed on the very first CI run, and not because of a bad workflow — because the app needs a Firebase service account file to even start up (Program.cs loads it at boot), and that file is gitignored on purpose since it's a real credential. A fresh checkout in CI simply doesn't have it.

It caught this and handled it two ways: if you set up a FIREBASE_SERVICE_ACCOUNT secret in the repo, it uses that. If not, it generates a fake throwaway credential on the fly (just enough to be structurally valid so the app can boot) — which works because the tests already swap out real Firebase auth for a fake test handler, so nothing in the test suite actually needs a real credential.

I appreciated that it didn't just claim this works — it actually generated one of these fake credentials locally and ran the integration tests against it to prove the app boots fine with it before putting it in the workflow.

## One thing it flagged but didn't fix

Apparently the app also makes a real network call to Google at startup (fetching some token verification keys) and does it in a blocking way. It pointed out this means every CI run technically depends on Google being reachable — a random network hiccup would look like a failed test run even though nothing's actually wrong with the code. It said this is a separate problem worth fixing on its own, not something it should sneak into "just add a CI file," so it left it alone and just wrote it down as a known risk.

Also noted: no database or Redis services needed in the workflow, since the tests run against in-memory versions of everything, not real infrastructure.