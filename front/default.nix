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

  npmDepsHash = "sha256-5gxnKzKdLlEQhe1xDViPCVr2XxBBa2hZ1aBnx5S8rMQ=";

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
