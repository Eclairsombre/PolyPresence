<template>
  <div class="attendance-sheet">
    <div
      v-if="authStore.user && authStore.user.existsInDb === false"
      class="state-card state-error"
    >
      <div class="state-icon">
        <svg
          width="24"
          height="24"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
        >
          <circle cx="12" cy="12" r="10" />
          <line x1="15" y1="9" x2="9" y2="15" />
          <line x1="9" y1="9" x2="15" y2="15" />
        </svg>
      </div>
      <p>
        Vous n'êtes pas présent dans la base de données. Veuillez contacter un
        administrateur.
      </p>
    </div>
    <div v-else>
      <div v-if="loading" class="state-card state-loading">
        <div class="spinner"></div>
        <p>Chargement des données...</p>
      </div>
      <div v-else-if="error" class="state-card state-error">
        <p>{{ error }}</p>
      </div>
      <div v-else-if="currentSession" class="session-content">
        <!-- Session Card -->
        <div class="session-card">
          <div class="session-card-header">
            <div class="session-title-row">
              <h2>{{ currentSession.name || "Session" }}</h2>
              <span
                class="status-badge"
                :class="{
                  'badge-present':
                    attendance &&
                    attendance.status !== 1 &&
                    attendance.status !== 2,
                  'badge-absent': attendance && attendance.status === 1,
                  'badge-cancelled': attendance && attendance.status === 2,
                }"
              >
                {{
                  attendance && attendance.status === 2
                    ? "Annulé"
                    : attendance && attendance.status !== 1
                      ? "Présent"
                      : "Absent"
                }}
              </span>
            </div>
            <p class="session-date">{{ formatDate(currentSession.date) }}</p>
          </div>

          <div class="session-details-grid">
            <div class="detail-card">
              <span class="detail-icon">
                <svg
                  width="16"
                  height="16"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  stroke-width="2"
                >
                  <circle cx="12" cy="12" r="10" />
                  <polyline points="12 6 12 12 16 14" />
                </svg>
              </span>
              <div>
                <span class="detail-label">Horaires</span>
                <span class="detail-value"
                  >{{ formatTime(currentSession.startTime) }} —
                  {{ formatTime(currentSession.endTime) }}</span
                >
              </div>
            </div>
            <div class="detail-card">
              <span class="detail-icon">
                <svg
                  width="16"
                  height="16"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  stroke-width="2"
                >
                  <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
                  <circle cx="12" cy="10" r="3" />
                </svg>
              </span>
              <div>
                <span class="detail-label">Salle</span>
                <span class="detail-value">{{ currentSession.room }}</span>
              </div>
            </div>
            <div class="detail-card">
              <span class="detail-icon">
                <svg
                  width="16"
                  height="16"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  stroke-width="2"
                >
                  <path d="M22 10v6M2 10l10-5 10 5-10 5z" />
                  <path d="M6 12v5c0 1.1 2.7 2 6 2s6-.9 6-2v-5" />
                </svg>
              </span>
              <div>
                <span class="detail-label">Formation</span>
                <span class="detail-value"
                  >{{
                    currentSession.specializationName
                      ? currentSession.specializationName + " — "
                      : ""
                  }}{{ currentSession.year }}</span
                >
              </div>
            </div>
          </div>

          <!-- Delegate section -->
          <div v-if="isDelegate" class="delegate-section">
            <div class="delegate-header">
              <span class="delegate-badge">Délégué</span>
            </div>

            <!-- Validation code -->
            <div class="delegate-row">
              <span class="delegate-label">Code de validation</span>
              <div
                class="delegate-actions"
                v-if="currentSession.validationCode"
              >
                <span class="code-display" :class="{ blurred: !showCode }">{{
                  currentSession.validationCode
                }}</span>
                <button
                  class="btn-sm btn-outline-sm"
                  @click="handleShowCodeClick"
                >
                  {{ showCode ? "Cacher" : "Voir" }}
                </button>
              </div>
            </div>

            <!-- Prof 1 -->
            <div class="delegate-row">
              <span class="delegate-label">Professeur 1</span>
              <div class="delegate-actions">
                <span class="prof-email">{{
                  professor1?.email || "Non défini"
                }}</span>
                <button
                  @click="showEditProfMailPopup = true"
                  class="btn-sm btn-outline-sm"
                >
                  Modifier
                </button>
                <button
                  @click="showResendProf1MailPopup = true"
                  class="btn-sm btn-accent-sm"
                >
                  Renvoyer
                </button>
              </div>
            </div>

            <!-- Prof 2 -->
            <div
              v-if="
                professor2?.email ||
                (isDelegate && currentSession.profFirstname2)
              "
              class="delegate-row"
            >
              <span class="delegate-label">Professeur 2</span>
              <div class="delegate-actions">
                <span class="prof-email">{{
                  professor2?.email || "Non défini"
                }}</span>
                <button
                  @click="showEditProf2MailPopup = true"
                  class="btn-sm btn-outline-sm"
                >
                  Modifier
                </button>
                <button
                  v-if="professor2?.email"
                  @click="showResendProf2MailPopup = true"
                  class="btn-sm btn-accent-sm"
                >
                  Renvoyer
                </button>
              </div>
            </div>

            <Transition name="fade">
              <div
                v-if="mailSentMessage"
                class="feedback feedback-success"
                style="margin-top: 8px"
              >
                {{ mailSentMessage }}
              </div>
            </Transition>
          </div>
        </div>

        <!-- Validate Presence -->
        <div
          v-if="attendance && attendance.status === 1"
          class="validate-section"
        >
          <ValidatePresence
            @presence-validated="loadData"
            :hasSignature="hasSignature"
          />
        </div>
      </div>

      <div v-else class="state-card state-info">
        <div class="state-icon">
          <svg
            width="24"
            height="24"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
          >
            <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
            <line x1="16" y1="2" x2="16" y2="6" />
            <line x1="8" y1="2" x2="8" y2="6" />
            <line x1="3" y1="10" x2="21" y2="10" />
          </svg>
        </div>
        <p>Aucune session en cours.</p>
      </div>

      <PopUpEditProfMail
        v-if="showEditProfMailPopup"
        :value="profEmailInput"
        @close="showEditProfMailPopup = false"
        @save="onProfMailPopupSave"
      />
      <PopUpEditProfMail
        v-if="showEditProf2MailPopup"
        :value="prof2EmailInput"
        @close="showEditProf2MailPopup = false"
        @save="onProf2MailPopupSave"
      />
      <PopUpResendProfMail
        v-if="showResendProf1MailPopup"
        @close="showResendProf1MailPopup = false"
        @confirm="confirmResendProf1Mail"
      />
      <PopUpResendProfMail
        v-if="showResendProf2MailPopup"
        @close="showResendProf2MailPopup = false"
        @confirm="confirmResendProf2Mail"
      />
      <PopUpResendProfMail
        v-if="showResendProfMailPopup"
        @close="showResendProfMailPopup = false"
        @confirm="confirmResendProfMail"
      />
      <PopUpShowCode
        v-if="showCodePopup"
        @confirm="confirmShowCode"
        @close="showCodePopup = false"
      />
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, watch } from "vue";
import { useSessionStore } from "../../stores/sessionStore";
import { useAuthStore } from "../../stores/authStore";
import { useStudentsStore } from "../../stores/studentsStore";
import { useProfessorStore } from "../../stores/professorStore";

import ValidatePresence from "../buttons/ValidatePresence.vue";
import PopUpEditProfMail from "../popups/PopUpEditProfMail.vue";
import PopUpResendProfMail from "../popups/PopUpResendProfMail.vue";
import PopUpShowCode from "../popups/PopUpShowCode.vue";

const loading = ref(true);
const error = ref(null);
const currentSession = ref(null);
const studentYear = ref(null);
const attendance = ref(null);
const hasSignature = ref(false);
const profEmailEditMode = ref(false);
const profEmailInput = ref("");
const prof2EmailEditMode = ref(false);
const prof2EmailInput = ref("");
const isDelegate = ref(false);
const mailSentMessage = ref("");
const showEditProfMailPopup = ref(false);
const showEditProf2MailPopup = ref(false);
const showResendProfMailPopup = ref(false);
const showResendProf1MailPopup = ref(false);
const showResendProf2MailPopup = ref(false);
const showCode = ref(false);
const showCodePopup = ref(false);
const professor1 = ref(null);
const professor2 = ref(null);

const sessionStore = useSessionStore();
const authStore = useAuthStore();
const studentsStore = useStudentsStore();
const professorStore = useProfessorStore();

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
  const t = timeString.includes("T") ? timeString.split("T")[1] : timeString;
  return t.substring(0, 5);
};

const loadData = async () => {
  loading.value = true;
  error.value = null;

  try {
    if (!authStore.user || !authStore.user.studentId) {
      error.value = "Veuillez vous connecter pour accéder à cette page.";
      return;
    }
    const studentData = await studentsStore.getStudent(
      authStore.user.studentId,
    );
    if (studentData) {
      if (studentData.signature && studentData.signature !== " ") {
        hasSignature.value = true;
      }
      if (studentData.isDelegate) {
        isDelegate.value = true;
      }
      studentYear.value = studentData.year;
      const session = await sessionStore.getCurrentSession(studentYear.value);
      if (!session) {
        return;
      }
      currentSession.value = session;
      if (session.profId)
        professor1.value = await professorStore.fetchProfessorById(
          session.profId,
        );
      if (session.profId2)
        professor2.value = await professorStore.fetchProfessorById(
          session.profId2,
        );

      profEmailInput.value = professor1.value?.email || "";
      prof2EmailInput.value = professor2.value?.email || "";
      const at = await sessionStore.getAttendance(
        authStore.user.studentId,
        currentSession.value.id,
      );
      attendance.value = at;
      if (!currentSession.value) {
        error.value = "Aucune session en cours pour votre année.";
      }
    } else {
      error.value = "Impossible de récupérer vos données d'étudiant.";
    }
  } catch (err) {
    if (err.response && err.response.status !== 404) {
      error.value = "Une erreur est survenue lors du chargement des données.";
    }
  } finally {
    loading.value = false;
  }
};

onMounted(async () => {
  await loadData();
  if (authStore.user && authStore.user.isDelegate) {
    isDelegate.value = true;
  }
});

const saveProfEmail = async () => {
  if (!profEmailInput.value || !currentSession.value?.id) return;
  try {
    await sessionStore.setProfEmail(
      currentSession.value.id,
      profEmailInput.value,
    );
    currentSession.value.profEmail = profEmailInput.value;
  } catch (e) {
    error.value =
      e.response?.data?.message ||
      "Erreur lors de la modification de l'email du professeur.";
    console.debug("Erreur:", e);
  }
  profEmailEditMode.value = false;
};
const cancelProfEmailEdit = () => {
  profEmailEditMode.value = false;
  profEmailInput.value = currentSession.value.profEmail;
};
const resendProfMail = async () => {
  if (!currentSession.value?.id) return;
  mailSentMessage.value = "";
  try {
    await sessionStore.resendProfMail(currentSession.value.id);
    mailSentMessage.value = "Mail renvoyé au professeur.";
  } catch (e) {
    mailSentMessage.value = "Erreur lors de l'envoi du mail.";
    console.debug("Erreur:", e);
  }
};
const onProfMailPopupSave = async (newEmail) => {
  profEmailInput.value = newEmail;
  await saveProfEmail();
  showEditProfMailPopup.value = false;
};
const confirmResendProfMail = async () => {
  showResendProfMailPopup.value = false;
  await resendProfMail();
};

const saveProf2Email = async () => {
  if (!prof2EmailInput.value || !currentSession.value?.id) return;
  try {
    await sessionStore.setProf2Email(
      currentSession.value.id,
      prof2EmailInput.value,
    );
    currentSession.value.profEmail2 = prof2EmailInput.value;
  } catch (e) {
    error.value =
      e.response?.data?.message ||
      "Erreur lors de la modification de l'email du professeur 2.";
    console.debug("Erreur:", e);
  }
  prof2EmailEditMode.value = false;
};

const cancelProf2EmailEdit = () => {
  prof2EmailEditMode.value = false;
  prof2EmailInput.value = currentSession.value.profEmail2;
};

const resendProf2Mail = async () => {
  if (!currentSession.value?.id) return;
  mailSentMessage.value = "";
  try {
    await sessionStore.resendProf2Mail(currentSession.value.id);
    mailSentMessage.value = "Mail renvoyé au professeur 2.";
  } catch (e) {
    mailSentMessage.value = "Erreur lors de l'envoi du mail au professeur 2.";
    console.debug("Erreur:", e);
  }
};

const onProf2MailPopupSave = async (newEmail) => {
  prof2EmailInput.value = newEmail;
  await saveProf2Email();
  showEditProf2MailPopup.value = false;
};

const confirmResendProf2Mail = async () => {
  showResendProf2MailPopup.value = false;
  await resendProf2Mail();
};

const confirmResendProf1Mail = async () => {
  showResendProf1MailPopup.value = false;
  await resendProfMail();
};
function handleShowCodeClick() {
  if (!showCode.value) {
    showCodePopup.value = true;
  } else {
    showCode.value = false;
  }
}
function confirmShowCode() {
  showCode.value = true;
  showCodePopup.value = false;
}
watch(currentSession, (val) => {
  profEmailInput.value = val?.profEmail || "";
  prof2EmailInput.value = val?.profEmail2 || "";
});

watch(professor1, (newProf) => {
  profEmailInput.value = newProf?.email || "";
});

watch(professor2, (newProf) => {
  prof2EmailInput.value = newProf?.email || "";
});

watch(
  () => authStore.user?.studentId,
  (newStudentId, oldStudentId) => {
    if (newStudentId !== oldStudentId && newStudentId) {
      loadData();
    }
  },
);
</script>

<style scoped>
.attendance-sheet {
  width: 100%;
}

/* State cards (loading, error, info) */
.state-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  padding: 40px 24px;
  border-radius: 12px;
  text-align: center;
  border: 1px solid #e0e4ea;
  background: #fff;
}

.state-card p {
  margin: 0;
  font-size: 0.95rem;
  color: #6c757d;
}

.state-icon {
  color: #6c757d;
}

.state-loading .spinner {
  width: 28px;
  height: 28px;
  border: 3px solid #e0e4ea;
  border-top-color: #1a1a2e;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

.state-error {
  border-color: #fecaca;
  background: #fff5f5;
}

.state-error p {
  color: #c0392b;
}

.state-error .state-icon {
  color: #e74c3c;
}

.state-info {
  border-color: #dbeafe;
  background: #f0f4ff;
}

.state-info p {
  color: #3b5bdb;
}

.state-info .state-icon {
  color: #3498db;
}

/* Session card */
.session-content {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.session-card {
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 14px;
  overflow: hidden;
}

.session-card-header {
  padding: 22px 24px 16px;
  border-bottom: 1px solid #f0f0f5;
}

.session-title-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}

.session-title-row h2 {
  font-size: 1.25rem;
  font-weight: 700;
  color: #1a1a2e;
  margin: 0;
}

.session-date {
  font-size: 0.88rem;
  color: #6c757d;
  margin: 4px 0 0;
}

/* Status badge */
.status-badge {
  padding: 5px 14px;
  border-radius: 20px;
  font-size: 0.82rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.3px;
  flex-shrink: 0;
}

.badge-present {
  background: #d1fae5;
  color: #065f46;
}

.badge-absent {
  background: #fee2e2;
  color: #991b1b;
}

.badge-cancelled {
  background: #e8ecf1;
  color: #6c757d;
  text-decoration: line-through;
}

/* Details grid */
.session-details-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1px;
  background: #f0f0f5;
}

.detail-card {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  padding: 16px 24px;
  background: #fff;
}

.detail-icon {
  color: #6c757d;
  flex-shrink: 0;
  margin-top: 2px;
}

.detail-label {
  font-size: 0.75rem;
  font-weight: 600;
  color: #6c757d;
  text-transform: uppercase;
  letter-spacing: 0.4px;
  display: block;
}

.detail-value {
  font-size: 0.95rem;
  font-weight: 600;
  color: #1a1a2e;
  display: block;
  margin-top: 2px;
}

/* Delegate section */
.delegate-section {
  border-top: 1px solid #f0f0f5;
  padding: 16px 24px 20px;
}

.delegate-header {
  margin-bottom: 14px;
}

.delegate-badge {
  display: inline-block;
  background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
  color: #fff;
  padding: 4px 12px;
  border-radius: 6px;
  font-size: 0.75rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.delegate-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 10px 0;
  border-bottom: 1px solid #f5f5f8;
  flex-wrap: wrap;
}

.delegate-row:last-of-type {
  border-bottom: none;
}

.delegate-label {
  font-size: 0.85rem;
  font-weight: 600;
  color: #495057;
  flex-shrink: 0;
}

.delegate-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.prof-email {
  font-size: 0.9rem;
  color: #1a1a2e;
}

.code-display {
  font-family: "SF Mono", "Fira Code", monospace;
  font-size: 1.05rem;
  font-weight: 700;
  color: #1a1a2e;
  letter-spacing: 2px;
  transition: filter 0.2s;
}

.blurred {
  filter: blur(6px);
  user-select: none;
}

/* Small buttons */
.btn-sm {
  padding: 5px 12px;
  border-radius: 6px;
  font-size: 0.8rem;
  font-weight: 600;
  cursor: pointer;
  border: none;
  transition: all 0.15s;
  white-space: nowrap;
}

.btn-outline-sm {
  background: #f8f9fb;
  color: #495057;
  border: 1px solid #d1d5db;
}

.btn-outline-sm:hover {
  background: #e8ecf1;
  border-color: #adb5bd;
}

.btn-accent-sm {
  background: #3498db;
  color: #fff;
}

.btn-accent-sm:hover {
  background: #2980b9;
}

/* Validate section */
.validate-section {
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 14px;
  padding: 24px;
}

/* Feedback */
.feedback {
  padding: 10px 14px;
  border-radius: 8px;
  font-size: 0.9rem;
  font-weight: 500;
}

.feedback-success {
  background: #f0fdf4;
  color: #166534;
  border: 1px solid #bbf7d0;
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

@media (max-width: 768px) {
  .session-title-row {
    flex-direction: column;
    align-items: flex-start;
    gap: 8px;
  }

  .session-details-grid {
    grid-template-columns: 1fr;
  }

  .delegate-row {
    flex-direction: column;
    align-items: flex-start;
    gap: 6px;
  }
}
</style>
