<template>
  <div class="specialization-page">
    <!-- Page Header -->
    <div class="page-header">
      <div class="page-title">
        <h1>Filières</h1>
        <p class="page-subtitle">Gérez les spécialisations proposées.</p>
      </div>
      <button
        class="btn btn-success"
        @click="showAddForm = true"
        v-if="!showAddForm && !editingId"
      >
        + Ajouter une filière
      </button>
    </div>

    <!-- Form -->
    <Transition name="slide">
      <div v-if="showAddForm || editingId" class="form-card">
        <div class="form-card-header">
          <h3>{{ editingId ? "Modifier la filière" : "Nouvelle filière" }}</h3>
        </div>
        <div class="form-card-body">
          <div class="form-row">
            <div class="form-field">
              <label>Nom</label>
              <input
                v-model="form.name"
                type="text"
                placeholder="Ex: Informatique"
              />
            </div>
            <div class="form-field">
              <label>Code</label>
              <input v-model="form.code" type="text" placeholder="Ex: INFO" />
            </div>
          </div>
          <div class="form-field">
            <label>Description</label>
            <input
              v-model="form.description"
              type="text"
              placeholder="Description (optionnel)"
            />
          </div>
          <div class="form-actions">
            <button
              class="btn btn-primary"
              @click="saveSpecialization"
              :disabled="!form.name || !form.code"
            >
              {{ editingId ? "Enregistrer" : "Créer" }}
            </button>
            <button class="btn btn-ghost" @click="cancelForm">Annuler</button>
          </div>
          <p v-if="formError" class="form-error">{{ formError }}</p>
        </div>
      </div>
    </Transition>

    <!-- Table -->
    <div class="table-card">
      <table v-if="specializations.length > 0">
        <thead>
          <tr>
            <th>Nom</th>
            <th>Code</th>
            <th class="hide-mobile">Description</th>
            <th>Statut</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="spec in specializations"
            :key="spec.id"
            :class="{ 'editing-row': editingId === spec.id }"
          >
            <td class="cell-name">{{ spec.name }}</td>
            <td>
              <code>{{ spec.code }}</code>
            </td>
            <td class="hide-mobile cell-desc">{{ spec.description || "—" }}</td>
            <td>
              <span
                :class="[
                  'status-badge',
                  spec.isActive ? 'badge-active' : 'badge-inactive',
                ]"
              >
                {{ spec.isActive ? "Active" : "Inactive" }}
              </span>
            </td>
            <td>
              <div class="cell-actions">
                <button
                  class="btn-icon btn-edit"
                  @click="editSpecialization(spec)"
                  title="Modifier"
                >
                  ✏️
                </button>
                <button
                  class="btn-icon"
                  :class="spec.isActive ? 'btn-deactivate' : 'btn-activate'"
                  @click="toggleActive(spec)"
                  :title="spec.isActive ? 'Désactiver' : 'Réactiver'"
                >
                  {{ spec.isActive ? "⏸" : "▶" }}
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
      <div v-else-if="!specializationStore.loading" class="empty-state">
        <div class="empty-text">Aucune filière trouvée.</div>
      </div>
    </div>

    <div v-if="specializationStore.loading" class="state-card">
      <div class="spinner"></div>
      <p>Chargement...</p>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, computed } from "vue";
import { useSpecializationStore } from "../../stores/specializationStore.js";

const specializationStore = useSpecializationStore();

const specializations = computed(() => specializationStore.specializations);

const showAddForm = ref(false);
const editingId = ref(null);
const formError = ref("");

const form = ref({
  name: "",
  code: "",
  description: "",
});

onMounted(async () => {
  await specializationStore.fetchSpecializations();
});

const resetForm = () => {
  form.value = { name: "", code: "", description: "" };
  editingId.value = null;
  showAddForm.value = false;
  formError.value = "";
};

const cancelForm = () => {
  resetForm();
};

const editSpecialization = (spec) => {
  editingId.value = spec.id;
  form.value = {
    name: spec.name,
    code: spec.code,
    description: spec.description || "",
  };
  showAddForm.value = false;
};

const saveSpecialization = async () => {
  formError.value = "";
  try {
    if (editingId.value) {
      const existing = specializations.value.find(
        (s) => s.id === editingId.value,
      );
      await specializationStore.updateSpecialization(editingId.value, {
        ...form.value,
        isActive: existing?.isActive ?? true,
      });
    } else {
      await specializationStore.createSpecialization({
        ...form.value,
        isActive: true,
      });
    }
    resetForm();
  } catch (e) {
    formError.value =
      e.response?.data?.message || "Erreur lors de la sauvegarde.";
  }
};

const toggleActive = async (spec) => {
  try {
    if (spec.isActive) {
      await specializationStore.deleteSpecialization(spec.id);
    } else {
      await specializationStore.updateSpecialization(spec.id, {
        name: spec.name,
        code: spec.code,
        description: spec.description,
        isActive: true,
      });
    }
  } catch (e) {
    formError.value =
      e.response?.data?.message || "Erreur lors de l'opération.";
  }
};
</script>

<style scoped>
.specialization-page {
  max-width: 960px;
  margin: 0 auto;
  width: 100%;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 20px;
  flex-wrap: wrap;
  gap: 16px;
}

.page-title h1 {
  font-size: 1.6rem;
  font-weight: 700;
  color: #1a1a2e;
  margin-bottom: 2px;
}

.page-subtitle {
  color: #6c757d;
  font-size: 0.92rem;
  margin: 0;
}

/* Buttons */
.btn {
  padding: 9px 18px;
  border: none;
  border-radius: 8px;
  font-size: 0.9rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.btn-success {
  background: #27ae60;
  color: #fff;
}

.btn-success:hover {
  background: #219a52;
  box-shadow: 0 4px 12px rgba(39, 174, 96, 0.25);
}

.btn-primary {
  background: #3498db;
  color: #fff;
}

.btn-primary:hover:not(:disabled) {
  background: #2980b9;
}

.btn-primary:disabled {
  background: #b2bec3;
  cursor: not-allowed;
}

.btn-ghost {
  background: #f0f2f5;
  color: #495057;
}

.btn-ghost:hover {
  background: #e2e6ea;
}

/* Form Card */
.form-card {
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 12px;
  overflow: hidden;
  margin-bottom: 20px;
}

.form-card-header {
  padding: 16px 22px;
  border-bottom: 1px solid #f0f0f5;
}

.form-card-header h3 {
  margin: 0;
  font-size: 1.05rem;
  font-weight: 600;
  color: #1a1a2e;
}

.form-card-body {
  padding: 22px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

.form-field {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.form-field label {
  font-size: 0.78rem;
  font-weight: 600;
  color: #6c757d;
  text-transform: uppercase;
  letter-spacing: 0.4px;
}

.form-field input {
  padding: 10px 12px;
  border: 1px solid #d1d5db;
  border-radius: 8px;
  font-size: 0.95rem;
  color: #1a1a2e;
  outline: none;
  transition:
    border-color 0.2s,
    box-shadow 0.2s;
}

.form-field input:focus {
  border-color: #3498db;
  box-shadow: 0 0 0 3px rgba(52, 152, 219, 0.1);
}

.form-actions {
  display: flex;
  gap: 10px;
}

.form-error {
  color: #e74c3c;
  font-size: 0.9rem;
  margin: 0;
}

/* Table */
.table-card {
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 12px;
  overflow: hidden;
}

table {
  width: 100%;
  border-collapse: collapse;
}

thead {
  background: #f8f9fb;
}

th {
  text-align: left;
  padding: 11px 16px;
  font-size: 0.78rem;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: #6c757d;
  font-weight: 600;
  border-bottom: 1px solid #f0f0f5;
}

td {
  padding: 13px 16px;
  border-bottom: 1px solid #f5f5f8;
  font-size: 0.92rem;
  vertical-align: middle;
}

tbody tr {
  transition: background 0.15s;
}

tbody tr:hover {
  background: #f8f9fb;
}

tbody tr.editing-row {
  background: #eef6ff;
}

.cell-name {
  font-weight: 600;
  color: #1a1a2e;
}

.cell-desc {
  color: #6c757d;
}

code {
  background: #f0f2f5;
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 0.85rem;
  color: #495057;
}

.status-badge {
  padding: 3px 10px;
  border-radius: 6px;
  font-size: 0.8rem;
  font-weight: 700;
}

.badge-active {
  background: #d4edda;
  color: #155724;
}

.badge-inactive {
  background: #f8d7da;
  color: #721c24;
}

.cell-actions {
  display: flex;
  gap: 6px;
}

.btn-icon {
  width: 34px;
  height: 34px;
  display: flex;
  align-items: center;
  justify-content: center;
  border: 1px solid #e0e4ea;
  border-radius: 8px;
  background: #fff;
  cursor: pointer;
  font-size: 0.88rem;
  transition: all 0.15s;
}

.btn-icon:hover {
  transform: translateY(-1px);
}

.btn-edit:hover {
  background: #fff8e1;
  border-color: #ffca28;
}

.btn-deactivate:hover {
  background: #ffebee;
  border-color: #ef5350;
}

.btn-activate:hover {
  background: #e8f5e9;
  border-color: #66bb6a;
}

/* States */
.empty-state {
  padding: 48px 24px;
  text-align: center;
  color: #6c757d;
}

.empty-icon {
  font-size: 2.5rem;
  margin-bottom: 12px;
}

.state-card {
  text-align: center;
  padding: 48px 24px;
  color: #6c757d;
}

.spinner {
  width: 28px;
  height: 28px;
  border: 3px solid #e0e4ea;
  border-top-color: #3498db;
  border-radius: 50%;
  animation: spin 0.7s linear infinite;
  margin: 0 auto 16px;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

/* Transitions */
.slide-enter-active,
.slide-leave-active {
  transition: all 0.25s ease;
}
.slide-enter-from,
.slide-leave-to {
  opacity: 0;
  transform: translateY(-12px);
}

/* Responsive */
@media (max-width: 768px) {
  .hide-mobile {
    display: none;
  }

  .form-row {
    grid-template-columns: 1fr;
  }
}
</style>
