<template>
  <div class="students-list-page">
    <!-- Page Header -->
    <div class="page-header">
      <div class="page-title">
        <h1>{{ yearFilter !== "ADMIN" ? "Étudiants" : "Administrateurs" }}</h1>
        <p class="page-subtitle">
          {{ students.length }}
          {{ yearFilter !== "ADMIN" ? "étudiants" : "administrateurs" }} trouvés
        </p>
      </div>
      <div class="page-actions">
        <button
          v-if="yearFilter !== 'ADMIN'"
          class="btn btn-outline"
          @click="openImportPopup"
        >
          Importer
        </button>
        <AddStudentButton @click="openAddPopup" :year="yearFilter" />
      </div>
    </div>

    <!-- Filters -->
    <div class="filters-card">
      <div class="filters-row">
        <div class="filter-item">
          <label>Filière</label>
          <select v-model="selectedSpecializationId" @change="refreshStudents">
            <option value="">Toutes</option>
            <option
              v-for="spec in specializations"
              :key="spec.id"
              :value="spec.id"
            >
              {{ spec.name }} ({{ spec.code }})
            </option>
          </select>
        </div>
        <div class="year-tabs">
          <button
            :class="['tab', { active: yearFilter === 'ADMIN' }]"
            @click="filterYear('ADMIN')"
          >
            Admin
          </button>
          <button
            :class="['tab', { active: yearFilter === '3A' }]"
            @click="filterYear('3A')"
          >
            3A
          </button>
          <button
            :class="['tab', { active: yearFilter === '4A' }]"
            @click="filterYear('4A')"
          >
            4A
          </button>
          <button
            :class="['tab', { active: yearFilter === '5A' }]"
            @click="filterYear('5A')"
          >
            5A
          </button>
        </div>
      </div>
    </div>

    <!-- Table -->
    <div class="table-card">
      <table v-if="students.length > 0">
        <thead>
          <tr>
            <th>Nom</th>
            <th>Prénom</th>
            <th class="hide-mobile">N° étudiant</th>
            <th class="hide-mobile">Email</th>
            <th>Année</th>
            <th v-if="yearFilter !== 'ADMIN'">Délégué</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="student in students" :key="student.id">
            <td class="cell-name">{{ student.name }}</td>
            <td>{{ student.firstname }}</td>
            <td class="hide-mobile">
              <code>{{ student.studentNumber }}</code>
            </td>
            <td class="hide-mobile cell-email">{{ student.email }}</td>
            <td>
              <span class="year-badge">{{ student.year }}</span>
            </td>
            <td v-if="yearFilter !== 'ADMIN'">
              <span v-if="student.isDelegate" class="delegate-badge">Oui</span>
              <span v-else class="no-delegate">Non</span>
            </td>
            <td>
              <div class="cell-actions">
                <button
                  class="btn-icon btn-edit"
                  @click="openEditPopup(student)"
                  title="Modifier"
                >
                  ✏️
                </button>
                <button
                  class="btn-icon btn-delete"
                  @click="confirmDeleteStudent(student)"
                  :disabled="isCurrentUser(student)"
                  title="Supprimer"
                >
                  🗑️
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
      <div v-else class="empty-state">
        <div class="empty-text">
          Aucun
          {{ yearFilter !== "ADMIN" ? "étudiant" : "administrateur" }} trouvé.
        </div>
      </div>
    </div>
  </div>
  <PopUpImportStudent
    v-if="showImportPopup"
    :year="yearFilter"
    :students="students"
    @close="closeImportPopup"
  />
  <PopUpAddStudent
    v-if="showAddPopup"
    :year="yearFilter"
    @close="closeAddPopup"
    @student-added="refreshStudents"
  />
  <PopUpEditStudent
    v-if="showEditPopup"
    :student="selectedStudent"
    @close="closeEditPopup"
    @student-updated="refreshStudents"
  />
  <PopUpDeleteStudent
    v-if="showDeleteConfirm"
    @close="cancelDelete"
    @confirm="deleteStudent"
  />
</template>

<script setup>
import { useStudentsStore } from "../../stores/studentsStore.js";
import { useAuthStore } from "../../stores/authStore.js";
import { useSpecializationStore } from "../../stores/specializationStore.js";
import { onMounted, ref, computed } from "vue";
import PopUpImportStudent from "../popups/PopUpImportStudent.vue";
import PopUpAddStudent from "../popups/PopUpAddStudent.vue";
import AddStudentButton from "../buttons/AddStudentButton.vue";
import PopUpEditStudent from "../popups/PopUpEditStudent.vue";
import PopUpDeleteStudent from "../popups/PopUpDeleteStudent.vue";

const yearFilter = ref("3A");
const selectedSpecializationId = ref("");
const studentsStore = useStudentsStore();
const authStore = useAuthStore();
const specializationStore = useSpecializationStore();
const specializations = computed(
  () => specializationStore.activeSpecializations,
);
const showImportPopup = ref(false);
const showAddPopup = ref(false);
const showEditPopup = ref(false);
const showDeleteConfirm = ref(false);
const selectedStudent = ref(null);

const students = ref([]);

onMounted(async () => {
  authStore.initialize();
  await specializationStore.fetchSpecializations();
  await refreshStudents();
});

const refreshStudents = async () => {
  const specId = selectedSpecializationId.value || undefined;
  students.value = await studentsStore.fetchStudents(yearFilter.value, specId);
  students.value = students.value.sort((a, b) => a.name.localeCompare(b.name));
};

const currentUser = computed(() => authStore.user);

const isCurrentUser = (student) => {
  return (
    currentUser.value && currentUser.value.studentId === student.studentNumber
  );
};

const filterYear = async (year) => {
  yearFilter.value = year;
  await refreshStudents();
};

const openImportPopup = () => {
  showImportPopup.value = true;
};

const closeImportPopup = async () => {
  showImportPopup.value = false;
  await refreshStudents();
};

const openAddPopup = () => {
  showAddPopup.value = true;
};

const closeAddPopup = () => {
  showAddPopup.value = false;
};

const openEditPopup = (student) => {
  selectedStudent.value = student;
  showEditPopup.value = true;
};

const closeEditPopup = () => {
  showEditPopup.value = false;
};

const confirmDeleteStudent = (student) => {
  selectedStudent.value = student;
  showDeleteConfirm.value = true;
};

const deleteStudent = async () => {
  await studentsStore.deleteStudent(selectedStudent.value.studentNumber);
  showDeleteConfirm.value = false;
  await refreshStudents();
};

const cancelDelete = () => {
  showDeleteConfirm.value = false;
};
</script>

<style scoped>
.students-list-page {
  max-width: 1200px;
  margin: 0 auto;
  width: 100%;
}

/* Page Header */
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

.page-actions {
  display: flex;
  gap: 10px;
  align-items: center;
}

/* Buttons */
.btn-outline {
  padding: 8px 16px;
  background: transparent;
  color: #3498db;
  border: 1px solid #3498db;
  border-radius: 8px;
  font-size: 0.88rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-outline:hover {
  background: #eef6ff;
}

/* Filters */
.filters-card {
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 12px;
  padding: 16px 22px;
  margin-bottom: 20px;
}

.filters-row {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
  align-items: flex-end;
}

.filter-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.filter-item label {
  font-size: 0.75rem;
  font-weight: 600;
  color: #6c757d;
  text-transform: uppercase;
  letter-spacing: 0.4px;
}

.filter-item select {
  padding: 8px 12px;
  border: 1px solid #d1d5db;
  border-radius: 8px;
  font-size: 0.9rem;
  color: #1a1a2e;
  background: #fff;
  outline: none;
  transition:
    border-color 0.2s,
    box-shadow 0.2s;
  min-width: 160px;
}

.filter-item select:focus {
  border-color: #3498db;
  box-shadow: 0 0 0 3px rgba(52, 152, 219, 0.1);
}

/* Year tabs */
.year-tabs {
  display: flex;
  gap: 4px;
  background: #f0f2f5;
  border-radius: 10px;
  padding: 3px;
}

.tab {
  padding: 7px 16px;
  border: none;
  border-radius: 8px;
  font-size: 0.85rem;
  font-weight: 600;
  cursor: pointer;
  background: transparent;
  color: #495057;
  transition: all 0.2s;
}

.tab:hover {
  background: rgba(0, 0, 0, 0.05);
}

.tab.active {
  background: #1a1a2e;
  color: white;
  box-shadow: 0 2px 8px rgba(26, 26, 46, 0.2);
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

.cell-name {
  font-weight: 600;
  color: #1a1a2e;
}

.cell-email {
  color: #6c757d;
  font-size: 0.88rem;
}

code {
  background: #f0f2f5;
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 0.85rem;
  color: #495057;
}

.year-badge {
  background: #dbeafe;
  color: #1e40af;
  padding: 3px 10px;
  border-radius: 6px;
  font-size: 0.8rem;
  font-weight: 700;
}

.delegate-badge {
  background: #d4edda;
  color: #155724;
  padding: 2px 8px;
  border-radius: 6px;
  font-size: 0.82rem;
  font-weight: 700;
}

.no-delegate {
  color: #adb5bd;
}

/* Cell actions */
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

.btn-delete:hover {
  background: #ffebee;
  border-color: #ef5350;
}

.btn-icon:disabled {
  opacity: 0.3;
  cursor: not-allowed;
  transform: none;
}

/* Empty */
.empty-state {
  padding: 48px 24px;
  text-align: center;
  color: #6c757d;
}

.empty-icon {
  font-size: 2.5rem;
  margin-bottom: 12px;
}

/* Responsive */
@media (max-width: 768px) {
  .hide-mobile {
    display: none;
  }

  .page-header {
    flex-direction: column;
  }

  .filters-row {
    flex-direction: column;
  }

  .year-tabs {
    width: 100%;
    justify-content: center;
  }
}

@media (max-width: 480px) {
  .page-actions {
    flex-direction: column;
    width: 100%;
  }

  .tab {
    padding: 7px 10px;
    font-size: 0.82rem;
  }
}
</style>
