import { describe, expect, it } from "vitest";
import { findGroup, normalize } from "./groupLookup";

const languageGroups = [
  { id: 1, label: "A-I3002TR-AR51", displayName: "Anglais renforcé 1" },
  { id: 2, label: "A-I3002TR-AN51", displayName: "Anglais 1" },
  { id: 3, label: "A-I3004TR-ES51", displayName: "Espagnol 1" },
  { id: 4, label: "A-I3004TR-CH51", displayName: "Chinois 1" },
];

const subGroups = [
  { id: 10, label: "INFO 1-A", displayName: "INFO 1-A" },
  { id: 11, label: "INFO 1-B", displayName: "INFO 1-B" },
];

describe("normalize", () => {
  it("ignore casse, accents et separateurs", () => {
    expect(normalize("A-I3002TR-AR51")).toBe("ai3002trar51");
    expect(normalize(" Anglais renforcé 1 ")).toBe("anglaisrenforce1");
  });
});

describe("findGroup — correspondance exacte", () => {
  it("retrouve par libelle ADE", () => {
    expect(findGroup("A-I3002TR-AR51", languageGroups).group.id).toBe(1);
  });

  it("retrouve par nom d'affichage", () => {
    expect(findGroup("Espagnol 1", languageGroups).group.id).toBe(3);
  });

  it("tolere casse et separateurs", () => {
    expect(findGroup("a i3002tr ar51", languageGroups).group.id).toBe(1);
  });

  it("rend null sur une valeur inconnue", () => {
    expect(findGroup("XX99", languageGroups).group).toBeNull();
  });

  it("rend null sur une cellule vide", () => {
    expect(findGroup("", languageGroups).group).toBeNull();
    expect(findGroup(null, languageGroups).group).toBeNull();
  });
});

describe("findGroup — code court sur 4 caracteres", () => {
  it("retrouve le groupe par les 4 derniers caracteres du libelle", () => {
    // Le cas visé : le fichier de la scolarité ne reprend que le code final.
    expect(findGroup("AR51", languageGroups, { shortCode: true }).group.id).toBe(1);
    expect(findGroup("ES51", languageGroups, { shortCode: true }).group.id).toBe(3);
    expect(findGroup("ch51", languageGroups, { shortCode: true }).group.id).toBe(4);
  });

  it("n'applique le repli que sur exactement 4 caracteres", () => {
    // "R51" ou "TRAR51" ne sont pas des codes : aucun rapprochement approximatif.
    expect(findGroup("R51", languageGroups, { shortCode: true }).group).toBeNull();
    expect(findGroup("TRAR51", languageGroups, { shortCode: true }).group).toBeNull();
  });

  it("reste inactif quand l'option n'est pas demandee", () => {
    // Les sous-groupes de promotion n'ont pas de code final signifiant :
    // y appliquer le repli rapprocherait des libellés sans rapport.
    expect(findGroup("AR51", languageGroups).group).toBeNull();
    expect(findGroup("1-A", subGroups, { shortCode: true }).group).toBeNull();
  });

  it("donne la priorite a la correspondance exacte", () => {
    // Un groupe dont le nom d'affichage fait 4 caracteres ne doit pas être
    // supplanté par un code court qui viserait un autre groupe.
    const groups = [
      { id: 20, label: "A-I3004TR-ES51", displayName: "ES51" },
      { id: 21, label: "A-I3002TR-ES51", displayName: "Espagnol bis" },
    ];

    expect(findGroup("ES51", groups, { shortCode: true }).group.id).toBe(20);
  });

  it("refuse de choisir quand le code vise plusieurs groupes", () => {
    // Deux modules différents peuvent porter le même code final. Trancher au
    // hasard rattacherait l'étudiant au mauvais cours.
    const groups = [
      { id: 30, label: "A-I3002TR-AR51", displayName: "Anglais 3A" },
      { id: 31, label: "A-I4002TR-AR51", displayName: "Anglais 4A" },
    ];

    const result = findGroup("AR51", groups, { shortCode: true });

    expect(result.group).toBeNull();
    expect(result.ambiguous).toEqual(["A-I3002TR-AR51", "A-I4002TR-AR51"]);
  });

  it("accepte une liste de candidats vide ou absente", () => {
    expect(findGroup("AR51", [], { shortCode: true }).group).toBeNull();
    expect(findGroup("AR51", undefined, { shortCode: true }).group).toBeNull();
  });
});
