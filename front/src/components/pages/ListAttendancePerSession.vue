<template>
  <div class="attendance-page">
    <div v-if="loading" class="state-card">
      <p>Chargement des données…</p>
    </div>

    <div v-else-if="error" class="state-card state-error">
      <p>{{ error }}</p>
      <button
        @click="loadSessionData"
        class="btn btn-primary"
        style="margin-top: 12px"
      >
        Réessayer
      </button>
    </div>

    <div v-else class="attendance-container">
      <!-- Page Header -->
      <div class="page-header">
        <div class="page-title">
          <h1>Liste de présence</h1>
          <p class="page-subtitle" v-if="session">
            {{ formatDate(session.date) }} &mdash;
            {{ formatTime(session.startTime) }} –
            {{ formatTime(session.endTime) }}
          </p>
        </div>
        <div class="page-actions">
          <button class="btn btn-outline" @click="goBack">Retour</button>
          <button
            class="btn btn-primary"
            @click="exportToPDF"
            :disabled="exporting"
          >
            {{ exporting ? "Génération…" : "Exporter PDF" }}
          </button>
        </div>
      </div>

      <!-- Info Cards Row -->
      <div class="info-row">
        <div class="info-card">
          <div class="info-card-header">Établissement</div>
          <div class="info-card-body">
            <p>UCBL1 - EPUL</p>
            <p class="info-detail">
              Ingénieur de l'EPUL - spécialité
              {{ session?.specializationName || "Informatique" }} -
              apprentissage
            </p>
          </div>
        </div>
        <div class="info-card" v-if="session && (session.name || session.room)">
          <div class="info-card-header">Session</div>
          <div class="info-card-body">
            <p v-if="session.name">
              <strong>{{ session.name }}</strong>
            </p>
            <p v-if="session.room" class="info-detail">
              Salle : {{ session.room }}
            </p>
            <p class="info-detail">Année : {{ session?.year }}</p>
          </div>
        </div>
      </div>

      <!-- Professors -->
      <div class="professors-row" v-if="session">
        <h2 class="section-label">Encadrement pédagogique</h2>
        <div class="prof-edit-row">
          <div class="prof-edit-card">
            <label class="prof-edit-label" for="session-prof-1"
              >Professeur 1</label
            >
            <div class="prof-edit-controls">
              <select
                id="session-prof-1"
                v-model="selectedProf1"
                class="prof-select"
              >
                <option value="">Aucun professeur</option>
                <option
                  v-for="prof in allProfessors"
                  :key="`p1-${prof.id}`"
                  :value="String(prof.id)"
                >
                  {{ prof.firstname }} {{ prof.name }}
                </option>
              </select>
              <button
                class="btn btn-outline"
                @click="updateSessionProfessor(1)"
                :disabled="savingProfessorSlot === 1"
              >
                {{ savingProfessorSlot === 1 ? "Enregistrement..." : "Appliquer" }}
              </button>
            </div>
          </div>
          <div class="prof-edit-card">
            <label class="prof-edit-label" for="session-prof-2"
              >Professeur 2</label
            >
            <div class="prof-edit-controls">
              <select
                id="session-prof-2"
                v-model="selectedProf2"
                class="prof-select"
              >
                <option value="">Aucun professeur</option>
                <option
                  v-for="prof in allProfessors"
                  :key="`p2-${prof.id}`"
                  :value="String(prof.id)"
                >
                  {{ prof.firstname }} {{ prof.name }}
                </option>
              </select>
              <button
                class="btn btn-outline"
                @click="updateSessionProfessor(2)"
                :disabled="savingProfessorSlot === 2"
              >
                {{ savingProfessorSlot === 2 ? "Enregistrement..." : "Appliquer" }}
              </button>
            </div>
          </div>
        </div>
        <div class="prof-cards">
          <div
            class="prof-card"
            v-if="
              (professor1 && (professor1.firstname || professor1.name)) ||
              session.profSignature
            "
          >
            <div class="prof-identity">
              <span class="prof-name">{{ professor1Label }}</span>
              <span v-if="professor1 && professor1.email" class="prof-email">{{
                professor1.email
              }}</span>
            </div>
            <div class="prof-sig">
              <template v-if="session.profSignature">
                <img
                  :src="session.profSignature"
                  alt="Signature"
                  class="sig-img"
                />
              </template>
              <span v-else class="sig-missing">Non signée</span>
            </div>
          </div>
          <div
            class="prof-card"
            v-if="
              (professor2 && (professor2.firstname || professor2.name)) ||
              session.profSignature2
            "
          >
            <div class="prof-identity">
              <span class="prof-name">{{ professor2Label }}</span>
              <span v-if="professor2 && professor2.email" class="prof-email">{{
                professor2.email
              }}</span>
            </div>
            <div class="prof-sig">
              <template v-if="session.profSignature2">
                <img
                  :src="session.profSignature2"
                  alt="Signature"
                  class="sig-img"
                />
              </template>
              <span v-else class="sig-missing">Non signée</span>
            </div>
          </div>
        </div>
      </div>

      <!-- Attendance Table -->
      <div class="table-card">
        <div class="table-scroll">
          <table>
            <thead>
              <tr>
                <th class="col-num">N°</th>
                <th>Nom</th>
                <th>Prénom</th>
                <th class="col-status">Statut</th>
                <th class="col-sig">Signature</th>
                <th class="col-comment">Commentaire</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(student, index) in students" :key="student.id">
                <td class="cell-num">{{ index + 1 }}</td>
                <td class="cell-name">{{ student.name }}</td>
                <td>{{ student.firstname }}</td>
                <td class="cell-status">
                  <span
                    :class="[
                      'status-badge',
                      student.status === 'Present'
                        ? 'badge-present'
                        : 'badge-absent',
                    ]"
                  >
                    {{ student.status === "Present" ? "P" : "A" }}
                  </span>
                </td>
                <td class="cell-sig">
                  <SignatureDisplay
                    v-if="student.status === 'Present'"
                    :signatureData="student.signature"
                    :inAttendanceList="true"
                  />
                </td>
                <td class="cell-comment">
                  <div
                    v-if="student.comment"
                    class="comment-bubble"
                    :title="student.comment"
                  >
                    {{ student.comment }}
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import { defineComponent, onMounted, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useSessionStore } from "../../stores/sessionStore";
import { useStudentsStore } from "../../stores/studentsStore";
import SignatureDisplay from "../signature/SignatureDisplay.vue";
import { useMailPreferencesStore } from "../../stores/mailPreferencesStore.js";
import { useProfessorStore } from "../../stores/professorStore";

export default defineComponent({
  name: "ListAttendancePerSession",
  components: {
    SignatureDisplay,
  },

  setup() {
    const route = useRoute();
    const router = useRouter();
    const sessionStore = useSessionStore();
    const studentsStore = useStudentsStore();
    const mailStore = useMailPreferencesStore();

    const session = ref(null);
    const students = ref([]);
    const loading = ref(true);
    const error = ref(null);
    const exporting = ref(false);
    const professorStore = useProfessorStore();
    const professor1 = ref(null);
    const professor2 = ref(null);
    const allProfessors = ref([]);
    const selectedProf1 = ref("");
    const selectedProf2 = ref("");
    const savingProfessorSlot = ref(0);
    const professor1Label = ref("Professeur 1");
    const professor2Label = ref("Professeur 2");

    const loadProfessorOptions = async () => {
      allProfessors.value = await professorStore.fetchProfessors();
    };

    const loadProfessors = async () => {
      if (session.value?.profId) {
        professor1.value = await professorStore.fetchProfessorById(
          session.value.profId,
        );
        professor1Label.value =
          professor1.value?.firstname || professor1.value?.name
            ? `${professor1.value.firstname || ""} ${professor1.value.name || ""}`.trim()
            : "Professeur supprimé";
      } else {
        professor1.value = null;
        professor1Label.value = session.value?.profSignature
          ? "Professeur supprimé"
          : "Professeur 1";
      }
      selectedProf1.value = session.value?.profId ? String(session.value.profId) : "";

      if (session.value?.profId2) {
        professor2.value = await professorStore.fetchProfessorById(
          session.value.profId2,
        );
        professor2Label.value =
          professor2.value?.firstname || professor2.value?.name
            ? `${professor2.value.firstname || ""} ${professor2.value.name || ""}`.trim()
            : "Professeur supprimé";
      } else {
        professor2.value = null;
        professor2Label.value = session.value?.profSignature2
          ? "Professeur supprimé"
          : "Professeur 2";
      }
      selectedProf2.value = session.value?.profId2 ? String(session.value.profId2) : "";
    };

    const loadSessionData = async () => {
      loading.value = true;
      error.value = null;
      try {
        const sessionId = route.params.id;
        session.value = await sessionStore.fetchSessionById(sessionId);
        const attendances = await sessionStore.getSessionAttendances(sessionId);
        students.value = attendances
          .map((student) => ({
            id: student.item1.id,
            name: student.item1.name,
            firstname: student.item1.firstname,
            status: student.item2 === 0 ? "Present" : "Absent",
            signature: student.item1.signature || "",
            comment: student.item1.comment || "",
          }))
          .sort((a, b) => a.name.localeCompare(b.name));
      } catch (err) {
        console.error("Erreur lors du chargement des données:", err);
        error.value = "Impossible de charger les données de présence.";
      } finally {
        loading.value = false;
      }
    };

    const handleSignatureSaved = async ({
      studentId,
      sessionId,
      signatureData,
    }) => {
      try {
        const studentData = await studentsStore.getStudentById(studentId);
        if (!studentData) {
          console.error("Impossible de trouver l'étudiant");
          return;
        }

        await sessionStore.saveSignature(
          studentData.studentNumber,
          sessionId,
          signatureData,
        );

        const studentIndex = students.value.findIndex(
          (s) => s.id === studentId,
        );
        if (studentIndex !== -1) {
          students.value[studentIndex].signature = signatureData;
        }
      } catch (err) {
        console.error("Erreur lors de la sauvegarde de la signature:", err);
      }
    };
    const goBack = () => {
      const { year, startDate, endDate } = route.query;
      router.push({
        path: "/sessions",
        query: {
          year,
          startDate,
          endDate,
        },
      });
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

    const exportToPDF = async () => {
      if (!session.value) return;
      exporting.value = true;

      try {
        await mailStore.getSessionPdf(session);
      } finally {
        exporting.value = false;
      }
    };

    const updateSessionProfessor = async (slot) => {
      if (!session.value?.id) return;

      const selectedValue = slot === 1 ? selectedProf1.value : selectedProf2.value;
      const professorId = selectedValue ? Number(selectedValue) : null;

      savingProfessorSlot.value = slot;
      const ok = await sessionStore.setSessionProfessor(
        slot,
        session.value.id,
        professorId,
      );
      savingProfessorSlot.value = 0;

      if (!ok) {
        alert(sessionStore.error || "Impossible de modifier le professeur de la session.");
        return;
      }

      await loadSessionData();
      await loadProfessors();
    };

    onMounted(async () => {
      await loadSessionData();
      await loadProfessorOptions();
      await loadProfessors();
    });
    // Recharge les professeurs si la session change (ex: navigation)
    watch(session, async (newVal) => {
      await loadProfessors();
    });

    return {
      session,
      students,
      loading,
      error,
      exporting,
      loadSessionData,
      goBack,
      formatDate,
      formatTime,
      handleSignatureSaved,
      exportToPDF,
      updateSessionProfessor,
      route,
      professor1,
      professor2,
      allProfessors,
      selectedProf1,
      selectedProf2,
      savingProfessorSlot,
      professor1Label,
      professor2Label,
    };
  },
});
</script>

<style scoped>
.attendance-page {
  width: 100%;
  max-width: 1100px;
  margin: 0 auto;
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

.state-icon {
  font-size: 2rem;
  display: block;
  margin-bottom: 8px;
}

.state-error {
  border-color: #e74c3c;
  color: #e74c3c;
}

/* Page Header */
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  flex-wrap: wrap;
  gap: 16px;
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
  font-size: 0.95rem;
  margin: 0;
}

.page-actions {
  display: flex;
  gap: 10px;
  flex-shrink: 0;
}

/* Buttons */
.btn {
  padding: 10px 20px;
  border: none;
  border-radius: 8px;
  font-size: 0.9rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
  white-space: nowrap;
}

.btn-primary {
  background: #1f78c8;
  color: #fff;
}

.btn-primary:hover:not(:disabled) {
  background: #1766aa;
  box-shadow: 0 4px 12px rgba(52, 152, 219, 0.25);
}

.btn-primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-outline {
  background: transparent;
  color: #3498db;
  border: 1px solid #3498db;
}

.btn-outline:hover {
  background: #eef6ff;
}

/* Info Cards Row */
.info-row {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: 16px;
  margin-bottom: 24px;
}

.info-card {
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 12px;
  overflow: hidden;
}

.info-card-header {
  padding: 12px 18px;
  font-size: 0.75rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: #415367;
  background: #f8f9fb;
  border-bottom: 1px solid #f0f0f5;
}

.info-card-body {
  padding: 16px 18px;
}

.info-card-body p {
  margin: 0 0 4px 0;
  font-weight: 600;
  color: #1a1a2e;
}

.info-detail {
  font-weight: 400 !important;
  color: #6c757d !important;
  font-size: 0.92rem;
}

/* Professors */
.professors-row {
  margin-bottom: 24px;
}

.section-label {
  font-size: 0.95rem;
  font-weight: 700;
  color: #1a1a2e;
  margin: 0 0 12px 0;
}

.prof-edit-row {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: 12px;
  margin-bottom: 12px;
}

.prof-edit-card {
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 12px;
  padding: 12px;
}

.prof-edit-label {
  display: block;
  font-size: 0.8rem;
  font-weight: 700;
  color: #415367;
  margin-bottom: 8px;
  text-transform: uppercase;
  letter-spacing: 0.4px;
}

.prof-edit-controls {
  display: flex;
  gap: 8px;
}

.prof-select {
  flex: 1;
  border: 1px solid #d2d9e3;
  border-radius: 8px;
  font-size: 0.9rem;
  padding: 8px 10px;
  color: #1a1a2e;
  background: #fff;
}

.prof-cards {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: 12px;
}

.prof-card {
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 12px;
  padding: 16px 18px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
}

.prof-identity {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.prof-name {
  font-weight: 600;
  color: #1a1a2e;
  font-size: 0.95rem;
}

.prof-email {
  color: #6c757d;
  font-size: 0.85rem;
}

.prof-sig {
  flex-shrink: 0;
}

.sig-img {
  max-height: 48px;
  border: 1px solid #e0e4ea;
  border-radius: 6px;
  background: #fff;
  padding: 2px;
}

.sig-missing {
  color: #adb5bd;
  font-size: 0.85rem;
  font-style: italic;
}

/* Table Card */
.table-card {
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 12px;
  overflow: hidden;
}

.table-scroll {
  overflow-x: auto;
}

table {
  width: 100%;
  border-collapse: collapse;
}

thead {
  background: #f8f9fb;
}

thead th {
  padding: 12px 16px;
  text-align: left;
  font-size: 0.75rem;
  font-weight: 700;
  color: #415367;
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

tbody td {
  padding: 12px 16px;
  font-size: 0.93rem;
  color: #2d3748;
  border-bottom: 1px solid #f0f0f5;
  vertical-align: middle;
}

tbody tr:last-child td {
  border-bottom: none;
}

.col-num {
  width: 50px;
}
.col-status {
  width: 80px;
}
.col-sig {
  width: 18%;
}
.col-comment {
  width: 20%;
}

.cell-num {
  color: #adb5bd;
  font-weight: 600;
}

.cell-name {
  font-weight: 600;
  color: #1a1a2e;
}

.cell-status {
  text-align: center;
}

.status-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 30px;
  height: 30px;
  border-radius: 8px;
  font-weight: 700;
  font-size: 0.85rem;
}

.badge-present {
  background: #dff3e8;
  color: #1f6f43;
}

.badge-absent {
  background: #fde2e0;
  color: #b9372a;
}

.cell-sig {
  vertical-align: middle;
  padding: 8px 16px;
}

.comment-bubble {
  font-size: 0.88rem;
  color: #576574;
  background: #f8f9fb;
  border-left: 3px solid #3498db;
  padding: 8px 10px;
  border-radius: 4px;
  max-height: 80px;
  overflow-y: auto;
  word-break: break-word;
  line-height: 1.4;
}

/* Responsive */
@media (max-width: 768px) {
  .page-header {
    flex-direction: column;
  }

  .page-actions {
    width: 100%;
  }

  .page-actions .btn {
    flex: 1;
    text-align: center;
  }

  table {
    min-width: 600px;
  }

  .comment-bubble {
    font-size: 0.82rem;
    max-height: 60px;
  }
}

@media (max-width: 480px) {
  .info-row {
    grid-template-columns: 1fr;
  }

  .prof-edit-controls {
    flex-direction: column;
  }

  .prof-cards {
    grid-template-columns: 1fr;
  }

  .prof-card {
    flex-direction: column;
    align-items: flex-start;
  }
}
</style>
