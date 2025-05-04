<template>
  <div class="import-ics-container">
    <h2>Importer des sessions depuis un lien ICS</h2>
    <div class="ics-links-list">
      <h3>Liens ICS enregistrés</h3>
      <ul>
        <li v-for="link in icsLinks" :key="link.id" class="ics-link-item">
          <span><strong>{{ link.year }}</strong> : <a :href="link.url" target="_blank">{{ link.url }}</a></span>
          <button @click="editLink(link)" class="edit-btn">Modifier</button>
          <button @click="deleteLink(link.id)" class="delete-btn">Supprimer</button>
        </li>
      </ul>
      <div v-if="icsLinks.length === 0">Aucun lien enregistré.</div>
    </div>
    <hr style="margin: 24px 0; width: 100%;" />
    <div class="form-group">
      <label for="year-select">Année :</label>
      <select v-model="year" id="year-select" class="ics-year-select">
        <option value="">Sélectionner une année</option>
        <option value="3A">3A</option>
        <option value="4A">4A</option>
        <option value="5A">5A</option>
      </select>
    </div>
    <input
      v-model="icsUrl"
      type="text"
      placeholder="Collez ici le lien ICS..."
      class="ics-input"
    />
    <button @click="saveIcsLink" :disabled="loading || !icsUrl || !year" class="import-btn">
      {{ editingId ? (loading ? 'Modification...' : 'Modifier') : (loading ? 'Ajout...' : 'Ajouter') }}
    </button>
    <button v-if="editingId" @click="cancelEdit" class="cancel-btn">Annuler</button>
    <p v-if="message" :class="{'success-message': success, 'error-message': !success}">{{ message }}</p>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import axios from 'axios';

const icsLinks = ref<any[]>([]);
const icsUrl = ref('');
const year = ref('');
const message = ref('');
const success = ref(false);
const loading = ref(false);
const editingId = ref<number|null>(null);

const API_URL = import.meta.env.VITE_API_URL;

const fetchIcsLinks = async () => {
  try {
    const res = await axios.get(`${API_URL}/IcsLink`);
    console.log(res.data);
    icsLinks.value = res.data.$values;
  } catch {
    icsLinks.value = [];
  }
};

const saveIcsLink = async () => {
  message.value = '';
  success.value = false;
  loading.value = true;
  try {
    if (editingId.value) {
      await axios.put(`${API_URL}/IcsLink/${editingId.value}`, { id: editingId.value, year: year.value, url: icsUrl.value });
      message.value = 'Lien modifié !';
    } else {
      await axios.post(`${API_URL}/IcsLink`, { year: year.value, url: icsUrl.value });
      message.value = 'Lien ajouté !';
    }
    await axios.post(`${API_URL}/Session/import-ics`, { icsUrl: icsUrl.value, year: year.value });
    success.value = true;
    icsUrl.value = '';
    year.value = '';
    editingId.value = null;
    fetchIcsLinks();
  } catch (err: any) {
    message.value = err.response?.data?.message || "Erreur lors de l'enregistrement.";
    success.value = false;
  } finally {
    loading.value = false;
  }
};

const editLink = (link: any) => {
  editingId.value = link.id;
  year.value = link.year;
  icsUrl.value = link.url;
};

const cancelEdit = () => {
  editingId.value = null;
  year.value = '';
  icsUrl.value = '';
};

const deleteLink = async (id: number) => {
  if (!confirm('Supprimer ce lien ?')) return;
  loading.value = true;
  try {
    await axios.delete(`${API_URL}/IcsLink/${id}`);
    message.value = 'Lien supprimé !';
    success.value = true;
    fetchIcsLinks();
  } catch (err: any) {
    message.value = err.response?.data?.message || "Erreur lors de la suppression.";
    success.value = false;
  } finally {
    loading.value = false;
  }
};

onMounted(() => {
  fetchIcsLinks();
});
</script>

<style scoped>
/* Mise en forme du CSS pour une meilleure lisibilité */
.import-ics-container {
  max-width: 500px;
  margin: 30px auto;
  padding: 24px;
  background: #fff;
  border-radius: 10px;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.08);
  display: flex;
  flex-direction: column;
  align-items: center;
}

.form-group {
  width: 100%;
  margin-bottom: 16px;
  display: flex;
  flex-direction: column;
  align-items: flex-start;
}

.ics-year-select {
  width: 100%;
  padding: 8px;
  border-radius: 4px;
  border: 1px solid #ccc;
  font-size: 1rem;
  margin-top: 4px;
}

.ics-input {
  width: 100%;
  padding: 10px;
  border-radius: 4px;
  border: 1px solid #ccc;
  margin-bottom: 16px;
  font-size: 1rem;
}

.import-btn {
  background: #3498db;
  color: #fff;
  border: none;
  padding: 10px 24px;
  border-radius: 4px;
  font-size: 1rem;
  cursor: pointer;
  transition: background 0.2s;
}

.import-btn:disabled {
  background: #b2bec3;
  cursor: not-allowed;
}

.import-btn:not(:disabled):hover {
  background: #217dbb;
}

.success-message {
  color: #27ae60;
  margin-top: 16px;
}

.error-message {
  color: #e74c3c;
  margin-top: 16px;
}

.ics-links-list {
  width: 100%;
  margin-bottom: 24px;
  overflow-x: auto;
}

.ics-link-item {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  margin-bottom: 8px;
  flex-wrap: wrap;
  word-break: break-all;
}

.ics-link-item span {
  flex: 1 1 200px;
  min-width: 0;
  max-width: 100%;
  word-break: break-all;
  overflow-wrap: anywhere;
}

.ics-link-item a {
  display: inline-block;
  max-width: 220px;
  vertical-align: middle;
  word-break: break-all;
  overflow-wrap: anywhere;
  text-overflow: ellipsis;
  white-space: pre-line;
}

.edit-btn,
.delete-btn,
.cancel-btn {
  background: #eee;
  border: none;
  border-radius: 4px;
  padding: 4px 10px;
  cursor: pointer;
  font-size: 0.95rem;
  max-width: 110px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.edit-btn:hover {
  background: #ffe082;
}

.delete-btn {
  background: #ffb3b3;
}

.delete-btn:hover {
  background: #ff5252;
  color: #fff;
}

.cancel-btn {
  background: #b2bec3;
  margin-left: 8px;
}

.cancel-btn:hover {
  background: #636e72;
  color: #fff;
}

@media (max-width: 600px) {
  .ics-link-item a {
    max-width: 120px;
  }
  .edit-btn,
  .delete-btn,
  .cancel-btn {
    max-width: 80px;
    font-size: 0.85rem;
    padding: 4px 6px;
  }
}
</style>
