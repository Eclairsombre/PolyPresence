import { describe, it, expect, beforeEach, vi, afterEach } from "vitest";
import {
  readOverride,
  writeOverride,
  pruneExpiredOverrides,
  sessionEndTimestamp,
} from "./profMailOverride";

const makeSession = (id, { endsInMinutes = 60 } = {}) => {
  const end = new Date(Date.now() + endsInMinutes * 60 * 1000);
  const pad = (n) => String(n).padStart(2, "0");
  return {
    id,
    date: `${end.getFullYear()}-${pad(end.getMonth() + 1)}-${pad(end.getDate())}T00:00:00`,
    endTime: `0001-01-01T${pad(end.getHours())}:${pad(end.getMinutes())}:${pad(end.getSeconds())}`,
  };
};

describe("profMailOverride", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("relit l'adresse enregistrée pour la même session", () => {
    const session = makeSession(42);
    writeOverride("u1", session, 1, "prof@test.fr");
    expect(readOverride("u1", 42, 1)).toBe("prof@test.fr");
  });

  it("ne fuit pas sur la session suivante", () => {
    writeOverride("u1", makeSession(42), 1, "prof@test.fr");
    expect(readOverride("u1", 43, 1)).toBe("");
  });

  it("cloisonne les créneaux prof 1 et prof 2", () => {
    const session = makeSession(42);
    writeOverride("u1", session, 1, "prof1@test.fr");
    expect(readOverride("u1", 42, 2)).toBe("");
  });

  it("cloisonne les utilisateurs d'un même navigateur", () => {
    writeOverride("u1", makeSession(42), 1, "prof@test.fr");
    expect(readOverride("u2", 42, 1)).toBe("");
  });

  it("expire à la fin de la session", () => {
    const session = makeSession(42, { endsInMinutes: 30 });
    writeOverride("u1", session, 1, "prof@test.fr");
    expect(readOverride("u1", 42, 1)).toBe("prof@test.fr");

    vi.useFakeTimers();
    vi.setSystemTime(new Date(Date.now() + 31 * 60 * 1000));
    expect(readOverride("u1", 42, 1)).toBe("");
  });

  it("n'enregistre rien pour une session déjà terminée", () => {
    writeOverride("u1", makeSession(42, { endsInMinutes: -10 }), 1, "x@test.fr");
    expect(readOverride("u1", 42, 1)).toBe("");
  });

  it("supprime l'entrée périmée à la lecture", () => {
    const session = makeSession(42, { endsInMinutes: 30 });
    writeOverride("u1", session, 1, "prof@test.fr");
    vi.useFakeTimers();
    vi.setSystemTime(new Date(Date.now() + 31 * 60 * 1000));
    readOverride("u1", 42, 1);
    expect(localStorage.length).toBe(0);
  });

  it("purge les entrées périmées sans toucher aux valides", () => {
    writeOverride("u1", makeSession(1, { endsInMinutes: 10 }), 1, "a@test.fr");
    writeOverride("u1", makeSession(2, { endsInMinutes: 120 }), 1, "b@test.fr");

    vi.useFakeTimers();
    vi.setSystemTime(new Date(Date.now() + 20 * 60 * 1000));
    pruneExpiredOverrides();

    expect(readOverride("u1", 1, 1)).toBe("");
    expect(readOverride("u1", 2, 1)).toBe("b@test.fr");
  });

  it("ignore une entrée corrompue", () => {
    localStorage.setItem(
      "polypresence:profMailOverride:u1:42:1",
      "pas-du-json",
    );
    expect(readOverride("u1", 42, 1)).toBe("");
  });

  it("renvoie null quand l'horaire de fin est inexploitable", () => {
    expect(sessionEndTimestamp({ date: null, endTime: null })).toBeNull();
  });

  it("ne casse pas si l'écriture localStorage échoue (navigation privée)", () => {
    const spy = vi
      .spyOn(Storage.prototype, "setItem")
      .mockImplementation(() => {
        throw new Error("QuotaExceededError");
      });

    expect(() =>
      writeOverride("u1", makeSession(42), 1, "prof@test.fr"),
    ).not.toThrow();

    spy.mockRestore();
  });

  it("ne casse pas si la lecture localStorage échoue", () => {
    const spy = vi
      .spyOn(Storage.prototype, "getItem")
      .mockImplementation(() => {
        throw new Error("SecurityError");
      });

    expect(readOverride("u1", 42, 1)).toBe("");

    spy.mockRestore();
  });

  it("ne casse pas si la purge échoue", () => {
    const spy = vi.spyOn(Storage.prototype, "key").mockImplementation(() => {
      throw new Error("SecurityError");
    });

    expect(() => pruneExpiredOverrides()).not.toThrow();

    spy.mockRestore();
  });

  it("garde une session sans horaire exploitable dans une fenêtre bornée", () => {
    // Pas de date/heure de fin : on retombe sur le garde-fou (4 h), jamais sur
    // une entrée éternelle.
    writeOverride("u1", { id: 99 }, 1, "prof@test.fr");
    expect(readOverride("u1", 99, 1)).toBe("prof@test.fr");

    vi.useFakeTimers();
    vi.setSystemTime(new Date(Date.now() + 5 * 60 * 60 * 1000));
    expect(readOverride("u1", 99, 1)).toBe("");
  });
});
