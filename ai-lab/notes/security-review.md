What I asked

Fed the sample UploadInvoice snippet from the lab into the AI and asked it to break it down — path traversal, missing validation, bad logging, weak error handling. Also told it to check whether our real UploadProductImage endpoint has the same problems, since that seemed like the more useful question.

What came back

It flagged 12 issues, 4 of them serious. Reading through it, I think it's right on all four:

file.FileName gets used straight in Path.Combine with zero sanitization — so someone can name their upload ../../appsettings.json and just... overwrite our config file. Or use C:\inetpub\wwwroot\shell.aspx, which doesn't even need .. because Path.Combine just drops the first argument when the second one is rooted. I didn't know that quirk of Path.Combine before this.
There's no [Authorize] on the snippet's endpoint at all, so this is all pre-auth in the example.
FileMode.Create overwrites silently, so two people uploading a file with the same name just clobber each other with no warning.

It also caught something I wouldn't have thought to check: logging the raw filename is a log-injection risk, because filenames can contain \r\n and get written straight into our plaintext log file. Someone could name a file so that when it gets logged, it looks like a fake extra log line was written by the system.

Comparing it to our actual code

This is where it got genuinely useful. It looked at our real UploadProductImage endpoint and pointed out it already avoids most of this — mainly because the stored filename is built from the product's GUID ({id}{extension}), not from anything the client sends. That one decision kills both the traversal issue and the rooted-path issue at once.

But it also found real problems still in our actual endpoint, not just the hypothetical snippet:

Content isn't actually checked — only the extension is. You could upload an .exe renamed to .png and it'd go right through.
No logging at all on this endpoint, so if an image gets overwritten or something goes wrong, there's no record of who did it.
No error handling — if the upload gets interrupted partway, you're left with a corrupted file that already replaced the old one.

And two things it found that I hadn't even thought to ask about:

UseStaticFiles() is registered before UseAuthorization() in Program.cs. That means uploading a product image requires an admin token, but reading it back doesn't require anything — and since the filename is just the product ID, and product IDs are public (GET /api/products returns them), anyone can pull any product image directly.
We already have a proper file storage setup (IFileStorageService + MinIO) used elsewhere in the app (FilesController), but this endpoint doesn't use it — it just writes straight to local disk, which means no DB record, no uploader tracking, and the file's gone if the container restarts.
My take

The AI's review was thorough and I checked its claims against the actual code rather than taking them at face value — the auth-ordering thing especially, I went and looked at Program.cs myself to confirm UseStaticFiles() really does come before UseAuthorization(), and it does.

The two real findings (public image reads, and bypassing our own storage abstraction) are more interesting to me than the hypothetical snippet, honestly — those are actual gaps in this project, not just a "spot the bug in this code" exercise. I think the auth-ordering issue in particular is worth raising separately, since it's a live bypass, not a theoretical one.