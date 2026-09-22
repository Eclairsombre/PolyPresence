import { beforeEach, describe, expect, it, vi } from "vitest";
import { createPinia, setActivePinia } from "pinia";
import axios from "axios";
import { useGroupStore, GROUP_TYPE } from "./groupStore";

vi.mock("axios", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

vi.mock("./authStore", () => ({
  useAuthStore: () => ({
    getAdminToken: vi.fn().mockResolvedValue("admin-token"),
  }),
}));

const groups = [
  { id: 1, label: "INFO 1-A", displayName: "INFO 1-A", type: GROUP_TYPE.SUB },
  { id: 2, label: "INFO 1-B", displayName: "INFO 1-B", type: GROUP_TYPE.SUB },
  {
    id: 3,
    label: "Diplôme d'Ingénieur POLYTECH 3A (Informatique)",
    displayName: "INFO 3A",
    type: GROUP_TYPE.PROMO,
  },
  {
    id: 4,
    label: "A-PL9003TR-BE91",
    displayName: "Anglais A",
    type: GROUP_TYPE.LANGUAGE,
  },
  {
    id: 5,
    label: "A-PL9004TR-BE91",
    displayName: "Espagnol A",
    type: GROUP_TYPE.LANGUAGE,
  },
  // Groupe enregistré avant l'unification, encore typé LV2 (3) en base.
  {
    id: 6,
    label: "A-PL9005TR-BE91",
    displayName: "Allemand A",
    type: 3,
  },
];

describe("groupStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  it("fetchGroups charge les groupes", async () => {
    axios.get.mockResolvedValue({ data: groups });
    const store = useGroupStore();

    await store.fetchGroups();

    expect(store.groups).toHaveLength(6);
    expect(store.loading).toBe(false);
  });

  it("fetchGroups accepte l'enveloppe $values", async () => {
    axios.get.mockResolvedValue({ data: { $values: groups } });
    const store = useGroupStore();

    await store.fetchGroups();

    expect(store.groups).toHaveLength(6);
  });

  it("fetchGroups vide la liste en cas d'erreur", async () => {
    axios.get.mockRejectedValue(new Error("boom"));
    const store = useGroupStore();

    const result = await store.fetchGroups();

    expect(result).toEqual([]);
    expect(store.groups).toEqual([]);
    expect(store.error).toBeTruthy();
  });

  it("fetchGroups transmet les filtres en paramètres de requête", async () => {
    axios.get.mockResolvedValue({ data: [] });
    const store = useGroupStore();

    await store.fetchGroups({
      specializationId: 2,
      year: "3A",
      type: GROUP_TYPE.LANGUAGE,
      includeInactive: true,
      search: "anglais",
    });

    expect(axios.get).toHaveBeenCalledWith(expect.stringContaining("/Group"), {
      params: {
        specializationId: 2,
        year: "3A",
        type: GROUP_TYPE.LANGUAGE,
        includeInactive: true,
        search: "anglais",
      },
    });
  });

  it("fetchGroups n'envoie pas de filtre vide", async () => {
    axios.get.mockResolvedValue({ data: [] });
    const store = useGroupStore();

    await store.fetchGroups();

    expect(axios.get).toHaveBeenCalledWith(expect.stringContaining("/Group"), {
      params: {},
    });
  });

  it("le type 0 (sous-groupe) est bien transmis et non traité comme absent", async () => {
    // GROUP_TYPE.SUB vaut 0 : un test de vérité simple l'aurait écarté.
    axios.get.mockResolvedValue({ data: [] });
    const store = useGroupStore();

    await store.fetchGroups({ type: GROUP_TYPE.SUB });

    expect(axios.get).toHaveBeenCalledWith(expect.stringContaining("/Group"), {
      params: { type: 0 },
    });
  });

  it("les getters séparent les groupes par type", async () => {
    axios.get.mockResolvedValue({ data: groups });
    const store = useGroupStore();

    await store.fetchGroups();

    expect(store.subGroups.map((g) => g.label)).toEqual([
      "INFO 1-A",
      "INFO 1-B",
    ]);
    // Les groupes de langue forment un seul vivier : les deux emplacements d'un
    // étudiant y puisent tous les deux, il n'y a pas de groupe "LV1" ni "LV2".
    expect(store.languageGroups.map((g) => g.displayName)).toEqual([
      "Anglais A",
      "Espagnol A",
      "Allemand A",
    ]);
    // Le libellé promo n'est proposé dans aucun emplacement d'affectation.
    expect(store.subGroups.some((g) => g.type === GROUP_TYPE.PROMO)).toBe(false);
    expect(store.languageGroups.some((g) => g.type === GROUP_TYPE.PROMO)).toBe(
      false,
    );
  });

  it("createGroup envoie le jeton admin", async () => {
    axios.post.mockResolvedValue({ data: { id: 9 } });
    const store = useGroupStore();

    await store.createGroup({ label: "B-PL9004TR-BE92" });

    expect(axios.post).toHaveBeenCalledWith(
      expect.stringContaining("/Group"),
      { label: "B-PL9004TR-BE92" },
      { headers: { "Admin-Token": "admin-token" } },
    );
  });

  it("updateGroup cible le bon identifiant", async () => {
    axios.put.mockResolvedValue({});
    const store = useGroupStore();

    await store.updateGroup(7, { displayName: "Groupe A" });

    expect(axios.put).toHaveBeenCalledWith(
      expect.stringContaining("/Group/7"),
      { displayName: "Groupe A" },
      { headers: { "Admin-Token": "admin-token" } },
    );
  });

  it("deactivateGroup propage l'erreur du serveur", async () => {
    // Le backend refuse tant que des étudiants sont rattachés : l'écran doit
    // pouvoir afficher ce message plutôt que d'échouer en silence.
    axios.delete.mockRejectedValue({
      response: { data: { message: "2 étudiant(s) encore rattachés" } },
    });
    const store = useGroupStore();

    await expect(store.deactivateGroup(1)).rejects.toMatchObject({
      response: { data: { message: "2 étudiant(s) encore rattachés" } },
    });
  });
});
