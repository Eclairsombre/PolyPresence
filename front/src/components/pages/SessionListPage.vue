<template>
  <div class="sessions-page">
    <!-- Page Header -->
    <div class="page-header">
      <div class="page-title">
        <h1>Sessions</h1>
        <p class="page-subtitle">
          Gérez et consultez toutes les sessions de cours.
        </p>
      </div>
      <div class="page-actions">
        <button @click="showCreateSessionModal = true" class="btn btn-success">
          + Créer une session
        </button>
        <ExportSessionsPdf :sessions="sessions" :selectedYear="selectedYear" />
      </div>
    </div>

    <!-- Filters -->
    <div class="filters-card">
      <div class="filters-row">
        <div class="filter-item">
          <label>Filière</label>
          <select v-model="selectedSpecializationId" @change="loadSessions">
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
        <div class="filter-item">
          <label>Année</label>
          <select v-model="selectedYear">
            <option value="">Toutes</option>
            <option value="3A">3A</option>
            <option value="4A">4A</option>
            <option value="5A">5A</option>
          </select>
        </div>
        <div class="filter-item">
          <label>Du</label>
          <input type="date" v-model="filters.startDate" />
        </div>
        <div class="filter-item">
          <label>Au</label>
          <input
            type="date"
            v-model="filters.endDate"
            :min="filters.startDate"
          />
        </div>
        <div class="filter-buttons">
          <button @click="applyFilters" class="btn btn-primary btn-sm">
            Filtrer
          </button>
          <button @click="clearFilters" class="btn btn-ghost btn-sm">
            Réinitialiser
          </button>
        </div>
      </div>
    </div>

    <PopUpCreateSession
      v-if="showCreateSessionModal"
      @close="showCreateSessionModal = false"
      @sessionCreated="handleSessionCreated"
    />

    <Transition name="fade">
      <div v-if="showSuccessMessage" class="toast toast-success">
        Session créée avec succès !
      </div>
    </Transition>

    <!-- States -->
    <div v-if="sessionStore.loading" class="state-card">
      <div class="spinner"></div>
      <p>Chargement des sessions...</p>
    </div>

    <div v-else-if="sessionStore.error" class="state-card state-error">
      <p>{{ sessionStore.error }}</p>
      <button @click="loadSessions" class="btn btn-primary btn-sm">
        Réessayer
      </button>
    </div>

    <div v-else-if="sessions.length === 0" class="state-card">
      <div class="empty-text">Aucune session</div>
      <p>Aucune session trouvée.</p>
      <p class="state-hint">
        Modifiez vos filtres ou créez une nouvelle session.
      </p>
    </div>

    <!-- Sessions Grid -->
    <div v-else class="sessions-grid">
      <div v-for="session in sessions" :key="session.id" class="session-card">
        <div class="card-top">
          <div class="card-date">{{ formatDate(session.date) }}</div>
          <span class="card-badge">
            {{
              session.specializationCode
                ? session.specializationCode + " · "
                : ""
            }}{{ session.year }}
          </span>
        </div>
        <div class="card-body">
          <h3 v-if="session.name" class="card-name">{{ session.name }}</h3>
          <div class="card-meta">
            <span class="meta-item"
              >{{ formatTime(session.startTime) }} -
              {{ formatTime(session.endTime) }}</span
            >
            <span v-if="session.room" class="meta-item">{{
              session.room
            }}</span>
          </div>
          <div v-if="session.profId || session.profId2" class="card-profs">
            <span v-if="session.profId" class="prof-tag">{{
              getProfName(session.profId)
            }}</span>
            <span v-if="session.profId2" class="prof-tag">{{
              getProfName(session.profId2)
            }}</span>
          </div>
        </div>
        <div class="card-footer">
          <router-link
            :to="{
              path: `/sessions/${session.id}`,
              query: {
                year: selectedYear,
                startDate: filters.startDate,
                endDate: filters.endDate,
              },
            }"
            class="btn btn-primary btn-sm"
          >
            Voir les présences
          </router-link>
          <button
            v-if="
              session.name &&
              session.name.toLowerCase().includes('travail personnel')
            "
            class="btn btn-outline btn-sm"
            @click="openEditSessionModal(session)"
          >
            Signer
          </button>
        </div>
      </div>
    </div>

    <PopUpSignSession
      v-if="showEditSessionModal"
      :session="selectedSession"
      @close="showEditSessionModal = false"
      @sessionUpdated="handleSessionUpdated"
    />
  </div>
</template>

<script>
import { defineComponent, ref, computed, onMounted, reactive } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useSessionStore } from "../../stores/sessionStore";
import { useStudentsStore } from "../../stores/studentsStore";
import { useProfessorStore } from "../../stores/professorStore";
import { useSpecializationStore } from "../../stores/specializationStore";
import ExportSessionsPdf from "../exports/ExportSessionsPdf.vue";
import PopUpCreateSession from "../popups/PopUpCreateSession.vue";
import PopUpSignSession from "../popups/PopUpSignSession.vue";

export default defineComponent({
  name: "StudentsSessionPage",
  components: {
    ExportSessionsPdf,
    PopUpCreateSession,
    PopUpSignSession: PopUpSignSession,
  },
  setup() {
    const sessionStore = useSessionStore();
    const studentsStore = useStudentsStore();
    const professorStore = useProfessorStore();
    const specializationStore = useSpecializationStore();
    const route = useRoute();
    const router = useRouter();
    const selectedYear = ref(route.query.year || "");
    const selectedSpecializationId = ref("");
    const specializations = computed(
      () => specializationStore.activeSpecializations,
    );
    const today = new Date().toISOString().split("T")[0];
    const filters = reactive({
      startDate: route.query.startDate || today,
      endDate: route.query.endDate || "",
    });
    const showCreateSessionForm = ref(false);
    const showSuccessMessage = ref(false);
    const studentLoading = ref(false);
    const students = ref([]);
    const isFiltering = ref(false);
    const showCreateSessionModal = ref(false);
    const showEditSessionModal = ref(false);
    const selectedSession = ref(null);

    const newSession = reactive({
      date: "",
      startTime: "",
      endTime: "",
      year: "",
      profName: "",
      profFirstname: "",
      profEmail: "",
    });

    const sessions = computed(() => {
      let filtered = sessionStore.sessions.slice();
      if (selectedSpecializationId.value) {
        filtered = filtered.filter(
          (s) => s.specializationId == selectedSpecializationId.value,
        );
      }
      return filtered.sort((a, b) => {
        const dateComparison = new Date(a.date) - new Date(b.date);
        if (dateComparison !== 0) {
          return dateComparison;
        }
        return a.startTime.localeCompare(b.startTime);
      });
    });

    const loadSessions = async () => {
      isFiltering.value = true;
      router.replace({
        query: {
          year: selectedYear.value || undefined,
          startDate: filters.startDate || undefined,
          endDate: filters.endDate || undefined,
        },
      });
      await sessionStore.fetchSessionsByFilters({
        year: selectedYear.value,
        startDate: filters.startDate,
        endDate: filters.endDate,
      });
      isFiltering.value = false;
    };

    const applyFilters = async () => {
      await loadSessions();
    };
    const clearFilters = () => {
      filters.startDate = new Date().toISOString().split("T")[0];
      filters.endDate = "";
      selectedYear.value = "";
      loadSessions();
    };

    const loadStudentsByYear = async () => {
      if (!newSession.year) {
        students.value = [];
        return;
      }

      studentLoading.value = true;
      studentsStore
        .fetchStudents(newSession.year)
        .then((response) => {
          students.value = response;
        })
        .catch((error) => {
          console.error("Erreur lors du chargement des étudiants:", error);
        })
        .finally(() => {
          studentLoading.value = false;
        });
    };

    const createNewSession = async () => {
      if (
        !newSession.date ||
        !newSession.startTime ||
        !newSession.endTime ||
        !newSession.year ||
        !newSession.profName ||
        !newSession.profFirstname ||
        !newSession.profEmail
      ) {
        return;
      }

      let validationCode = "";
      for (let i = 0; i < 4; i++) {
        validationCode += Math.floor(Math.random() * 10).toString();
      }
      const sessionData = {
        date: newSession.date,
        startTime: newSession.startTime,
        endTime: newSession.endTime,
        year: newSession.year,
        validationCode: validationCode,
        profName: newSession.profName,
        profFirstname: newSession.profFirstname,
        profEmail: newSession.profEmail,
      };

      try {
        const createdSession = await sessionStore.createSession(sessionData);

        if (createdSession && students.value.length > 0) {
          try {
            await sessionStore.addStudentsToSessionByNumber(
              createdSession.id,
              students.value,
            );
          } catch (error) {
            console.error(
              "Erreur lors de l'ajout des étudiants à la session:",
              error,
            );
          }
        }

        newSession.date = "";
        newSession.startTime = "";
        newSession.endTime = "";
        newSession.year = "";
        newSession.profName = "";
        newSession.profFirstname = "";
        newSession.profEmail = "";
        students.value = [];
        showCreateSessionForm.value = false;

        showSuccessMessage.value = true;
        setTimeout(() => {
          showSuccessMessage.value = false;
        }, 3000);

        loadSessions();
      } catch (error) {
        console.error("Erreur lors de la création de la session:", error);
      }
    };

    const formatDate = (dateString) => {
      if (!dateString) return "";
      const date = new Date(dateString);
      const day = String(date.getDate()).padStart(2, "0");
      const month = String(date.getMonth() + 1).padStart(2, "0");
      const year = date.getFullYear();
      return `${day}/${month}/${year}`;
    };

    const formatTime = (timeString) => {
      if (!timeString) return "";
      const t = timeString.includes("T")
        ? timeString.split("T")[1]
        : timeString;
      return t.substring(0, 5);
    };

    const handleSessionCreated = () => {
      showCreateSessionModal.value = false;
      loadSessions();
    };

    const openEditSessionModal = (session) => {
      selectedSession.value = { ...session };
      showEditSessionModal.value = true;
    };

    const handleSessionUpdated = () => {
      showEditSessionModal.value = false;
      loadSessions();
    };

    const getProfName = (id) => {
      if (!id) return "";
      const prof = professorStore.professors.find((p) => p.id == id);
      if (!prof) return "";
      return `${prof.firstname} ${prof.name}`;
    };

    onMounted(() => {
      professorStore.fetchProfessors();
      specializationStore.fetchSpecializations();
      loadSessions();
    });

    return {
      sessionStore,
      sessions,
      selectedYear,
      selectedSpecializationId,
      specializations,
      loadSessions,
      formatDate,
      formatTime,
      showCreateSessionForm,
      newSession,
      createNewSession,
      loadStudentsByYear,
      students,
      studentLoading,
      showSuccessMessage,
      filters,
      applyFilters,
      clearFilters,
      isFiltering,
      showCreateSessionModal,
      handleSessionCreated,
      showEditSessionModal,
      selectedSession,
      openEditSessionModal,
      handleSessionUpdated,
      getProfName,
    };
  },
});
</script>

<style scoped>
.sessions-page {
  width: 100%;
  max-width: 1200px;
  margin: 0 auto;
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
.btn {
  padding: 9px 18px;
  border: none;
  border-radius: 8px;
  font-size: 0.9rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
  text-decoration: none;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  white-space: nowrap;
}

.btn-sm {
  padding: 7px 14px;
  font-size: 0.85rem;
}

.btn-primary {
  background: #3498db;
  color: #fff;
}

.btn-primary:hover {
  background: #2980b9;
  box-shadow: 0 4px 12px rgba(52, 152, 219, 0.25);
}

.btn-success {
  background: #27ae60;
  color: #fff;
}

.btn-success:hover {
  background: #219a52;
  box-shadow: 0 4px 12px rgba(39, 174, 96, 0.25);
}

.btn-outline {
  background: transparent;
  color: #3498db;
  border: 1px solid #3498db;
}

.btn-outline:hover {
  background: #eef6ff;
}

.btn-ghost {
  background: #f0f2f5;
  color: #495057;
}

.btn-ghost:hover {
  background: #e2e6ea;
}

.btn-icon-text {
  font-size: 1.1rem;
  line-height: 1;
}

/* Filters */
.filters-card {
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 12px;
  padding: 18px 22px;
  margin-bottom: 24px;
}

.filters-row {
  display: flex;
  flex-wrap: wrap;
  gap: 14px;
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

.filter-item select,
.filter-item input {
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
  min-width: 130px;
}

.filter-item select:focus,
.filter-item input:focus {
  border-color: #3498db;
  box-shadow: 0 0 0 3px rgba(52, 152, 219, 0.1);
}

.filter-buttons {
  display: flex;
  gap: 8px;
  align-items: flex-end;
}

/* Toast */
.toast {
  position: fixed;
  top: 80px;
  right: 24px;
  padding: 14px 22px;
  border-radius: 10px;
  font-weight: 600;
  font-size: 0.9rem;
  z-index: 500;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.12);
}

.toast-success {
  background: #d4edda;
  color: #155724;
  border: 1px solid #c3e6cb;
}

/* States */
.state-card {
  text-align: center;
  padding: 48px 24px;
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 12px;
  color: #6c757d;
}

.state-error {
  border-color: #f5c6cb;
  color: #721c24;
}

.state-hint {
  font-size: 0.85rem;
  color: #adb5bd;
  margin-top: 4px;
}

.empty-icon {
  font-size: 2.5rem;
  margin-bottom: 12px;
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

/* Sessions Grid */
.sessions-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(340px, 1fr));
  gap: 18px;
}

.session-card {
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 12px;
  overflow: hidden;
  transition:
    transform 0.2s,
    box-shadow 0.2s;
  display: flex;
  flex-direction: column;
}

.session-card:hover {
  transform: translateY(-3px);
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.08);
}

.card-top {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 14px 18px;
  background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
  color: white;
}

.card-date {
  font-weight: 700;
  font-size: 1rem;
}

.card-badge {
  background: rgba(255, 255, 255, 0.15);
  padding: 3px 10px;
  border-radius: 6px;
  font-size: 0.82rem;
  font-weight: 600;
  backdrop-filter: blur(4px);
}

.card-body {
  padding: 16px 18px;
  flex: 1;
}

.card-name {
  font-size: 1rem;
  font-weight: 600;
  color: #1a1a2e;
  margin: 0 0 10px;
}

.card-meta {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.meta-item {
  font-size: 0.88rem;
  color: #495057;
}

.card-profs {
  margin-top: 10px;
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.prof-tag {
  font-size: 0.82rem;
  background: #eef2ff;
  color: #3949ab;
  padding: 3px 10px;
  border-radius: 6px;
}

.card-footer {
  padding: 12px 18px;
  border-top: 1px solid #f0f0f5;
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
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
  transform: translateY(-8px);
}

/* Responsive */
@media (max-width: 768px) {
  .sessions-grid {
    grid-template-columns: 1fr;
  }

  .page-header {
    flex-direction: column;
  }

  .filters-row {
    flex-direction: column;
  }

  .filter-item select,
  .filter-item input {
    width: 100%;
  }
}

@media (max-width: 480px) {
  .page-actions {
    flex-direction: column;
    width: 100%;
  }

  .page-actions .btn {
    width: 100%;
    justify-content: center;
  }
}
</style>
