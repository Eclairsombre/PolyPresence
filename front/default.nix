{
  lib,
  buildNpmPackage,
  bash,
  simple-http-server
}:
buildNpmPackage rec {
  pname = "polypresence_front";
  version = "0.0.0";

  src = lib.cleanSource ./.;

  npmDepsHash = "sha256-b8fuar96WjWSt0MCQWq84k/E1AzEe/wzDh2QkeTG0+0=";

  installPhase = ''
    runHook preInstall
    mkdir -p $out/lib
    cp -r dist/* $out/lib/

    # Create a wrapper script
    mkdir -p $out/bin
    echo '#!${bash}/bin/bash' > $out/bin/${pname}
    echo '${simple-http-server}/bin/simple-http-server ${placeholder "out"}/lib' >> $out/bin/${pname}
    chmod +x $out/bin/${pname}
    runHook postInstall
  '';

  meta = {}; # TODO complete some meta informations

}
