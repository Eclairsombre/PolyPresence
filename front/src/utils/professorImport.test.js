import { describe, expect, it } from "vitest";
import {
  identityKey,
  indexProfessors,
  mapHeaders,
  parseProfessorSheet,
  planRow,
} from "./professorImport";

const HEADER = ["Nom", "Prénom", "Email"];

describe("mapHeaders", () => {
  it("lit les colonnes par en-tete, quel que soit leur ordre", () => {
    const mapping = mapHeaders(["Email", "Prenom", "NOM"]);

    expect(mapping).toEqual({ email: 0, firstname: 1, name: 2 });
  });

  it("accepte les variantes d'en-tete courantes", () => {
    expect(mapHeaders(["Nom de famille", "Prénoms", "Adresse mail"])).toEqual({
      name: 0,
      firstname: 1,
      email: 2,
    });
  });
});

describe("identityKey", () => {
  it("ignore casse, accents et espaces", () => {
    // Le cas qui compte : ADE ecrit les profs en MAJUSCULES, l'Excel de l'ecole
    // en casse normale. Sans cela, chaque import fabriquerait un doublon.
    expect(identityKey("DUPONT", "JEAN")).toBe(identityKey("Dupont", "Jean"));
    expect(identityKey("LÉVÊQUE", "Chloé")).toBe(
      identityKey("leveque", "chloe"),
    );
    expect(identityKey("Le Gall", "Anne-Marie")).toBe(
      identityKey("LEGALL", "ANNEMARIE"),
    );
  });

  it("distingue deux personnes differentes", () => {
    expect(identityKey("Dupont", "Jean")).not.toBe(
      identityKey("Dupont", "Jeanne"),
    );
  });
});

describe("parseProfessorSheet", () => {
  it("lit les lignes valides", () => {
    const result = parseProfessorSheet([
      HEADER,
      ["DUPONT", "Jean", "jean.dupont@univ-lyon1.fr"],
      ["Martin", "Claire", ""],
    ]);

    expect(result.fatal).toBeUndefined();
    expect(result.errors).toEqual([]);
    expect(result.rows).toEqual([
      { name: "DUPONT", firstname: "Jean", email: "jean.dupont@univ-lyon1.fr" },
      { name: "Martin", firstname: "Claire", email: "" },
    ]);
  });

  it("ignore les lignes entierement vides", () => {
    const result = parseProfessorSheet([
      HEADER,
      ["", "", ""],
      ["Martin", "Claire", "c@x.fr"],
    ]);

    expect(result.errors).toEqual([]);
    expect(result.rows).toHaveLength(1);
  });

  it("refuse le fichier si une colonne manque", () => {
    const result = parseProfessorSheet([
      ["Nom", "Email"],
      ["Martin", "c@x.fr"],
    ]);

    expect(result.fatal).toContain("Prénom");
    expect(result.rows).toEqual([]);
  });

  it("signale les lignes sans nom ou sans prenom, avec leur numero", () => {
    const result = parseProfessorSheet([
      HEADER,
      ["Martin", "", "c@x.fr"],
      ["", "Claire", "c@x.fr"],
    ]);

    expect(result.errors).toEqual([
      "Ligne 2 : nom et prénom obligatoires.",
      "Ligne 3 : nom et prénom obligatoires.",
    ]);
    // Rien n'est rendu : l'appelant doit tout refuser, pas importer la moitie.
    expect(result.rows).toEqual([]);
  });

  it("signale un email mal forme", () => {
    const result = parseProfessorSheet([HEADER, ["Martin", "Claire", "pas-un-mail"]]);

    expect(result.errors).toEqual([
      "Ligne 2 : email « pas-un-mail » invalide.",
    ]);
  });

  it("signale un doublon dans le fichier en pointant la premiere occurrence", () => {
    const result = parseProfessorSheet([
      HEADER,
      ["DUPONT", "Jean", "a@x.fr"],
      ["Martin", "Claire", "c@x.fr"],
      ["Dupont", "JEAN", "b@x.fr"],
    ]);

    expect(result.errors).toEqual([
      "Ligne 4 : JEAN Dupont apparaît déjà ligne 2.",
    ]);
  });

  it("refuse un fichier reduit a son en-tete", () => {
    expect(parseProfessorSheet([HEADER]).fatal).toContain("aucune ligne");
  });
});

describe("planRow", () => {
  const existing = indexProfessors([
    { id: 7, name: "DUPONT", firstname: "JEAN", email: "vieux@x.fr" },
    { id: 8, name: "Martin", firstname: "Claire", email: "" },
  ]);

  it("cree un professeur inconnu", () => {
    expect(planRow({ name: "Nouveau", firstname: "Paul", email: "" }, existing))
      .toEqual({ action: "create" });
  });

  it("retrouve un professeur cree par ADE malgre la casse", () => {
    const plan = planRow(
      { name: "Dupont", firstname: "Jean", email: "vieux@x.fr" },
      existing,
    );

    expect(plan.action).toBe("keep");
    expect(plan.professor.id).toBe(7);
  });

  it("met a jour l'email quand le fichier en donne un different", () => {
    const plan = planRow(
      { name: "Dupont", firstname: "Jean", email: "neuf@x.fr" },
      existing,
    );

    expect(plan.action).toBe("update");
    expect(plan.professor.id).toBe(7);
  });

  it("renseigne l'email d'un professeur qui n'en avait pas", () => {
    const plan = planRow(
      { name: "Martin", firstname: "Claire", email: "claire@x.fr" },
      existing,
    );

    expect(plan.action).toBe("update");
    expect(plan.professor.id).toBe(8);
  });

  it("n'efface jamais un email connu avec une cellule vide", () => {
    // L'absence d'information n'est pas une information : une colonne Email
    // laissee vide ne doit pas vider la fiche.
    const plan = planRow(
      { name: "Dupont", firstname: "Jean", email: "" },
      existing,
    );

    expect(plan.action).toBe("keep");
  });
});
