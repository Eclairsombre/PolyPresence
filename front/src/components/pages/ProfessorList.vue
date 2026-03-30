<template>
  <div class="prof-page">
    <div class="page-header">
      <div class="page-title">
        <h1>Professeurs</h1>
        <p class="page-subtitle">
          {{ professors.length }} professeur{{
            professors.length > 1 ? "s" : ""
          }}
          enregistré{{ professors.length > 1 ? "s" : "" }}
        </p>
      </div>
    </div>

    <div v-if="professors.length" class="table-card">
      <table>
        <thead>
          <tr>
            <th>Nom</th>
            <th>Email</th>
            <th class="col-actions">Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="prof in professors"
            :key="prof.id"
            :class="{ 'editing-row': prof.editing }"
          >
            <td class="cell-name">{{ prof.firstname }} {{ prof.name }}</td>
            <td class="cell-email">
              <span v-if="!prof.editing">{{ prof.email }}</span>
              <input
                v-else
                v-model="prof.newEmail"
                type="email"
                class="inline-input"
                placeholder="Email"
                @keyup.enter="saveEmail(prof)"
              />
            </td>
            <td class="col-actions">
              <template v-if="!prof.editing">
                <button
                  class="btn-icon btn-edit"
                  @click="enableEdit(prof)"
                  title="Modifier l'email"
                >
                  ✏️
                </button>
              </template>
              <template v-else>
                <button class="btn btn-sm btn-primary" @click="saveEmail(prof)">
                  Enregistrer
                </button>
                <button class="btn btn-sm btn-ghost" @click="cancelEdit(prof)">
                  Annuler
                </button>
              </template>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div v-else class="empty-state">
      <p>Aucun professeur trouvé.</p>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from "vue";
import { useProfessorStore } from "../../stores/professorStore";

const professorStore = useProfessorStore();
const professors = ref([]);

onMounted(async () => {
  await professorStore.fetchProfessors();
  professors.value = professorStore.professors;
  professorStore.professors.forEach((p) => {
    p.editing = false;
    p.newEmail = p.email;
  });
});

function enableEdit(prof) {
  prof.editing = true;
  prof.newEmail = prof.email;
}

function cancelEdit(prof) {
  prof.editing = false;
  prof.newEmail = prof.email;
}

async function saveEmail(prof) {
  if (!prof.newEmail || prof.newEmail === prof.email) {
    prof.editing = false;
    return;
  }
  const ok = await professorStore.updateProfessorEmail(prof.id, prof.newEmail);
  if (ok) {
    prof.email = prof.newEmail;
    prof.editing = false;
  } else {
    alert("Erreur lors de la mise à jour de l'email");
  }
}
</script>

<style scoped>
.prof-page {
  max-width: 800px;
  margin: 0 auto;
  width: 100%;
}

.page-header {
  margin-bottom: 24px;
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

/* Table Card */
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

thead th {
  padding: 12px 18px;
  text-align: left;
  font-size: 0.75rem;
  font-weight: 700;
  color: #6c757d;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  border-bottom: 1px solid #e0e4ea;
}

tbody tr {
  transition: background 0.15s;
}

tbody tr:hover {
  background: #f8f9fb;
}

tbody tr.editing-row {
  background: #f0f8ff;
}

tbody td {
  padding: 14px 18px;
  font-size: 0.93rem;
  color: #2d3748;
  border-bottom: 1px solid #f0f0f5;
}

tbody tr:last-child td {
  border-bottom: none;
}

.cell-name {
  font-weight: 600;
  color: #1a1a2e;
}

/* Inline Input */
.inline-input {
  padding: 7px 10px;
  border: 1px solid #3498db;
  border-radius: 8px;
  font-size: 0.93rem;
  color: #1a1a2e;
  outline: none;
  width: 100%;
  max-width: 280px;
  box-shadow: 0 0 0 3px rgba(52, 152, 219, 0.1);
}

/* Actions */
.col-actions {
  text-align: right;
  white-space: nowrap;
  width: 160px;
}

.btn-icon {
  width: 34px;
  height: 34px;
  border: none;
  border-radius: 8px;
  background: transparent;
  cursor: pointer;
  font-size: 1rem;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s;
}

.btn-edit:hover {
  background: #eef6ff;
}

.btn {
  padding: 6px 14px;
  border: none;
  border-radius: 8px;
  font-size: 0.82rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-sm {
  padding: 6px 14px;
}

.btn-primary {
  background: #3498db;
  color: #fff;
}

.btn-primary:hover {
  background: #2980b9;
}

.btn-ghost {
  background: transparent;
  color: #6c757d;
}

.btn-ghost:hover {
  background: #f0f2f5;
  color: #1a1a2e;
}

/* Empty */
.empty-state {
  text-align: center;
  padding: 48px 24px;
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 12px;
  color: #6c757d;
}

.empty-icon {
  font-size: 2.2rem;
  display: block;
  margin-bottom: 8px;
}

@media (max-width: 600px) {
  .col-actions {
    width: auto;
  }

  thead th,
  tbody td {
    padding: 10px 10px;
  }

  .inline-input {
    max-width: 160px;
  }
}
button:hover {
  background: #125ea2;
}
</style>
