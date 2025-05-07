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

          back-docker = pkgs.dockerTools.buildImage {
            name = "backend";
            copyToRoot = self'.packages.back;
            config = {
              Cmd = ["backend"];
            };
          };

          front-docker = let
            nginxPort = 80;
            nginxConf = pkgs.writeText "nginx.conf" ''
              daemon off;
              user nobody nobody;
              worker_processes auto;
              error_log /dev/stdout info;
              pid /tmp/nginx.pid;

              events {
                worker_connections 1024;
              }

              http {
                access_log /dev/stdout;
                include ${pkgs.nginx}/conf/mime.types;
                default_type application/octet-stream;
                sendfile on;

                server {
                  listen ${toString nginxPort};
                  server_name localhost;

                  location / {
                    root ${self'.packages.front};
                    index index.html;
                    try_files $uri $uri/ /index.html;
                  }
                }
              }
            '';
          in pkgs.dockerTools.buildLayeredImage {
            name = "frontend";
            tag = "latest";

            contents = [
              pkgs.nginx
              pkgs.fakeNss
              self'.packages.front
            ];

            extraCommands = ''
                mkdir -p tmp var/cache/nginx var/log/nginx
                # Ensure proper permissions
                chmod 1777 tmp
              '';

            config = {
              Cmd = [ "nginx" "-c" "${nginxConf}" ];
              ExposedPorts = {
                "${toString nginxPort}/tcp" = {};
              };
              WorkingDir = "/";
            };
          };
        };
        devShells.default = with pkgs;
          mkShell {
            inputsFrom = with self'.packages; [front back];
          };

        formatter = pkgs.alejandra;
      };
    };
}
