{
  lib,
  buildNpmPackage,
  bash,
  simple-http-server,
  # Add input variables for environment configuration
  port ? 8000,
  threads ? 3,
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
    export PORT="${toString port}"
    export VITE_API_URL="${apiUrl}"
    export VITE_BASE_URL="${baseUrl}"
    export VITE_COOKIE_SECRET="${cookieSecret}"
    npm run build
  '';

  installPhase = ''
    runHook preInstall
    mkdir -p $out/lib
    cp -r dist/* $out/lib/

    # Create a wrapper script
    mkdir -p $out/bin
    echo '#!${bash}/bin/bash' > $out/bin/${pname}
    echo 'PORT=''${PORT:-${toString port}}' >> $out/bin/${pname}
    echo 'THREADS=''${THREADS:-${toString threads}}' >> $out/bin/${pname}
    echo '${simple-http-server}/bin/simple-http-server -i -p $PORT -t $THREADS ${placeholder "out"}/lib' >> $out/bin/${pname}
    chmod +x $out/bin/${pname}
    runHook postInstall
  '';

  meta.mainProgram = pname;
}
