// Starkive version endpoint — update CURRENT_VERSION when shipping a new release.
// The app polls this on startup to show the in-app update banner.

const CURRENT_VERSION = "1.3.2";
const RELEASE_URL     = "https://github.com/sstephen-arch/zip-with-password/releases/tag/v1.3.2";

Deno.serve((_req: Request) => {
  return new Response(
    JSON.stringify({ version: CURRENT_VERSION, url: RELEASE_URL }),
    { status: 200, headers: { "Content-Type": "application/json" } },
  );
});
