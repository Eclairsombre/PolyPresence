{
  inputs = {
    nixpkgs.url = "nixpkgs";

    flake-parts = {
      url = "github:hercules-ci/flake-parts";
      inputs.nixpkgs-lib.follows = "nixpkgs";
    };
  };

  outputs = {
    self,
    nixpkgs,
    flake-parts,
  } @ inputs:
    flake-parts.lib.mkFlake {inherit inputs;} {
      systems = ["x86_64-linux" "aarch64-linux" "x86_64-darwin" "aarch64-darwin"];

      perSystem = {
        lib,
        system,
        self',
        ...
      }: let
        pkgs = import nixpkgs {
          inherit system;
        };
      in {
        packages = {
          front = pkgs.callPackage ./front {};
          back = pkgs.callPackage ./backend {};
        };
        devShells.default = with pkgs;
          mkShell {
            inputsFrom = with self'.packages; [front back];
          };

        formatter = pkgs.alejandra;
      };
    };
}
