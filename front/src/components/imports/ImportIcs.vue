<template>
  <div class="import-page">
    <!-- Header -->
    <div class="page-header">
      <div class="header-text">
        <h1>Gestion de l'emploi du temps</h1>
        <p class="subtitle">
          Importez et gérez les liens ICS pour chaque filière et année.
        </p>
      </div>
      <div class="header-status">
        <div class="auto-import-card" :class="{ enabled: autoImportEnabled }">
          <div class="auto-import-info">
            <span class="auto-import-label">Import automatique</span>
            <span class="auto-import-state">{{
              autoImportEnabled ? "Actif" : "Inactif"
            }}</span>
          </div>
          <div
            class="toggle-switch"
            @click="toggleAutoImport"
            role="switch"
            :aria-checked="autoImportEnabled"
            tabindex="0"
            @keydown.enter="toggleAutoImport"
            @keydown.space.prevent="toggleAutoImport"
          >
            <span class="toggle-track" :class="{ active: autoImportEnabled }">
              <span class="toggle-thumb"></span>
            </span>
          </div>
        </div>
        <p v-if="nextImportTimer && autoImportEnabled" class="next-import">
          Prochain import :
          <strong>{{
            new Date(nextImportTimer).toLocaleString("fr-FR")
          }}</strong>
        </p>
      </div>
    </div>

    <!-- Content Grid -->
    <div class="content-grid">
      <!-- Left: Links table -->
      <section class="section links-section">
        <div class="section-header">
          <h2>Liens ICS enregistrés</h2>
          <span class="badge">{{ icsLinkStore.icsLinks.length }}</span>
        </div>

        <div v-if="icsLinkStore.icsLinks.length === 0" class="empty-state">
          <div class="empty-icon">📅</div>
          <p>Aucun lien ICS enregistré.</p>
          <p class="empty-hint">
            Utilisez le formulaire pour ajouter un premier lien.
          </p>
        </div>

        <table v-else class="links-table">
          <thead>
            <tr>
              <th>Filière</th>
              <th>Année</th>
              <th>URL</th>
              <th style="width: 140px">Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="link in icsLinkStore.icsLinks"
              :key="link.id"
              :class="{ 'editing-row': editingId === link.id }"
            >
              <td>
                <span class="spec-badge">{{
                  link.specializationCode || "—"
                }}</span>
              </td>
              <td>
                <span class="year-badge">{{ link.year }}</span>
              </td>
              <td class="url-cell">
                <a
                  :href="link.url"
                  target="_blank"
                  rel="noopener noreferrer"
                  :title="link.url"
                >
                  {{ truncateUrl(link.url) }}
                </a>
              </td>
              <td>
                <div class="action-btns">
                  <button
                    @click="editLink(link)"
                    class="btn-icon btn-edit"
                    title="Modifier"
                  >
                    ✏️
                  </button>
                  <button
                    @click="deleteLink(link.id)"
                    class="btn-icon btn-delete"
                    title="Supprimer"
                  >
                    🗑️
                  </button>
                  <button
                    @click="reimportLink(link)"
                    class="btn-icon btn-reimport"
                    :disabled="loading"
                    title="Ré-importer"
                  >
                    🔄
                  </button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </section>

      <!-- Right: Form -->
      <section class="section form-section">
        <div class="section-header">
          <h2>{{ editingId ? "Modifier le lien" : "Ajouter un lien ICS" }}</h2>
        </div>

        <form @submit.prevent="saveIcsLink" class="ics-form">
          <div class="form-field">
            <label for="spec-select">Filière</label>
            <select
              v-model="selectedSpecializationId"
              id="spec-select"
              required
            >
              <option value="" disabled>Choisir une filière…</option>
              <option
                v-for="spec in specializations"
                :key="spec.id"
                :value="spec.id"
              >
                {{ spec.name }} ({{ spec.code }})
              </option>
            </select>
          </div>

          <div class="form-field">
            <label for="year-select">Année</label>
            <select v-model="year" id="year-select" required>
              <option value="" disabled>Choisir une année…</option>
              <option value="3A">3A</option>
              <option value="4A">4A</option>
              <option value="5A">5A</option>
            </select>
          </div>

          <div class="form-field">
            <label for="ics-url">Lien ICS</label>
            <div class="url-input-wrapper">
              <span class="url-icon">🔗</span>
              <input
                v-model="icsUrl"
                id="ics-url"
                type="url"
                placeholder="https://planning.univ-lyon1.fr/..."
                required
                autocomplete="off"
              />
            </div>
          </div>

          <div class="form-actions">
            <button
              type="submit"
              class="btn btn-primary"
              :disabled="
                loading || !icsUrl || !year || !selectedSpecializationId
              "
            >
              <span v-if="loading" class="spinner"></span>
              {{
                editingId
                  ? "Enregistrer les modifications"
                  : "Ajouter et importer"
              }}
            </button>
            <button
              v-if="editingId"
              type="button"
              @click="cancelEdit"
              class="btn btn-secondary"
            >
              Annuler
            </button>
          </div>
        </form>

        <!-- Feedback -->
        <Transition name="fade">
          <div
            v-if="message"
            class="feedback"
            :class="success ? 'feedback-success' : 'feedback-error'"
          >
            <span class="feedback-icon">{{ success ? "✓" : "✗" }}</span>
            {{ message }}
          </div>
        </Transition>
      </section>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, computed } from "vue";
import { useIcsLinkStore } from "../../stores/icsLinkStore.js";
import { useSpecializationStore } from "../../stores/specializationStore.js";

const icsLinkStore = useIcsLinkStore();
const specializationStore = useSpecializationStore();

const icsUrl = ref("");
const year = ref("");
const selectedSpecializationId = ref("");
const specializations = computed(
  () => specializationStore.activeSpecializations,
);
const message = computed(() => icsLinkStore.message);
const success = computed(() => icsLinkStore.success);
const loading = computed(() => icsLinkStore.loading);
const editingId = ref(null);
const nextImportTimer = computed(() =>
  icsLinkStore.timers ? icsLinkStore.timers.nextImport : null,
);
const autoImportEnabled = computed(() => icsLinkStore.autoImportEnabled);

const truncateUrl = (url) => {
  if (!url) return "";
  try {
    const u = new URL(url);
    const path =
      u.pathname.length > 30 ? u.pathname.substring(0, 30) + "…" : u.pathname;
    return u.hostname + path;
  } catch {
    return url.length > 50 ? url.substring(0, 50) + "…" : url;
  }
};

const fetchAll = async () => {
  await specializationStore.fetchSpecializations();
  await icsLinkStore.fetchIcsLinks();
  await icsLinkStore.fetchTimers();
  await icsLinkStore.fetchAutoImportStatus();
};

const saveIcsLink = async () => {
  icsLinkStore.resetMessage();
  if (editingId.value) {
    await icsLinkStore.updateIcsLink(
      editingId.value,
      year.value,
      icsUrl.value,
      selectedSpecializationId.value,
    );
  } else {
    await icsLinkStore.addIcsLink(
      year.value,
      icsUrl.value,
      selectedSpecializationId.value,
    );
  }
  await icsLinkStore.importIcs(
    icsUrl.value,
    year.value,
    selectedSpecializationId.value,
  );
  icsUrl.value = "";
  year.value = "";
  selectedSpecializationId.value = "";
  editingId.value = null;
};

const editLink = (link) => {
  editingId.value = link.id;
  year.value = link.year;
  icsUrl.value = link.url;
  selectedSpecializationId.value = link.specializationId || "";
  // Scroll to form on mobile
  document
    .querySelector(".form-section")
    ?.scrollIntoView({ behavior: "smooth", block: "start" });
};

const cancelEdit = () => {
  editingId.value = null;
  year.value = "";
  icsUrl.value = "";
  selectedSpecializationId.value = "";
  icsLinkStore.resetMessage();
};

const deleteLink = async (id) => {
  if (!confirm("Êtes-vous sûr de vouloir supprimer ce lien ICS ?")) return;
  await icsLinkStore.deleteIcsLink(id);
};

const reimportLink = async (link) => {
  icsLinkStore.resetMessage();
  await icsLinkStore.importIcs(link.url, link.year, link.specializationId);
};

const toggleAutoImport = async () => {
  await icsLinkStore.setAutoImportStatus(!autoImportEnabled.value);
};

onMounted(fetchAll);
</script>

<style scoped>
/* ========== Layout ========== */
.import-page {
  width: 100%;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 24px;
  margin-bottom: 28px;
  flex-wrap: wrap;
}

.header-text h1 {
  font-size: 1.6rem;
  font-weight: 700;
  color: #1a1a2e;
  margin-bottom: 4px;
}

.subtitle {
  color: #6c757d;
  font-size: 0.95rem;
  margin: 0;
}

/* Auto import card */
.auto-import-card {
  display: flex;
  align-items: center;
  gap: 16px;
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 10px;
  padding: 12px 18px;
  transition:
    border-color 0.2s,
    box-shadow 0.2s;
}

.auto-import-card.enabled {
  border-color: #27ae60;
  box-shadow: 0 0 0 3px rgba(39, 174, 96, 0.08);
}

.auto-import-info {
  display: flex;
  flex-direction: column;
}

.auto-import-label {
  font-size: 0.82rem;
  color: #6c757d;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  font-weight: 600;
}

.auto-import-state {
  font-size: 1rem;
  font-weight: 700;
  color: #1a1a2e;
}

.next-import {
  font-size: 0.85rem;
  color: #6c757d;
  margin: 8px 0 0;
  text-align: right;
}

/* Toggle */
.toggle-switch {
  cursor: pointer;
  outline: none;
}

.toggle-switch:focus-visible .toggle-track {
  box-shadow: 0 0 0 3px rgba(52, 152, 219, 0.4);
}

.toggle-track {
  display: flex;
  align-items: center;
  width: 48px;
  height: 26px;
  background: #ccc;
  border-radius: 26px;
  padding: 3px;
  transition: background 0.25s;
}

.toggle-track.active {
  background: #27ae60;
}

.toggle-thumb {
  width: 20px;
  height: 20px;
  background: #fff;
  border-radius: 50%;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.2);
  transition: transform 0.25s;
}

.toggle-track.active .toggle-thumb {
  transform: translateX(22px);
}

/* Content Grid */
.content-grid {
  display: grid;
  grid-template-columns: 1fr 380px;
  gap: 24px;
  align-items: start;
}

/* Sections */
.section {
  background: #fff;
  border-radius: 12px;
  border: 1px solid #e0e4ea;
  overflow: hidden;
}

.section-header {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 18px 22px;
  border-bottom: 1px solid #f0f0f5;
}

.section-header h2 {
  font-size: 1.1rem;
  font-weight: 600;
  color: #1a1a2e;
  margin: 0;
}

.badge {
  background: #e8ecf1;
  color: #495057;
  font-size: 0.8rem;
  font-weight: 700;
  padding: 2px 10px;
  border-radius: 12px;
}

/* Empty state */
.empty-state {
  padding: 48px 24px;
  text-align: center;
  color: #6c757d;
}

.empty-icon {
  font-size: 2.5rem;
  margin-bottom: 12px;
}

.empty-state p {
  margin: 0;
}

.empty-hint {
  font-size: 0.85rem;
  color: #adb5bd;
  margin-top: 4px !important;
}

/* Table */
.links-table {
  width: 100%;
  border-collapse: collapse;
}

.links-table thead {
  background: #f8f9fb;
}

.links-table th {
  text-align: left;
  padding: 10px 16px;
  font-size: 0.78rem;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: #6c757d;
  font-weight: 600;
  border-bottom: 1px solid #f0f0f5;
}

.links-table td {
  padding: 14px 16px;
  border-bottom: 1px solid #f5f5f8;
  font-size: 0.92rem;
  vertical-align: middle;
}

.links-table tbody tr {
  transition: background 0.15s;
}

.links-table tbody tr:hover {
  background: #f8f9fb;
}

.links-table tbody tr.editing-row {
  background: #eef6ff;
}

.spec-badge {
  background: #e8ecf1;
  color: #2c3e50;
  padding: 3px 10px;
  border-radius: 6px;
  font-size: 0.82rem;
  font-weight: 600;
}

.year-badge {
  background: #dbeafe;
  color: #1e40af;
  padding: 3px 10px;
  border-radius: 6px;
  font-size: 0.82rem;
  font-weight: 700;
}

.url-cell a {
  color: #3498db;
  text-decoration: none;
  font-size: 0.88rem;
}

.url-cell a:hover {
  text-decoration: underline;
}

/* Action buttons */
.action-btns {
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
  font-size: 0.9rem;
  transition:
    background 0.15s,
    border-color 0.15s,
    transform 0.1s;
}

.btn-icon:hover {
  transform: translateY(-1px);
}

.btn-edit:hover {
  background: #fff8e1;
  border-color: #ffca28;
}

.btn-delete:hover {
  background: #ffebee;
  border-color: #ef5350;
}

.btn-reimport:hover {
  background: #e8f5e9;
  border-color: #66bb6a;
}

.btn-icon:disabled {
  opacity: 0.4;
  cursor: not-allowed;
  transform: none;
}

/* Form */
.ics-form {
  padding: 22px;
  display: flex;
  flex-direction: column;
  gap: 18px;
}

.form-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form-field label {
  font-size: 0.82rem;
  font-weight: 600;
  color: #495057;
  text-transform: uppercase;
  letter-spacing: 0.3px;
}

.form-field select,
.form-field input {
  width: 100%;
  padding: 10px 12px;
  border: 1px solid #d1d5db;
  border-radius: 8px;
  font-size: 0.95rem;
  color: #1a1a2e;
  background: #fff;
  transition:
    border-color 0.2s,
    box-shadow 0.2s;
  outline: none;
}

.form-field select:focus,
.form-field input:focus {
  border-color: #3498db;
  box-shadow: 0 0 0 3px rgba(52, 152, 219, 0.12);
}

.url-input-wrapper {
  position: relative;
}

.url-icon {
  position: absolute;
  left: 12px;
  top: 50%;
  transform: translateY(-50%);
  font-size: 0.95rem;
  pointer-events: none;
}

.url-input-wrapper input {
  padding-left: 36px;
}

/* Buttons */
.form-actions {
  display: flex;
  gap: 10px;
  padding-top: 4px;
}

.btn {
  padding: 11px 22px;
  border: none;
  border-radius: 8px;
  font-size: 0.95rem;
  font-weight: 600;
  cursor: pointer;
  transition:
    background 0.2s,
    transform 0.1s,
    box-shadow 0.2s;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
}

.btn:active {
  transform: scale(0.98);
}

.btn-primary {
  flex: 1;
  background: #3498db;
  color: #fff;
}

.btn-primary:hover:not(:disabled) {
  background: #2980b9;
  box-shadow: 0 4px 12px rgba(52, 152, 219, 0.3);
}

.btn-primary:disabled {
  background: #b2bec3;
  cursor: not-allowed;
}

.btn-secondary {
  background: #f0f2f5;
  color: #495057;
}

.btn-secondary:hover {
  background: #e2e6ea;
}

/* Spinner */
.spinner {
  width: 16px;
  height: 16px;
  border: 2px solid rgba(255, 255, 255, 0.3);
  border-top-color: #fff;
  border-radius: 50%;
  animation: spin 0.6s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

/* Feedback */
.feedback {
  margin: 0 22px 18px;
  padding: 12px 16px;
  border-radius: 8px;
  font-size: 0.9rem;
  font-weight: 500;
  display: flex;
  align-items: center;
  gap: 8px;
}

.feedback-success {
  background: #d4edda;
  color: #155724;
  border: 1px solid #c3e6cb;
}

.feedback-error {
  background: #f8d7da;
  color: #721c24;
  border: 1px solid #f5c6cb;
}

.feedback-icon {
  font-size: 1.1rem;
  font-weight: 800;
}

/* Transitions */
.fade-enter-active,
.fade-leave-active {
  transition:
    opacity 0.3s,
    transform 0.3s;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
  transform: translateY(-6px);
}

/* ========== Responsive ========== */
@media (max-width: 820px) {
  .content-grid {
    grid-template-columns: 1fr;
  }

  .page-header {
    flex-direction: column;
  }

  .next-import {
    text-align: left;
  }
}

@media (max-width: 500px) {
  .links-table th:nth-child(3),
  .links-table td:nth-child(3) {
    display: none;
  }

  .form-actions {
    flex-direction: column;
  }

  .btn {
    width: 100%;
  }
}
</style>
