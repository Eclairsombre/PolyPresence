<template>
  <div class="specialization-list-page">
    <h1>Gestion des filières</h1>

    <div class="actions-bar">
      <button class="add-btn" @click="showAddForm = true">
        Ajouter une filière
      </button>
    </div>

    <div v-if="showAddForm || editingId" class="form-container">
      <h3>{{ editingId ? "Modifier la filière" : "Nouvelle filière" }}</h3>
      <div class="form-group">
        <label>Nom :</label>
        <input v-model="form.name" type="text" placeholder="Ex: Informatique" />
      </div>
      <div class="form-group">
        <label>Code :</label>
        <input v-model="form.code" type="text" placeholder="Ex: INFO" />
      </div>
      <div class="form-group">
        <label>Description :</label>
        <input
          v-model="form.description"
          type="text"
          placeholder="Description (optionnel)"
        />
      </div>
      <div class="form-actions">
        <button
          class="save-btn"
          @click="saveSpecialization"
          :disabled="!form.name || !form.code"
        >
          {{ editingId ? "Enregistrer" : "Créer" }}
        </button>
        <button class="cancel-btn" @click="cancelForm">Annuler</button>
      </div>
      <p v-if="formError" class="error-message">{{ formError }}</p>
    </div>

    <table v-if="specializations.length > 0">
      <thead>
        <tr>
          <th>Nom</th>
          <th>Code</th>
          <th>Description</th>
          <th>Statut</th>
          <th>Actions</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="spec in specializations" :key="spec.id">
          <td>{{ spec.name }}</td>
          <td>{{ spec.code }}</td>
          <td>{{ spec.description || "-" }}</td>
          <td>
            <span :class="spec.isActive ? 'badge-active' : 'badge-inactive'">
              {{ spec.isActive ? "Active" : "Inactive" }}
            </span>
          </td>
          <td>
            <button class="edit-btn" @click="editSpecialization(spec)">
              Modifier
            </button>
            <button class="delete-btn" @click="toggleActive(spec)">
              {{ spec.isActive ? "Désactiver" : "Réactiver" }}
            </button>
          </td>
        </tr>
      </tbody>
    </table>

    <div v-else-if="!specializationStore.loading" class="empty-message">
      Aucune filière trouvée.
    </div>

    <div v-if="specializationStore.loading" class="loading-state">
      Chargement...
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
.specialization-list-page {
  max-width: 900px;
  margin: 0 auto;
  padding: 20px;
}

.actions-bar {
  display: flex;
  justify-content: flex-end;
  margin-bottom: 20px;
}

.add-btn {
  padding: 10px 20px;
  background-color: #27ae60;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 1rem;
}

.add-btn:hover {
  background-color: #219a52;
}

.form-container {
  background: #f8f9fa;
  border: 1px solid #e0e0e0;
  border-radius: 8px;
  padding: 20px;
  margin-bottom: 24px;
}

.form-group {
  margin-bottom: 12px;
}

.form-group label {
  display: block;
  font-weight: 600;
  margin-bottom: 4px;
}

.form-group input {
  width: 100%;
  padding: 8px;
  border: 1px solid #ccc;
  border-radius: 4px;
  font-size: 1rem;
}

.form-actions {
  display: flex;
  gap: 10px;
  margin-top: 12px;
}

.save-btn {
  padding: 8px 20px;
  background-color: #3498db;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
}

.save-btn:disabled {
  background-color: #b2bec3;
  cursor: not-allowed;
}

.save-btn:not(:disabled):hover {
  background-color: #217dbb;
}

.cancel-btn {
  padding: 8px 20px;
  background-color: #b2bec3;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
}

.cancel-btn:hover {
  background-color: #636e72;
}

table {
  width: 100%;
  border-collapse: collapse;
}

th,
td {
  padding: 12px;
  text-align: left;
  border-bottom: 1px solid #ddd;
}

th {
  background-color: #f5f5f5;
  font-weight: bold;
}

tr:hover {
  background-color: #f9f9f9;
}

.badge-active {
  background-color: #27ae60;
  color: white;
  padding: 4px 10px;
  border-radius: 12px;
  font-size: 0.85rem;
}

.badge-inactive {
  background-color: #e74c3c;
  color: white;
  padding: 4px 10px;
  border-radius: 12px;
  font-size: 0.85rem;
}

.edit-btn,
.delete-btn {
  padding: 8px 16px;
  border: none;
  border-radius: 4px;
  font-size: 0.95rem;
  font-weight: 500;
  cursor: pointer;
  margin-right: 8px;
  transition: background-color 0.2s;
}

.edit-btn {
  background-color: #3498db;
  color: white;
}

.edit-btn:hover {
  background-color: #217dbb;
}

.delete-btn {
  background-color: #e74c3c;
  color: white;
}

.delete-btn:hover {
  background-color: #c0392b;
}

.empty-message {
  text-align: center;
  color: #888;
  margin-top: 20px;
}

.loading-state {
  text-align: center;
  color: #888;
  margin-top: 20px;
}

.error-message {
  color: #e74c3c;
  margin-top: 10px;
}

@media (max-width: 600px) {
  table,
  thead,
  tbody,
  th,
  td,
  tr {
    display: block;
    width: 100%;
  }
  th,
  td {
    padding: 8px 4px;
    font-size: 0.98em;
  }
  .edit-btn,
  .delete-btn {
    width: 100%;
    margin: 4px 0 0 0;
  }
}
</style>
