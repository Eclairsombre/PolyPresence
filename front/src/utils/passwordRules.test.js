import { describe, expect, it } from "vitest";
import { checkPassword, isPasswordValid, PASSWORD_RULES } from "./passwordRules";

const failing = (password) =>
  checkPassword(password)
    .rules.filter((r) => !r.satisfied)
    .map((r) => r.key);

describe("checkPassword", () => {
  it("accepte un mot de passe conforme", () => {
    expect(isPasswordValid("Motdepasse1!")).toBe(true);
    expect(failing("Motdepasse1!")).toEqual([]);
  });

  it("rend l'etat de chacune des cinq regles", () => {
    const { rules } = checkPassword("");
    expect(rules.map((r) => r.key)).toEqual([
      "length",
      "uppercase",
      "lowercase",
      "digit",
      "special",
    ]);
    expect(rules.every((r) => !r.satisfied)).toBe(true);
  });

  it("refuse un mot de passe trop court, meme complet par ailleurs", () => {
    expect(failing("Ab1!")).toEqual(["length"]);
  });

  it("refuse l'absence de majuscule", () => {
    expect(failing("motdepasse1!")).toEqual(["uppercase"]);
  });

  it("refuse l'absence de minuscule", () => {
    expect(failing("MOTDEPASSE1!")).toEqual(["lowercase"]);
  });

  it("refuse l'absence de chiffre", () => {
    expect(failing("Motdepasse!")).toEqual(["digit"]);
  });

  it("refuse l'absence de caractere special", () => {
    // Le cas que l'inscription laissait passer : 12 caractères, majuscule,
    // minuscule et chiffre, mais l'API le rejetait.
    expect(failing("Motdepasse12")).toEqual(["special"]);
  });

  it("n'accepte que les symboles reconnus par le serveur", () => {
    // « ~ » et « / » sont hors du jeu du backend : les accepter ici ferait
    // croire à l'utilisateur que son mot de passe passe, puis l'API refuserait.
    expect(failing("Motdepasse1~")).toEqual(["special"]);
    expect(failing("Motdepasse1/")).toEqual(["special"]);
    expect(failing("Motdepasse1|")).toEqual([]);
    expect(failing("Motdepasse1<")).toEqual([]);
    expect(failing("Motdepasse1\\")).toEqual([]);
  });

  it("tolere une valeur absente", () => {
    expect(isPasswordValid(undefined)).toBe(false);
    expect(isPasswordValid(null)).toBe(false);
  });

  it("expose les libelles affiches a l'utilisateur", () => {
    expect(PASSWORD_RULES.map((r) => r.label)).toEqual([
      "Au moins 8 caractères",
      "Une lettre majuscule",
      "Une lettre minuscule",
      "Un chiffre",
      "Un caractère spécial (!@#$%^&*…)",
    ]);
  });
});
