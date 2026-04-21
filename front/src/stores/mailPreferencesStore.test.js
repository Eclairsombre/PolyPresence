import { beforeEach, describe, expect, it, vi } from "vitest";
import { createPinia, setActivePinia } from "pinia";
import axios from "axios";
import { useMailPreferencesStore } from "./mailPreferencesStore";

vi.mock("axios", () => ({
  default: {
    get: vi.fn(),
    put: vi.fn(),
    post: vi.fn(),
  },
}));

describe("mailPreferencesStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  it("fetchMailPreferences renseigne preferences", async () => {
    axios.get.mockResolvedValue({
      data: { emailTo: "x@test.fr", active: true },
    });
    const store = useMailPreferencesStore();

    const res = await store.fetchMailPreferences("S1");

    expect(res.active).toBe(true);
    expect(store.preferences.emailTo).toBe("x@test.fr");
  });

  it("fetchMailPreferences retourne null en cas d'erreur", async () => {
    axios.get.mockRejectedValue(new Error("boom"));
    const store = useMailPreferencesStore();

    const res = await store.fetchMailPreferences("S1");
    expect(res).toBeNull();
    expect(store.error).toBeTruthy();
  });

  it("testMail renseigne un message d'echec si l'API echoue", async () => {
    axios.post.mockRejectedValue(new Error("smtp"));
    const store = useMailPreferencesStore();

    await store.testMail("a@b.fr");

    expect(store.testMessage).toContain("Échec");
  });

  it("getPdfBlob retourne un Blob si la requete reussit", async () => {
    axios.get.mockResolvedValue({ data: new Uint8Array([1, 2, 3]) });
    const store = useMailPreferencesStore();

    const blob = await store.getPdfBlob({ id: 10 });

    expect(blob).toBeInstanceOf(Blob);
    expect(blob.type).toBe("application/pdf");
  });

  it("updateMailPreferences met un message de succes", async () => {
    axios.put.mockResolvedValue({});
    const store = useMailPreferencesStore();

    await store.updateMailPreferences("S1", { enabled: true });
    expect(store.successMessage).toContain("mises à jour");
  });

  it("updateMailPreferences vide successMessage en cas d'erreur", async () => {
    axios.put.mockRejectedValue(new Error("boom"));
    const store = useMailPreferencesStore();

    await store.updateMailPreferences("S1", { enabled: true });
    expect(store.successMessage).toBe("");
    expect(store.error).toBeTruthy();
  });

  it("testMail renseigne un message de succes", async () => {
    axios.post.mockResolvedValue({});
    const store = useMailPreferencesStore();

    await store.testMail("a@b.fr");
    expect(store.testMessage).toContain("succès");
  });

  it("fetchTimers utilise le cache recent", async () => {
    const now = Date.now();
    vi.spyOn(Date, "now").mockReturnValue(now);
    const store = useMailPreferencesStore();
    store.lastTimerFetch = now;
    store.timerData = { enabled: true };

    const result = await store.fetchTimers();
    expect(result).toEqual({ enabled: true });
    expect(axios.get).not.toHaveBeenCalled();
  });

  it("fetchTimers charge depuis l'API hors cache", async () => {
    axios.get.mockResolvedValue({ data: { next: "10:00" } });
    const store = useMailPreferencesStore();

    await store.fetchTimers();
    expect(store.timerData).toEqual({ next: "10:00" });
  });

  it("fetchTimers met timerData a null en cas d'erreur", async () => {
    axios.get.mockRejectedValue(new Error("boom"));
    const store = useMailPreferencesStore();

    await store.fetchTimers();
    expect(store.timerData).toBeNull();
  });

  it("resetMessages vide les messages", () => {
    const store = useMailPreferencesStore();
    store.successMessage = "ok";
    store.testMessage = "ok";

    store.resetMessages();
    expect(store.successMessage).toBe("");
    expect(store.testMessage).toBe("");
  });

  it("getSessionPdf telecharge le blob", async () => {
    axios.get.mockResolvedValue({ data: new Uint8Array([1]) });
    const createObjectURL = vi.fn(() => "blob:url");
    window.URL.createObjectURL = createObjectURL;

    const appendChildSpy = vi.spyOn(document.body, "appendChild");
    const clickMock = vi.fn();
    vi.spyOn(document, "createElement").mockReturnValue({
      setAttribute: vi.fn(),
      click: clickMock,
      href: "",
    });

    const store = useMailPreferencesStore();
    await store.getSessionPdf({
      value: {
        id: 5,
        date: "2026-03-10",
        startTime: "08:30",
        year: "3A",
      },
    });

    expect(createObjectURL).toHaveBeenCalled();
    expect(appendChildSpy).toHaveBeenCalled();
  });

  it("getSessionPdf renseigne error en cas d'echec", async () => {
    axios.get.mockRejectedValue(new Error("boom"));
    const store = useMailPreferencesStore();

    await store.getSessionPdf({
      value: {
        id: 5,
        date: "2026-03-10",
        startTime: "08:30",
        year: "3A",
      },
    });

    expect(store.error).toBeTruthy();
  });

  it("getPdfBlob accepte un id numerique", async () => {
    axios.get.mockResolvedValue({ data: new Uint8Array([1]) });
    const store = useMailPreferencesStore();

    const blob = await store.getPdfBlob(12);
    expect(blob).toBeInstanceOf(Blob);
  });

  it("getPdfBlob accepte le format { value: { id } }", async () => {
    axios.get.mockResolvedValue({ data: new Uint8Array([1]) });
    const store = useMailPreferencesStore();

    const blob = await store.getPdfBlob({ value: { id: 13 } });
    expect(blob).toBeInstanceOf(Blob);
  });

  it("getPdfBlob accepte le format { Id }", async () => {
    axios.get.mockResolvedValue({ data: new Uint8Array([1]) });
    const store = useMailPreferencesStore();

    const blob = await store.getPdfBlob({ Id: 14 });
    expect(blob).toBeInstanceOf(Blob);
  });

  it("getPdfBlob throw sur format inconnu", async () => {
    const store = useMailPreferencesStore();
    await expect(store.getPdfBlob({ nope: true })).rejects.toThrow(
      "Format de session non reconnu",
    );
  });
});
