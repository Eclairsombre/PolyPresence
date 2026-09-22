<template>
  <div class="groups-page">
    <div class="page-header">
      <div>
        <h1>Groupes</h1>
        <p class="subtitle">
          Les groupes sont découverts automatiquement à l'import des emplois du
          temps. Cet écran sert à les ranger et à déclarer ceux dont le
          calendrier n'existe pas encore.
        </p>
      </div>
      <button class="btn btn-primary" @click="showCreate = !showCreate">
        {{ showCreate ? "Fermer" : "Déclarer un groupe" }}
      </button>
    </div>

    <!-- Déclaration manuelle -->
    <section v-if="showCreate" class="card create-card">
      <h2>Déclarer un groupe à la main</h2>
      <p class="card-hint">
        Utile quand un emploi du temps n'est pas encore publié — le cas des
        langues — mais que la répartition des étudiants est déjà connue. Le
        libellé doit être <strong>exactement</strong> celui d'ADE, sinon
        l'import créera un doublon au lieu de rattacher les séances.
      </p>

      <form class="create-form" @submit.prevent="submitCreate">
        <div class="field">
          <label for="new-label">Libellé ADE <span class="req">*</span></label>
          <input
            id="new-label"
            v-model="newGroup.label"
            type="text"
            required
            placeholder="A-PL9004TR-BE91"
            autocomplete="off"
          />
        </div>
        <div class="field">
          <label for="new-display">Nom affiché</label>
          <input
            id="new-display"
            v-model="newGroup.displayName"
            type="text"
            placeholder="Espagnol groupe A"
            autocomplete="off"
          />
        </div>
        <div class="field">
          <label for="new-type">Type</label>
          <select id="new-type" v-model.number="newGroup.type">
            <option
              v-for="(label, value) in GROUP_TYPE_LABEL"
              :key="value"
              :value="Number(value)"
            >
              {{ label }}
            </option>
          </select>
        </div>
        <div class="field">
          <label for="new-spec">Filière</label>
          <select id="new-spec" v-model="newGroup.specializationId">
            <option :value="null">—</option>
            <option v-for="spec in specializations" :key="spec.id" :value="spec.id">
              {{ spec.name }} ({{ spec.code }})
            </option>
          </select>
        </div>
        <div class="field">
          <label for="new-year">Année</label>
          <select id="new-year" v-model="newGroup.year">
            <option :value="null">—</option>
            <option value="3A">3A</option>
            <option value="4A">4A</option>
            <option value="5A">5A</option>
          </select>
        </div>
        <div class="field field-actions">
          <button class="btn btn-primary" type="submit" :disabled="!newGroup.label">
            Créer
          </button>
        </div>
      </form>
      <p v-if="createError" class="inline-error">{{ createError }}</p>
    </section>

    <!-- Filtres -->
    <section class="card filters">
      <input
        v-model="search"
        type="search"
        class="filter-search"
        placeholder="Rechercher un groupe…"
      />
      <select v-model="typeFilter">
        <option :value="null">Tous les types</option>
        <option
          v-for="(label, value) in GROUP_TYPE_LABEL"
          :key="value"
          :value="Number(value)"
        >
          {{ label }}
        </option>
      </select>
      <select v-model="specFilter">
        <option :value="null">Toutes les filières</option>
        <option v-for="spec in specializations" :key="spec.id" :value="spec.id">
          {{ spec.code }}
        </option>
      </select>
      <label class="checkbox">
        <input v-model="includeInactive" type="checkbox" />
        Afficher les groupes masqués
      </label>
    </section>

    <!-- Tableau -->
    <section class="card">
      <div v-if="groupStore.loading" class="empty">Chargement…</div>
      <div v-else-if="filtered.length === 0" class="empty">
        Aucun groupe. Ils apparaissent après le premier import d'un emploi du
        temps.
      </div>

      <table v-else class="groups-table">
        <thead>
          <tr>
            <th>Libellé ADE</th>
            <th>Nom affiché</th>
            <th style="width: 150px">Type</th>
            <th style="width: 80px">Filière</th>
            <th style="width: 70px">Année</th>
            <th style="width: 80px" title="Séances portant ce libellé au dernier import">
              Séances
            </th>
            <th style="width: 90px">Étudiants</th>
            <th style="width: 90px">Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="group in filtered" :key="group.id" :class="{ inactive: !group.isActive }">
            <td>
              <code class="ade-label">{{ group.label }}</code>
            </td>
            <td>
              <input
                class="inline-input"
                :value="group.displayName"
                @change="rename(group, $event)"
              />
            </td>
            <td>
              <select
                class="inline-input"
                :value="group.type"
                @change="retype(group, $event)"
              >
                <option
                  v-for="(label, value) in GROUP_TYPE_LABEL"
                  :key="value"
                  :value="Number(value)"
                >
                  {{ label }}
                </option>
              </select>
            </td>
            <td>
              <span class="badge">{{ group.specializationCode || "—" }}</span>
            </td>
            <td>{{ group.year || "—" }}</td>
            <td class="num" :class="{ zero: group.sessionCount === 0 }">
              {{ group.sessionCount }}
            </td>
            <td class="num" :class="{ zero: group.studentCount === 0 }">
              {{ group.studentCount }}
            </td>
            <td>
              <button
                v-if="group.isActive"
                class="btn-link danger"
                :disabled="group.studentCount > 0"
                :title="
                  group.studentCount > 0
                    ? 'Réaffectez d\'abord les étudiants rattachés à ce groupe.'
                    : 'Masquer ce groupe'
                "
                @click="deactivate(group)"
              >
                Masquer
              </button>
              <button v-else class="btn-link" @click="reactivate(group)">
                Réafficher
              </button>
            </td>
          </tr>
        </tbody>
      </table>

      <p v-if="filtered.length > 0" class="table-hint">
        Un groupe à <strong>0 étudiant</strong> vient en général d'un cours
        mutualisé avec une autre filière : il est sans effet et peut être masqué.
        Un groupe à <strong>0 séance</strong> est un libellé qu'ADE n'utilise
        plus, ou dont le calendrier n'est pas encore publié.
      </p>
    </section>

    <p v-if="actionError" class="inline-error">{{ actionError }}</p>
  </div>
</template>

<script setup>
import { computed, onMounted, ref, watch } from "vue";
import {
  useGroupStore,
  GROUP_TYPE,
  GROUP_TYPE_LABEL,
} from "../../stores/groupStore.js";
import { useSpecializationStore } from "../../stores/specializationStore.js";

const groupStore = useGroupStore();
const specializationStore = useSpecializationStore();

const search = ref("");
const typeFilter = ref(null);
const specFilter = ref(null);
const includeInactive = ref(false);
const showCreate = ref(false);
const createError = ref("");
const actionError = ref("");

const newGroup = ref({
  label: "",
  displayName: "",
  type: GROUP_TYPE.LANGUAGE,
  specializationId: null,
  year: null,
});

const specializations = computed(
  () => specializationStore.activeSpecializations,
);

const load = () => groupStore.fetchGroups({ includeInactive: includeInactive.value });

onMounted(async () => {
  await specializationStore.fetchSpecializations();
  await load();
});

watch(includeInactive, load);

// Le filtrage se fait côté client : le volume tient largement en mémoire et
// la réponse est instantanée pendant la frappe.
const filtered = computed(() => {
  const term = search.value.trim().toLowerCase();
  return groupStore.groups.filter((g) => {
    if (typeFilter.value !== null && g.type !== typeFilter.value) return false;
    if (specFilter.value !== null && g.specializationId !== specFilter.value) {
      return false;
    }
    if (!term) return true;
    return (
      g.label.toLowerCase().includes(term) ||
      g.displayName.toLowerCase().includes(term)
    );
  });
});

const run = async (action) => {
  actionError.value = "";
  try {
    await action();
    await load();
  } catch (e) {
    actionError.value =
      e.response?.data?.message || e.message || "Action impossible.";
  }
};

const rename = (group, event) => {
  const displayName = event.target.value.trim();
  if (!displayName || displayName === group.displayName) return;
  run(() => groupStore.updateGroup(group.id, { displayName }));
};

const retype = (group, event) => {
  const type = Number(event.target.value);
  if (type === group.type) return;
  run(() => groupStore.updateGroup(group.id, { type }));
};

const deactivate = (group) =>
  run(() => groupStore.deactivateGroup(group.id));

const reactivate = (group) =>
  run(() => groupStore.updateGroup(group.id, { isActive: true }));

const submitCreate = async () => {
  createError.value = "";
  try {
    await groupStore.createGroup({
      label: newGroup.value.label.trim(),
      displayName: newGroup.value.displayName.trim() || null,
      type: newGroup.value.type,
      specializationId: newGroup.value.specializationId,
      year: newGroup.value.year,
    });
    newGroup.value = {
      label: "",
      displayName: "",
      type: GROUP_TYPE.LANGUAGE,
      specializationId: null,
      year: null,
    };
    await load();
  } catch (e) {
    createError.value =
      e.response?.data?.message || e.message || "Création impossible.";
  }
};
</script>

<style scoped>
.groups-page {
  width: 100%;
  max-width: 1100px;
  margin: 0 auto;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 24px;
  margin-bottom: 20px;
  flex-wrap: wrap;
}

h1 {
  margin: 0 0 4px;
  font-size: 1.5em;
  color: #23303d;
}

.subtitle {
  margin: 0;
  color: #5b6672;
  font-size: 0.88em;
  max-width: 620px;
  line-height: 1.45;
}

.card {
  background: #fff;
  border: 1px solid #e2e7ee;
  border-radius: 8px;
  padding: 16px;
  margin-bottom: 16px;
}

.card h2 {
  margin: 0 0 6px;
  font-size: 1.05em;
  color: #23303d;
}

.card-hint {
  margin: 0 0 14px;
  font-size: 0.83em;
  color: #5b6672;
  line-height: 1.5;
}

.create-form {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(170px, 1fr));
  gap: 12px;
  align-items: end;
}

.field {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.field label {
  font-size: 0.82em;
  font-weight: 600;
  color: #3d4756;
}

.req {
  color: #c0392b;
}

input,
select {
  padding: 7px 9px;
  border: 1px solid #d7dce3;
  border-radius: 5px;
  font-size: 0.9em;
  background: #fff;
  color: #23303d;
}

.filters {
  display: flex;
  gap: 12px;
  align-items: center;
  flex-wrap: wrap;
}

.filter-search {
  flex: 1;
  min-width: 180px;
}

.checkbox {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 0.85em;
  color: #3d4756;
}

.groups-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.87em;
}

.groups-table th {
  text-align: left;
  padding: 8px 10px;
  border-bottom: 2px solid #e2e7ee;
  color: #5b6672;
  font-weight: 600;
}

.groups-table td {
  padding: 6px 10px;
  border-bottom: 1px solid #eef1f5;
  color: #23303d;
}

tr.inactive {
  opacity: 0.55;
}

.ade-label {
  font-size: 0.92em;
  background: #f2f5f9;
  padding: 2px 6px;
  border-radius: 4px;
  color: #3d4756;
}

.inline-input {
  width: 100%;
  padding: 4px 6px;
  font-size: 0.95em;
}

.badge {
  display: inline-block;
  padding: 2px 8px;
  background: #e8eef7;
  color: #2c3e50;
  border-radius: 10px;
  font-size: 0.85em;
  font-weight: 600;
}

.num {
  text-align: right;
  font-variant-numeric: tabular-nums;
}

.num.zero {
  color: #a0a8b3;
}

.btn {
  padding: 8px 16px;
  border: none;
  border-radius: 5px;
  cursor: pointer;
  font-weight: 500;
  font-size: 0.9em;
}

.btn-primary {
  background: #2c3e50;
  color: #fff;
}

.btn-primary:disabled {
  background: #9aa5b4;
  cursor: not-allowed;
}

.btn-link {
  border: none;
  background: none;
  padding: 0;
  cursor: pointer;
  font-size: 0.88em;
  color: #2c6fb5;
  text-decoration: underline;
}

.btn-link.danger {
  color: #a33a3a;
}

.btn-link:disabled {
  color: #a0a8b3;
  cursor: not-allowed;
  text-decoration: none;
}

.empty {
  padding: 24px;
  text-align: center;
  color: #5b6672;
  font-size: 0.9em;
}

.table-hint {
  margin: 12px 0 0;
  font-size: 0.79em;
  color: #5b6672;
  line-height: 1.5;
}

.inline-error {
  margin: 0 0 16px;
  padding: 9px 12px;
  background: #fdf3f3;
  border: 1px solid #f0c2c2;
  border-radius: 5px;
  color: #a33a3a;
  font-size: 0.85em;
}

@media (max-width: 700px) {
  .groups-table th:nth-child(4),
  .groups-table td:nth-child(4),
  .groups-table th:nth-child(5),
  .groups-table td:nth-child(5) {
    display: none;
  }
}
</style>
