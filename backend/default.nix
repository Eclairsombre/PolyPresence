{
  lib,
  buildDotnetModule,
  dotnetCorePackages,
}:
buildDotnetModule {
  pname = "polypresence_back";
  version = "0.0.0";

  src = lib.cleanSource ./.;

  projectFile = "backend.csproj";
  nugetDeps = ./deps.json;

  dotnet-sdk = dotnetCorePackages.sdk_9_0;
  dotnet-runtime = dotnetCorePackages.runtime_9_0;
  selfContainedBuild = true;

  executables = ["backend"];
  meta.mainProgram = "backend";
}
