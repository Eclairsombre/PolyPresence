import { beforeEach, describe, expect, it, vi } from "vitest";
import { createPinia, setActivePinia } from "pinia";
import { useSessionStore } from "./sessionStore";
import apiClient from "../api/axios";

vi.mock("../api/axios", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

describe("sessionStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  it("getSessionsByYear filtre par annee", () => {
    const store = useSessionStore();
    store.sessions = [
      { id: 1, year: "3A" },
      { id: 2, year: "4A" },
    ];

    expect(store.getSessionsByYear("3A")).toHaveLength(1);
    expect(store.getSessionsByYear("3A")[0].id).toBe(1);
  });

  it("getUpcomingSessions retourne les sessions futures", () => {
    const store = useSessionStore();
    const tomorrow = new Date(Date.now() + 24 * 60 * 60 * 1000)
      .toISOString()
      .substring(0, 10);
    const yesterday = new Date(Date.now() - 24 * 60 * 60 * 1000)
      .toISOString()
      .substring(0, 10);

    store.sessions = [
      { id: 1, date: `${tomorrow}T08:00:00` },
      { id: 2, date: `${yesterday}T08:00:00` },
    ];

    expect(store.getUpcomingSessions.map((s) => s.id)).toEqual([1]);
  });

  it("getPastSessions retourne les sessions passees", () => {
    const store = useSessionStore();
    const tomorrow = new Date(Date.now() + 24 * 60 * 60 * 1000)
      .toISOString()
      .substring(0, 10);
    const yesterday = new Date(Date.now() - 24 * 60 * 60 * 1000)
      .toISOString()
      .substring(0, 10);

    store.sessions = [
      { id: 1, date: `${tomorrow}T08:00:00` },
      { id: 2, date: `${yesterday}T08:00:00` },
    ];

    expect(store.getPastSessions.map((s) => s.id)).toEqual([2]);
  });

  it("getSessionsByDateRange filtre entre deux bornes", () => {
    const store = useSessionStore();
    store.sessions = [
      { id: 1, date: "2026-01-10T08:00:00" },
      { id: 2, date: "2026-02-10T08:00:00" },
      { id: 3, date: "2026-03-10T08:00:00" },
    ];

    const filtered = store.getSessionsByDateRange("2026-02-01", "2026-02-28");
    expect(filtered.map((s) => s.id)).toEqual([2]);
  });

  it("fetchAllSessions charge la liste", async () => {
    apiClient.get.mockResolvedValue({ data: { $values: [{ id: 7 }] } });
    const store = useSessionStore();

    const result = await store.fetchAllSessions();
    expect(result).toEqual([{ id: 7 }]);
    expect(store.loading).toBe(false);
  });

  it("fetchAllSessions retourne null en cas d'erreur", async () => {
    apiClient.get.mockRejectedValue(new Error("net"));
    const store = useSessionStore();

    const result = await store.fetchAllSessions();
    expect(result).toBeNull();
    expect(store.error).toContain("net");
  });

  it("fetchSessionsByYear sans annee delegue a fetchAllSessions", async () => {
    const store = useSessionStore();
    vi.spyOn(store, "fetchAllSessions").mockResolvedValue([{ id: 1 }]);

    const result = await store.fetchSessionsByYear();
    expect(result).toEqual([{ id: 1 }]);
  });

  it("fetchSessionsByYear retourne null sur erreur generique", async () => {
    apiClient.get.mockRejectedValue(new Error("boom"));
    const store = useSessionStore();

    const result = await store.fetchSessionsByYear("3A");
    expect(result).toBeNull();
  });

  it("fetchSessionsByYear retourne [] sur 404", async () => {
    apiClient.get.mockRejectedValue({ response: { status: 404 } });
    const store = useSessionStore();

    const result = await store.fetchSessionsByYear("3A");

    expect(result).toEqual([]);
    expect(store.sessions).toEqual([]);
  });

  it("fetchSessionById renseigne currentSession", async () => {
    apiClient.get.mockResolvedValue({ data: { id: 9 } });
    const store = useSessionStore();

    const result = await store.fetchSessionById(9);
    expect(result).toEqual({ id: 9 });
    expect(store.currentSession.id).toBe(9);
  });

  it("createSession ajoute la session au state", async () => {
    const created = { id: 3, year: "3A", name: "Algo" };
    apiClient.post.mockResolvedValue({ data: created });

    const store = useSessionStore();
    store.sessions = [{ id: 1, year: "3A" }];

    const result = await store.createSession({ year: "3A", name: "Algo" });

    expect(result).toEqual(created);
    expect(store.sessions.some((s) => s.id === 3)).toBe(true);
  });

  it("createSession ne push pas si annee non presente", async () => {
    const created = { id: 5, year: "5A", name: "Reseau" };
    apiClient.post.mockResolvedValue({ data: created });

    const store = useSessionStore();
    store.sessions = [{ id: 1, year: "3A" }];

    await store.createSession({ year: "5A", name: "Reseau" });
    expect(store.sessions.some((s) => s.id === 5)).toBe(false);
  });

  it("fetchSessionById retourne null en cas d'erreur", async () => {
    apiClient.get.mockRejectedValue(new Error("boom"));
    const store = useSessionStore();

    const result = await store.fetchSessionById(9);
    expect(result).toBeNull();
  });

  it("updateSession met a jour la session", async () => {
    apiClient.put.mockResolvedValue({});
    const store = useSessionStore();
    store.sessions = [{ id: 2, name: "Avant" }];

    const ok = await store.updateSession({ id: 2, name: "Apres" });
    expect(ok).toBe(true);
    expect(store.sessions[0].name).toBe("Apres");
  });

  it("updateSession retourne false en cas d'echec", async () => {
    apiClient.put.mockRejectedValue(new Error("boom"));
    const store = useSessionStore();

    const ok = await store.updateSession({ id: 2, name: "Apres" });
    expect(ok).toBe(false);
  });

  it("deleteSession retire la session", async () => {
    apiClient.delete.mockResolvedValue({});
    const store = useSessionStore();
    store.sessions = [{ id: 1 }, { id: 2 }];

    const ok = await store.deleteSession(1);
    expect(ok).toBe(true);
    expect(store.sessions).toEqual([{ id: 2 }]);
  });

  it("deleteSession retourne false en cas d'echec", async () => {
    apiClient.delete.mockRejectedValue(new Error("boom"));
    const store = useSessionStore();

    const ok = await store.deleteSession(1);
    expect(ok).toBe(false);
  });

  it("addStudentsToSessionByNumber retourne le resultat success/failed", async () => {
    const store = useSessionStore();
    vi.spyOn(store, "fetchSessionById").mockResolvedValue({ id: 10 });
    apiClient.get
      .mockResolvedValueOnce({ data: { exists: true } })
      .mockResolvedValueOnce({ data: { exists: false } });
    apiClient.post.mockResolvedValueOnce({ data: {} });

    const result = await store.addStudentsToSessionByNumber(10, [
      { studentNumber: "S1" },
      { studentNumber: "S2" },
    ]);

    expect(result.success).toEqual(["S1"]);
    expect(result.failed).toHaveLength(1);
  });

  it("addStudentsToSessionByNumber ne fait rien si tableau vide", async () => {
    const store = useSessionStore();

    const result = await store.addStudentsToSessionByNumber(10, []);
    expect(result).toBeUndefined();
  });

  it("addStudentsToSessionByNumber throw si session introuvable", async () => {
    const store = useSessionStore();
    vi.spyOn(store, "fetchSessionById").mockResolvedValue(null);

    await expect(
      store.addStudentsToSessionByNumber(10, [{ studentNumber: "S1" }]),
    ).rejects.toThrow("n'existe pas");
  });

  it("getCurrentSession retourne null sur 404", async () => {
    apiClient.get.mockRejectedValue({ response: { status: 404 } });
    const store = useSessionStore();

    const result = await store.getCurrentSession("3A");
    expect(result).toBeNull();
    expect(store.currentSession).toBeNull();
  });

  it("getCurrentSession retourne la session en succes", async () => {
    apiClient.get.mockResolvedValue({ data: { id: 11 } });
    const store = useSessionStore();

    const result = await store.getCurrentSession("3A");
    expect(result).toEqual({ id: 11 });
  });

  it("validatePresence retourne la reponse API", async () => {
    apiClient.post.mockResolvedValue({ data: { ok: true } });
    const store = useSessionStore();

    const result = await store.validatePresence("S1", 5, "ABC");
    expect(result).toEqual({ ok: true });
  });

  it("validatePresence propage l'erreur et renseigne message", async () => {
    apiClient.post.mockRejectedValue({
      response: { data: { message: "Code invalide" } },
    });
    const store = useSessionStore();

    await expect(store.validatePresence("S1", 5, "BAD")).rejects.toBeTruthy();
    expect(store.error).toBe("Code invalide");
  });

  it("getAttendance retourne la presence", async () => {
    apiClient.get.mockResolvedValue({ data: { status: "Present" } });
    const store = useSessionStore();

    const result = await store.getAttendance("S1", 5);
    expect(result.status).toBe("Present");
  });

  it("getAttendance retourne null en cas d'erreur", async () => {
    apiClient.get.mockRejectedValue(new Error("boom"));
    const store = useSessionStore();

    const result = await store.getAttendance("S1", 5);
    expect(result).toBeNull();
  });

  it("saveSignature retourne la reponse brute axios", async () => {
    apiClient.post.mockResolvedValue({ data: { saved: true } });
    const store = useSessionStore();

    const result = await store.saveSignature("S1", "sig");
    expect(result.data.saved).toBe(true);
  });

  it("saveSignature retourne null en cas d'erreur", async () => {
    apiClient.post.mockRejectedValue(new Error("boom"));
    const store = useSessionStore();

    const result = await store.saveSignature("S1", "sig");
    expect(result).toBeNull();
  });

  it("getSignature retourne la signature", async () => {
    apiClient.get.mockResolvedValue({ data: { signature: "sig" } });
    const store = useSessionStore();

    const result = await store.getSignature("S1");
    expect(result.signature).toBe("sig");
  });

  it("getSignature retourne null en cas d'erreur", async () => {
    apiClient.get.mockRejectedValue(new Error("boom"));
    const store = useSessionStore();

    const result = await store.getSignature("S1");
    expect(result).toBeNull();
  });

  it("getSessionExportData retourne session + attendances", async () => {
    apiClient.get
      .mockResolvedValueOnce({ data: { id: 3 } })
      .mockResolvedValueOnce({ data: [{ studentNumber: "S1" }] });
    const store = useSessionStore();

    const result = await store.getSessionExportData(3);
    expect(result.session.id).toBe(3);
    expect(result.attendances).toHaveLength(1);
  });

  it("getSessionExportData retourne null en cas d'erreur", async () => {
    apiClient.get.mockRejectedValue(new Error("boom"));
    const store = useSessionStore();

    const result = await store.getSessionExportData(3);
    expect(result).toBeNull();
  });

  it("fetchSessionsByFilters applique filtre de date", async () => {
    const store = useSessionStore();
    vi.spyOn(store, "fetchAllSessions").mockImplementation(async () => {
      store.sessions = [
        { id: 1, date: "2026-01-01T08:00:00" },
        { id: 2, date: "2026-02-01T08:00:00" },
      ];
      return store.sessions;
    });

    const result = await store.fetchSessionsByFilters({
      startDate: "2026-02-01",
    });
    expect(result.map((s) => s.id)).toEqual([2]);
  });

  it("fetchSessionsByFilters utilise fetchSessionsByYear si annee presente", async () => {
    const store = useSessionStore();
    vi.spyOn(store, "fetchSessionsByYear").mockImplementation(async () => {
      store.sessions = [{ id: 1 }];
      return store.sessions;
    });

    const result = await store.fetchSessionsByFilters({ year: "3A" });
    expect(result).toEqual([{ id: 1 }]);
  });

  it("fetchSessionsByFilters retourne [] en cas d'erreur", async () => {
    const store = useSessionStore();
    vi.spyOn(store, "fetchAllSessions").mockRejectedValue(new Error("boom"));

    const result = await store.fetchSessionsByFilters({});
    expect(result).toEqual([]);
  });

  it("setProfEmail appelle l'API et retourne true", async () => {
    apiClient.post.mockResolvedValue({});
    const store = useSessionStore();
    const ok = await store.setProfEmail(1, "p1@test.fr");
    expect(ok).toBe(true);
  });

  it("resendProfMail appelle l'API", async () => {
    apiClient.post.mockResolvedValue({});
    const store = useSessionStore();
    const ok = await store.resendProfMail(1);
    expect(ok).toBe(true);
  });

  it("setProf2Email appelle l'API", async () => {
    apiClient.post.mockResolvedValue({});
    const store = useSessionStore();
    const ok = await store.setProf2Email(1, "p2@test.fr");
    expect(ok).toBe(true);
  });

  it("resendProf2Mail appelle l'API", async () => {
    apiClient.post.mockResolvedValue({});
    const store = useSessionStore();
    const ok = await store.resendProf2Mail(1);
    expect(ok).toBe(true);
  });

  it("setSessionProfessor retourne false et renseigne error en echec", async () => {
    apiClient.post.mockRejectedValue(new Error("boom"));
    const store = useSessionStore();

    const ok = await store.setSessionProfessor(1, 5, 9);
    expect(ok).toBe(false);
    expect(store.error).toBe("boom");
  });

  it("setSessionProfessor retourne true en succes", async () => {
    apiClient.post.mockResolvedValue({});
    const store = useSessionStore();

    const ok = await store.setSessionProfessor(1, 5, 9);
    expect(ok).toBe(true);
  });

  it("getSessionAttendances retourne $values", async () => {
    apiClient.get.mockResolvedValue({ data: { $values: [{ id: 1 }] } });
    const store = useSessionStore();

    const result = await store.getSessionAttendances(8);
    expect(result).toEqual([{ id: 1 }]);
  });

  it("getSessionAttendances retourne [] en cas d'erreur", async () => {
    apiClient.get.mockRejectedValue(new Error("boom"));
    const store = useSessionStore();

    const result = await store.getSessionAttendances(8);
    expect(result).toEqual([]);
  });

  it("changeAttendanceStatus retourne true", async () => {
    apiClient.post.mockResolvedValue({});
    const store = useSessionStore();

    const ok = await store.changeAttendanceStatus(8, "S1", "Present");
    expect(ok).toBe(true);
  });

  it("changeAttendanceStatus retourne false en cas d'erreur", async () => {
    apiClient.post.mockRejectedValue(new Error("boom"));
    const store = useSessionStore();

    const ok = await store.changeAttendanceStatus(8, "S1", "Present");
    expect(ok).toBe(false);
  });

  it("resetStore remet l'etat initial", () => {
    const store = useSessionStore();
    store.sessions = [{ id: 1 }];
    store.currentSession = { id: 1 };
    store.loading = true;
    store.error = "x";

    store.resetStore();
    expect(store.sessions).toEqual([]);
    expect(store.currentSession).toBeNull();
    expect(store.loading).toBe(false);
    expect(store.error).toBeNull();
  });

  it("updateAttendanceComment retourne true", async () => {
    apiClient.post.mockResolvedValue({});
    const store = useSessionStore();

    const ok = await store.updateAttendanceComment(8, "S1", "RAS");
    expect(ok).toBe(true);
  });

  it("updateAttendanceComment retourne false en cas d'erreur", async () => {
    apiClient.post.mockRejectedValue(new Error("boom"));
    const store = useSessionStore();

    const ok = await store.updateAttendanceComment(8, "S1", "RAS");
    expect(ok).toBe(false);
  });
});
