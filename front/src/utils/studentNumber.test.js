import { describe, expect, it } from "vitest";
import { normalizeStudentNumber } from "./studentNumber";

describe("normalizeStudentNumber", () => {
  it("remplace le 1 de tete par un p", () => {
    expect(normalizeStudentNumber("12345678")).toBe("p2345678");
  });

  it("ne touche qu'au premier caractere", () => {
    // Les autres 1 du numero sont des chiffres comme les autres.
    expect(normalizeStudentNumber("11111111")).toBe("p1111111");
  });

  it("laisse intact un numero deja prefixe par p", () => {
    expect(normalizeStudentNumber("p2345678")).toBe("p2345678");
  });

  it("laisse intact un numero commencant par un autre chiffre", () => {
    expect(normalizeStudentNumber("22345678")).toBe("22345678");
  });

  it("ignore les espaces autour", () => {
    // Excel rend volontiers des cellules avec des espaces parasites.
    expect(normalizeStudentNumber("  12345678  ")).toBe("p2345678");
  });

  it("accepte une cellule numerique", () => {
    // XLSX rend un nombre quand la colonne n'est pas formatee en texte.
    expect(normalizeStudentNumber(12345678)).toBe("p2345678");
  });

  it("rend une chaine vide pour une cellule vide", () => {
    expect(normalizeStudentNumber("")).toBe("");
    expect(normalizeStudentNumber(null)).toBe("");
    expect(normalizeStudentNumber(undefined)).toBe("");
  });
});
