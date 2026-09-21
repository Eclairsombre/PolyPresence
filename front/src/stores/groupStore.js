import { defineStore } from "pinia";
import axios from "axios";
import { useAuthStore } from "./authStore";

const API_URL = import.meta.env.VITE_API_URL || "/api";

/**
 * Types de groupe, alignés sur l'énumération GroupType du backend.
 * L'ordre compte : ce sont les valeurs entières envoyées à l'API.
 */
export const GROUP_TYPE = {
  SUB: 0, // sous-groupe de promotion ("INFO 1-A")
  PROMO: 1, // libellé désignant la promotion entière
  LV1: 2,
  LV2: 3,
};

export const GROUP_TYPE_LABEL = {
  [GROUP_TYPE.SUB]: "Sous-groupe",
  [GROUP_TYPE.PROMO]: "Promotion entière",
  [GROUP_TYPE.LV1]: "LV1",
  [GROUP_TYPE.LV2]: "LV2",
};

/**
 * Store des groupes d'étudiants.
 *
 * Les groupes ne se créent normalement pas à la main : l'import ICS lit les libellés
 * ADE et crée ceux qu'il ne connaît pas. Ce store sert surtout à les lister pour
 * alimenter les sélecteurs d'affectation, et à les ranger depuis l'écran d'admin.
 */
export const useGroupStore = defineStore("group", {
  state: () => ({
    groups: [],
    loading: false,
    error: null,
  }),

  getters: {
    /** Groupes utilisables pour l'affectation d'un étudiant, par type. */
    byType: (state) => (type) => state.groups.filter((g) => g.type === type),

    subGroups: (state) =>
      state.groups.filter((g) => g.type === GROUP_TYPE.SUB),
    lv1Groups: (state) => state.groups.filter((g) => g.type === GROUP_TYPE.LV1),
    lv2Groups: (state) => state.groups.filter((g) => g.type === GROUP_TYPE.LV2),
  },

  actions: {
    async _createAdminConfig() {
      const authStore = useAuthStore();
      const adminToken = await authStore.getAdminToken();
      if (!adminToken) {
        throw new Error("Token admin manquant");
      }
      return { headers: { "Admin-Token": adminToken } };
    },

    /**
     * Charge les groupes, avec filtres optionnels.
     * @param {Object} [filters] - { specializationId, year, type, includeInactive, search }
     */
    async fetchGroups(filters = {}) {
      this.loading = true;
      this.error = null;
      try {
        const params = {};
        if (filters.specializationId) {
          params.specializationId = filters.specializationId;
        }
        if (filters.year) params.year = filters.year;
        if (filters.type !== undefined && filters.type !== null) {
          params.type = filters.type;
        }
        if (filters.includeInactive) params.includeInactive = true;
        if (filters.search) params.search = filters.search;

        const res = await axios.get(`${API_URL}/Group`, { params });
        this.groups = res.data.$values || res.data || [];
        return this.groups;
      } catch (e) {
        this.groups = [];
        this.error = e;
        return [];
      } finally {
        this.loading = false;
      }
    },

    /**
     * Crée un groupe à la main. Sert à pré-déclarer une affectation avant que le
     * calendrier correspondant ne soit publié — le cas des LV aujourd'hui.
     * Le libellé doit être EXACTEMENT celui d'ADE, sinon l'import créera un doublon.
     */
    async createGroup(group) {
      const config = await this._createAdminConfig();
      const res = await axios.post(`${API_URL}/Group`, group, config);
      return res.data;
    },

    async updateGroup(id, changes) {
      const config = await this._createAdminConfig();
      await axios.put(`${API_URL}/Group/${id}`, changes, config);
    },

    /** Désactive un groupe (jamais de suppression : des séances passées le référencent). */
    async deactivateGroup(id) {
      const config = await this._createAdminConfig();
      await axios.delete(`${API_URL}/Group/${id}`, config);
    },
  },
});
