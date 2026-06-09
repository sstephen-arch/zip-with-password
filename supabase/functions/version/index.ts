// Starkive version endpoint — update CURRENT_VERSION when shipping a new release.
// The app polls this on startup to show the in-app update banner.

const CURRENT_VERSION = "1.3.3";
const DOWNLOAD_URL    = "https://github.com/sstephen-arch/zip-with-password/releases/download/v1.3.3/Starkive-Setup-1.3.3.exe";

Deno.serve((_req: Request) => {
  return new Response(
    JSON.stringify({ version: CURRENT_VERSION, url: DOWNLOAD_URL }),
    { status: 200, headers: { "Content-Type": "application/json" } },
  );
});
