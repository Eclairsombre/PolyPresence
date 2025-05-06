{
  lib,
  buildNpmPackage,
  domain ? "localhost",
  apiUrl ? "https://${domain}/api",
  baseUrl ? "https://${domain}",
  cookieSecret ? "PolyPresenceSecretKey2025;",
}:
buildNpmPackage rec {
  pname = "polypresence_front";
  version = "0.0.0";

  src = lib.cleanSource ./.;

  npmDepsHash = "sha256-b8fuar96WjWSt0MCQWq84k/E1AzEe/wzDh2QkeTG0+0=";

  buildPhase = ''
    export VITE_API_URL="${apiUrl}"
    export VITE_BASE_URL="${baseUrl}"
    export VITE_COOKIE_SECRET="${cookieSecret}"
    npm run build
  '';

  installPhase = ''
    runHook preInstall
    mkdir -p $out
    cp -r dist/* $out/
    runHook postInstall
  '';

  meta.mainProgram = pname;
}
