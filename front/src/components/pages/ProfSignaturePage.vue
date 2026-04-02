<template>
  <div class="prof-page">
    <PopUpProfSignatureWarning
      v-if="showSignatureWarning"
      @close="showSignatureWarning = false"
      @confirm="handleSignatureConfirm"
    />

    <!-- Toast -->
    <Transition name="fade">
      <div v-if="toastMessage" class="toast" :class="toastType">
        {{ toastMessage }}
      </div>
    </Transition>

    <!-- Loading -->
    <div v-if="loading" class="state-card">
      <div class="spinner"></div>
      <p>Chargement de la session...</p>
    </div>

    <!-- Error -->
    <div v-else-if="error && !session" class="state-card state-error">
      <div class="empty-icon">⚠️</div>
      <p>{{ error }}</p>
    </div>

    <template v-else>
      <!-- Page header -->
      <div class="page-header">
        <div class="page-title">
          <h1>Émargement — Signature professeur</h1>
          <p class="page-subtitle">
            {{
              session
                ? formatDate(session.date || session.Date) +
                  " · " +
                  formatTime(session.startTime || session.StartTime) +
                  " – " +
                  formatTime(session.endTime || session.EndTime)
                : ""
            }}
          </p>
        </div>

        <!-- Validation code chip -->
        <div v-if="validationCode" class="code-chip">
          <span class="code-chip-label">Code</span>
          <span class="code-chip-value">{{ validationCode }}</span>
        </div>
      </div>

      <!-- Session meta + professors -->
      <div v-if="session" class="info-row">
        <div class="info-card">
          <div class="info-grid">
            <div class="info-item">
              <span class="info-label">Année</span>
              <span class="info-value">{{ session.year || session.Year }}</span>
            </div>
            <div class="info-item">
              <span class="info-label">Date</span>
              <span class="info-value">{{
                formatDate(session.date || session.Date)
              }}</span>
            </div>
            <div class="info-item">
              <span class="info-label">Début</span>
              <span class="info-value">{{
                formatTime(session.startTime || session.StartTime)
              }}</span>
            </div>
            <div class="info-item">
              <span class="info-label">Fin</span>
              <span class="info-value">{{
                formatTime(session.endTime || session.EndTime)
              }}</span>
            </div>
          </div>
        </div>

        <div class="professors-row">
          <div
            v-if="professor1?.firstname"
            class="prof-card"
            :class="{ 'prof-card--you': isMainProfessor }"
          >
            <div class="prof-card-avatar">
              {{ professor1.firstname[0] }}{{ professor1.name[0] }}
            </div>
            <div class="prof-card-body">
              <div class="prof-card-role">Professeur principal</div>
              <div class="prof-card-name">
                {{ professor1.firstname }} {{ professor1.name }}
              </div>
              <div class="prof-card-email">{{ professor1.email }}</div>
            </div>
            <span v-if="isMainProfessor" class="you-badge">Vous</span>
          </div>

          <div
            v-if="professor2?.firstname"
            class="prof-card prof-card--secondary"
            :class="{ 'prof-card--you': !isMainProfessor }"
          >
            <div class="prof-card-avatar prof-card-avatar--purple">
              {{ professor2.firstname[0] }}{{ professor2.name[0] }}
            </div>
            <div class="prof-card-body">
              <div class="prof-card-role">Professeur 2</div>
              <div class="prof-card-name">
                {{ professor2.firstname }} {{ professor2.name }}
              </div>
              <div class="prof-card-email">{{ professor2.email }}</div>
            </div>
            <span v-if="!isMainProfessor" class="you-badge">Vous</span>
          </div>
        </div>
      </div>

      <!-- Main content: two columns -->
      <div class="content-grid">
        <!-- Left: Signature form -->
        <div class="panel">
          <div class="panel-header">
            <h2 class="panel-title">Signature</h2>
          </div>

          <!-- Récap après soumission -->
          <div v-if="signatureSuccess" class="sig-recap">
            <div class="sig-recap-identity">
              <div class="sig-recap-avatar">
                <template v-if="submittedFirstname && submittedName">
                  {{ submittedFirstname[0] }}{{ submittedName[0] }}
                </template>
                <template v-else>✓</template>
              </div>
              <div>
                <div class="sig-recap-name">
                  <template v-if="submittedFirstname || submittedName">
                    {{ submittedFirstname }} {{ submittedName }}
                  </template>
                  <template v-else>Signature déjà enregistrée</template>
                </div>
                <div class="sig-recap-tag">Signature enregistrée</div>
              </div>
            </div>
            <div v-if="submittedSignature" class="sig-recap-img-wrap">
              <img
                :src="submittedSignature"
                class="sig-recap-img"
                alt="Signature"
              />
            </div>
          </div>

          <form v-else @submit.prevent="openSignatureWarning" class="sig-form">
            <div class="form-row">
              <div class="form-group">
                <label class="form-label">Prénom</label>
                <input
                  v-model="profFirstname"
                  type="text"
                  class="form-input"
                  required
                  placeholder="Votre prénom"
                />
              </div>
              <div class="form-group">
                <label class="form-label">Nom</label>
                <input
                  v-model="profName"
                  type="text"
                  class="form-input"
                  required
                  placeholder="Votre nom"
                />
              </div>
            </div>

            <div class="form-group">
              <label class="form-label">Zone de signature</label>
              <div class="sig-wrapper">
                <SignatureCreator
                  v-bind:hideSaveButton="true"
                  ref="signaturePad"
                />
              </div>
              <p class="form-hint">Signez dans la zone ci-dessus</p>
            </div>

            <div v-if="signatureError" class="inline-error">
              {{ signatureError }}
            </div>

            <button type="submit" class="btn btn-primary btn-full">
              ✓ Valider la signature
            </button>
          </form>
        </div>

        <!-- Right: Attendances -->
        <div class="panel panel--wide">
          <div class="panel-header">
            <h2 class="panel-title">
              Présences
              <span class="count-badge"
                >{{ presentCount }}/{{ attendances.length }} présents</span
              >
            </h2>
            <button
              @click="loadAttendances"
              class="btn btn-ghost btn-sm"
              :disabled="attendancesLoading"
            >
              <span :class="{ spinning: attendancesLoading }">↻</span>
              Rafraîchir
            </button>
          </div>

          <div v-if="attendancesLoading" class="state-inline">
            <div class="spinner spinner-sm"></div>
            <span>Chargement...</span>
          </div>

          <div
            v-else-if="attendances.length === 0"
            class="state-card state-card--inline"
          >
            <div class="empty-icon">📋</div>
            <p>Aucune présence enregistrée pour cette session.</p>
          </div>

          <div v-else class="table-wrap">
            <table class="data-table">
              <thead>
                <tr>
                  <th class="col-cb">
                    <div class="cb-header">
                      <div class="cb-cell-wrap">
                        <div
                          v-if="bulkLoading"
                          class="spinner spinner-sm"
                        ></div>
                        <input
                          v-else
                          type="checkbox"
                          ref="headerCheckbox"
                          :checked="allPresent"
                          @change="toggleSelectAll"
                          class="cb"
                          :disabled="bulkLoading"
                          :title="
                            allPresent ? 'Tout annuler' : 'Tout marquer présent'
                          "
                        />
                      </div>
                      <span class="cb-all-label">{{
                        allPresent ? "Tout annuler" : "Tout présent"
                      }}</span>
                    </div>
                  </th>
                  <th>Nom</th>
                  <th>Prénom</th>
                  <th>Statut</th>
                  <th>Commentaire</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="attendance in attendances"
                  :key="attendance.item1.id"
                  :class="rowClass(attendance.item2)"
                >
                  <td class="col-cb" data-label="Présent">
                    <div class="cb-cell-wrap">
                      <div
                        v-if="
                          pendingStudents.has(attendance.item1.studentNumber)
                        "
                        class="spinner spinner-sm"
                      ></div>
                      <input
                        v-else
                        type="checkbox"
                        :checked="attendance.item2 === 0"
                        @change="
                          togglePresence(
                            attendance.item1.studentNumber,
                            attendance.item2,
                          )
                        "
                        class="cb"
                        :disabled="
                          pendingStudents.has(attendance.item1.studentNumber) ||
                          bulkLoading
                        "
                      />
                    </div>
                  </td>
                  <td class="cell-name" data-label="Nom">
                    {{ attendance.item1.name }}
                  </td>
                  <td data-label="Prénom">{{ attendance.item1.firstname }}</td>
                  <td data-label="Statut">
                    <span
                      class="status-pill"
                      :class="statusPillClass(attendance.item2)"
                    >
                      {{ statusLabel(attendance.item2) }}
                    </span>
                  </td>
                  <td class="col-comment" data-label="Commentaire">
                    <div
                      v-if="
                        editingCommentFor === attendance.item1.studentNumber
                      "
                      class="comment-edit"
                    >
                      <textarea
                        v-model="editingComment"
                        class="comment-textarea"
                        placeholder="Ajouter un commentaire..."
                        @keyup.esc="cancelCommentEdit"
                        ref="commentTextarea"
                        rows="2"
                      ></textarea>
                      <div class="comment-btns">
                        <button
                          class="btn btn-primary btn-sm"
                          @click="saveComment(attendance.item1.studentNumber)"
                        >
                          ✓
                        </button>
                        <button
                          class="btn btn-ghost btn-sm"
                          @click="cancelCommentEdit"
                        >
                          ✕
                        </button>
                      </div>
                    </div>
                    <div v-else class="comment-display">
                      <span
                        class="comment-text"
                        :class="{ 'comment-empty': !attendance.item1.comment }"
                        :title="attendance.item1.comment || ''"
                      >
                        {{ attendance.item1.comment || "—" }}
                      </span>
                      <button
                        class="btn-edit-comment"
                        @click="
                          startCommentEdit(
                            attendance.item1.studentNumber,
                            attendance.item1.comment || '',
                          )
                        "
                        title="Modifier le commentaire"
                      >
                        ✎
                      </button>
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </template>
  </div>
</template>

<script setup>
import { ref, onMounted, nextTick, computed, watch } from "vue";
import { useRoute } from "vue-router";
import SignatureCreator from "../signature/SignatureCreator.vue";
import PopUpProfSignatureWarning from "../popups/PopUpProfSignatureWarning.vue";
import { useProfSignatureStore } from "../../stores/profSignatureStore";
import { useSessionStore } from "../../stores/sessionStore";
import { useProfessorStore } from "../../stores/professorStore";

const route = useRoute();
const token = route.params.token;

const profName = ref("");
const profFirstname = ref("");
const loading = ref(true);
const error = ref("");
const signatureError = ref("");
const signatureSuccess = ref(false);
const submittedName = ref("");
const submittedFirstname = ref("");
const submittedSignature = ref("");
const signaturePad = ref(null);
const validationCode = ref("");
const session = ref(null);

const profSignatureStore = useProfSignatureStore();
const sessionStore = useSessionStore();
const professorStore = useProfessorStore();

const attendances = ref([]);
const attendancesLoading = ref(false);
const editingCommentFor = ref(null);
const editingComment = ref("");
const commentTextarea = ref(null);
const professor1 = ref(null);
const professor2 = ref(null);
const showSignatureWarning = ref(false);
const headerCheckbox = ref(null);
const pendingStudents = ref(new Set());
const bulkLoading = ref(false);

const toastMessage = ref("");
const toastType = ref("toast-success");
let toastTimer = null;

function showToast(msg, type = "toast-success") {
  toastMessage.value = msg;
  toastType.value = type;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => (toastMessage.value = ""), 3500);
}

// Computed
const isMainProfessor = computed(() => {
  if (!session.value) return true;
  return session.value.profSignatureToken === token;
});

const allPresent = computed(
  () =>
    attendances.value.length > 0 &&
    attendances.value.every((a) => a.item2 === 0),
);
const somePresent = computed(
  () => attendances.value.some((a) => a.item2 === 0) && !allPresent.value,
);
const presentCount = computed(
  () => attendances.value.filter((a) => a.item2 === 0).length,
);

watch([allPresent, somePresent], () => {
  if (headerCheckbox.value)
    headerCheckbox.value.indeterminate = somePresent.value;
});

// Helpers UI
function rowClass(status) {
  return {
    "row-present": status === 0,
    "row-absent": status === 1,
    "row-canceled": status === 2,
  };
}
function statusPillClass(status) {
  switch (status) {
    case 0:
      return "pill-present";
    case 1:
      return "pill-absent";
    case 2:
      return "pill-canceled";
    default:
      return "";
  }
}
function statusLabel(status) {
  switch (status) {
    case 0:
      return "Présent";
    case 1:
      return "Absent";
    case 2:
      return "Annulé";
    default:
      return "";
  }
}

// Date / Time
function formatDate(dateString) {
  if (!dateString) return "";
  const date = new Date(dateString);
  return date.toLocaleDateString("fr-FR", {
    day: "2-digit",
    month: "long",
    year: "numeric",
  });
}
function formatTime(timeString) {
  if (!timeString) return "";
  const t = timeString.includes("T") ? timeString.split("T")[1] : timeString;
  return t.substring(0, 5);
}

// Load
async function loadAttendances() {
  if (!session.value?.id) return;
  attendancesLoading.value = true;
  attendances.value = (
    await sessionStore.getSessionAttendances(session.value.id)
  ).sort((a, b) => a.item1.name.localeCompare(b.item1.name));
  attendancesLoading.value = false;
}

const STORAGE_KEY = `prof-sig-${token}`;

function saveToStorage(name, firstname, signature) {
  try {
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({ name, firstname, signature }),
    );
  } catch (_) {}
}

function loadFromStorage() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? JSON.parse(raw) : null;
  } catch (_) {
    return null;
  }
}

onMounted(async () => {
  const data = await profSignatureStore.fetchSessionByProfSignatureToken(token);
  if (data) {
    session.value = data;
    validationCode.value = data.validationCode || "";
    if (data.profId)
      professor1.value = await professorStore.fetchProfessorById(data.profId);
    if (data.profId2)
      professor2.value = await professorStore.fetchProfessorById(data.profId2);

    // Détecter si la signature a déjà été soumise
    const alreadySigned =
      data.profSignatureToken === token
        ? !!data.profSignature
        : !!data.profSignature2;

    if (alreadySigned) {
      const stored = loadFromStorage();
      const backendSig =
        data.profSignatureToken === token
          ? data.profSignature
          : data.profSignature2;
      submittedName.value = stored?.name ?? "";
      submittedFirstname.value = stored?.firstname ?? "";
      // Priorité au localStorage (contient le dessin), fallback sur le backend
      submittedSignature.value = stored?.signature ?? backendSig ?? "";
      signatureSuccess.value = true;
    }

    loading.value = false;
    await loadAttendances();
    await nextTick();
    setTimeout(() => {
      if (signaturePad.value?.forceCanvasReset)
        signaturePad.value.forceCanvasReset();
    }, 300);
  } else {
    error.value = profSignatureStore.error;
    loading.value = false;
  }
});

// Signature
function openSignatureWarning() {
  signatureError.value = "";
  const signatureData = signaturePad.value?.getSignature();
  if (!signatureData || signatureData.length < 30) {
    signatureError.value =
      "Veuillez signer dans la zone prévue avant de valider.";
    return;
  }
  showSignatureWarning.value = true;
}
async function handleSignatureConfirm() {
  showSignatureWarning.value = false;
  await submitSignature();
}
async function submitSignature() {
  const signatureData = signaturePad.value.getSignature();
  if (!signatureData || signatureData.length < 30) {
    signatureError.value = "Merci de signer dans la zone prévue.";
    return;
  }
  const payload = {
    Signature: signatureData,
    Name: profName.value,
    Firstname: profFirstname.value,
  };
  const result = await profSignatureStore.saveProfSignature(token, payload);
  if (result) {
    submittedName.value = profName.value;
    submittedFirstname.value = profFirstname.value;
    submittedSignature.value = signatureData;
    saveToStorage(profName.value, profFirstname.value, signatureData);
    signatureSuccess.value = true;
    signatureError.value = "";
    showToast("Signature enregistrée avec succès !");
  } else {
    signatureError.value = profSignatureStore.error;
  }
}

// Comments
const startCommentEdit = (studentNumber, currentComment) => {
  editingCommentFor.value = studentNumber;
  editingComment.value = currentComment;
  nextTick(() => commentTextarea.value?.focus());
};
const cancelCommentEdit = () => {
  editingCommentFor.value = null;
  editingComment.value = "";
};
watch(editingCommentFor, (v) => {
  if (v) nextTick(() => commentTextarea.value?.focus());
});
const saveComment = async (studentNumber) => {
  if (!session.value?.id) return;
  try {
    const result = await sessionStore.updateAttendanceComment(
      session.value.id,
      studentNumber,
      editingComment.value,
    );
    if (result) {
      const idx = attendances.value.findIndex(
        (a) => a.item1.studentNumber === studentNumber,
      );
      if (idx !== -1)
        attendances.value[idx].item1.comment = editingComment.value;
      cancelCommentEdit();
      showToast("Commentaire enregistré.");
    }
  } catch (e) {
    showToast(
      "Erreur : " +
        (e.response?.status === 403 ? "Autorisation refusée" : e.message),
      "toast-error",
    );
  }
};

// Presence
const toggleSelectAll = async () => {
  if (!session.value?.id || bulkLoading.value) return;
  const headers = { "Prof-Signature-Token": token };
  const targetStatus = allPresent.value ? 2 : 0;
  const toUpdate = attendances.value.filter((a) => a.item2 !== targetStatus);
  if (toUpdate.length === 0) return;

  // Snapshot pour rollback
  const snapshot = toUpdate.map((a) => ({
    sn: a.item1.studentNumber,
    prev: a.item2,
  }));
  // Optimistic update
  toUpdate.forEach((a) => (a.item2 = targetStatus));
  bulkLoading.value = true;
  try {
    await Promise.all(
      toUpdate.map((a) =>
        sessionStore.changeAttendanceStatus(
          session.value.id,
          a.item1.studentNumber,
          targetStatus,
          headers,
        ),
      ),
    );
    const label =
      targetStatus === 0
        ? toUpdate.length + " étudiant(s) marqué(s) présent"
        : toUpdate.length + " présence(s) annulée(s)";
    showToast(label);
  } catch (e) {
    // Rollback
    snapshot.forEach(({ sn, prev }) => {
      const idx = attendances.value.findIndex(
        (a) => a.item1.studentNumber === sn,
      );
      if (idx !== -1) attendances.value[idx].item2 = prev;
    });
    showToast(
      "Erreur globale : " +
        (e.response?.status === 403 ? "Autorisation refusée" : e.message),
      "toast-error",
    );
  } finally {
    bulkLoading.value = false;
  }
};

const togglePresence = async (studentNumber, currentStatus) => {
  if (!session.value?.id || pendingStudents.value.has(studentNumber)) return;
  const newStatus = currentStatus === 0 ? 2 : 0;
  const headers = { "Prof-Signature-Token": token };
  // Optimistic update
  const idx = attendances.value.findIndex(
    (a) => a.item1.studentNumber === studentNumber,
  );
  if (idx !== -1) attendances.value[idx].item2 = newStatus;
  pendingStudents.value = new Set([...pendingStudents.value, studentNumber]);
  try {
    await sessionStore.changeAttendanceStatus(
      session.value.id,
      studentNumber,
      newStatus,
      headers,
    );
  } catch (e) {
    // Rollback
    if (idx !== -1) attendances.value[idx].item2 = currentStatus;
    showToast(
      "Erreur : " +
        (e.response?.status === 403 ? "Autorisation refusée" : e.message),
      "toast-error",
    );
  } finally {
    pendingStudents.value = new Set(
      [...pendingStudents.value].filter((s) => s !== studentNumber),
    );
  }
};
</script>

<style scoped>
/* Layout */
.prof-page {
  width: 100%;
  max-width: 1200px;
  margin: 0 auto;
  padding: 0 4px;
}

/* Toast */
.toast {
  position: fixed;
  top: 76px;
  right: 24px;
  padding: 13px 20px;
  border-radius: 10px;
  font-weight: 600;
  font-size: 0.88rem;
  z-index: 600;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.12);
}
.toast-success {
  background: #d4edda;
  color: #155724;
  border: 1px solid #c3e6cb;
}
.toast-error {
  background: #f8d7da;
  color: #721c24;
  border: 1px solid #f5c6cb;
}

/* Page header */
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 24px;
  flex-wrap: wrap;
  gap: 16px;
}
.page-title h1 {
  font-size: 1.45rem;
  font-weight: 700;
  color: #1a1a2e;
  margin: 0 0 4px;
}
.page-subtitle {
  color: #6c757d;
  font-size: 0.9rem;
  margin: 0;
}

/* Validation code chip */
.code-chip {
  display: inline-flex;
  align-items: center;
  gap: 10px;
  background: #fffceb;
  border: 1.5px solid #f7c948;
  border-radius: 10px;
  padding: 8px 18px;
  box-shadow: 0 2px 8px rgba(247, 201, 72, 0.15);
}
.code-chip-label {
  font-size: 0.72rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: #b8860b;
}
.code-chip-value {
  font-family: "JetBrains Mono", "Fira Mono", "Consolas", monospace;
  font-size: 1.35rem;
  font-weight: 700;
  color: #b8860b;
  letter-spacing: 6px;
}

/* Info row */
.info-row {
  display: flex;
  gap: 16px;
  margin-bottom: 24px;
  flex-wrap: wrap;
}
.info-card {
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 12px;
  padding: 18px 22px;
  flex-shrink: 0;
}
.info-grid {
  display: grid;
  grid-template-columns: repeat(4, auto);
  gap: 8px 28px;
}
.info-item {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.info-label {
  font-size: 0.7rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: #6c757d;
}
.info-value {
  font-size: 0.95rem;
  font-weight: 600;
  color: #1a1a2e;
}

/* Professors */
.professors-row {
  display: flex;
  gap: 12px;
  flex: 1;
  flex-wrap: wrap;
  min-width: 0;
}
.prof-card {
  display: flex;
  align-items: center;
  gap: 14px;
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 12px;
  padding: 14px 18px;
  flex: 1;
  min-width: 220px;
  position: relative;
}
.prof-card--you {
  border-color: #3498db;
  background: #f0f8ff;
}
.prof-card-avatar {
  width: 42px;
  height: 42px;
  border-radius: 50%;
  background: linear-gradient(135deg, #3498db, #2980b9);
  color: #fff;
  font-weight: 700;
  font-size: 0.95rem;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  text-transform: uppercase;
}
.prof-card-avatar--purple {
  background: linear-gradient(135deg, #9b59b6, #8e44ad);
}
.prof-card-role {
  font-size: 0.7rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: #6c757d;
  margin-bottom: 2px;
}
.prof-card-name {
  font-weight: 600;
  font-size: 0.95rem;
  color: #1a1a2e;
}
.prof-card-email {
  font-size: 0.8rem;
  color: #6c757d;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.you-badge {
  position: absolute;
  top: 10px;
  right: 12px;
  background: #3498db;
  color: #fff;
  font-size: 0.7rem;
  font-weight: 700;
  padding: 2px 8px;
  border-radius: 20px;
}

/* Content grid */
.content-grid {
  display: grid;
  grid-template-columns: 340px 1fr;
  gap: 20px;
  align-items: start;
  min-width: 0;
}

/* Panel */
.panel {
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 12px;
  overflow: hidden;
  min-width: 0;
}
.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 20px;
  border-bottom: 1px solid #f0f2f5;
}
.panel-title {
  font-size: 1rem;
  font-weight: 700;
  color: #1a1a2e;
  margin: 0;
  display: flex;
  align-items: center;
  gap: 8px;
}
.count-badge {
  background: #e9ecef;
  color: #495057;
  font-size: 0.75rem;
  font-weight: 700;
  padding: 2px 8px;
  border-radius: 20px;
}

/* Signature form */
.sig-form {
  padding: 20px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
  min-width: 0;
}
.form-group {
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 0;
}
.form-label {
  font-size: 0.72rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.4px;
  color: #6c757d;
}
.form-input {
  padding: 9px 12px;
  border: 1px solid #d1d5db;
  border-radius: 8px;
  font-size: 0.9rem;
  color: #1a1a2e;
  background: #fff;
  outline: none;
  transition:
    border-color 0.2s,
    box-shadow 0.2s;
  font-family: inherit;
  width: 100%;
  box-sizing: border-box;
  min-width: 0;
}
.form-input:focus {
  border-color: #3498db;
  box-shadow: 0 0 0 3px rgba(52, 152, 219, 0.1);
}
.sig-wrapper {
  border: 1px solid #d1d5db;
  border-radius: 8px;
  overflow: hidden;
  background: #fafafa;
}
.form-hint {
  font-size: 0.78rem;
  color: #adb5bd;
  margin: 0;
  text-align: center;
  font-style: italic;
}
.inline-error {
  background: #f8d7da;
  color: #721c24;
  border: 1px solid #f5c6cb;
  border-radius: 8px;
  padding: 10px 14px;
  font-size: 0.85rem;
}
.alert {
  margin: 0 20px 20px;
  padding: 12px 16px;
  border-radius: 8px;
  font-size: 0.88rem;
  font-weight: 600;
}
.alert-success {
  background: #d4edda;
  color: #155724;
  border: 1px solid #c3e6cb;
}

/* Signature recap */
.sig-recap {
  padding: 20px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.sig-recap-identity {
  display: flex;
  align-items: center;
  gap: 12px;
  background: #d4edda;
  border: 1px solid #c3e6cb;
  border-radius: 10px;
  padding: 14px 16px;
}
.sig-recap-avatar {
  width: 40px;
  height: 40px;
  border-radius: 50%;
  background: linear-gradient(135deg, #27ae60, #2ecc71);
  color: #fff;
  font-weight: 700;
  font-size: 0.9rem;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  text-transform: uppercase;
}
.sig-recap-name {
  font-weight: 700;
  font-size: 0.95rem;
  color: #155724;
}
.sig-recap-tag {
  font-size: 0.75rem;
  color: #27ae60;
  font-weight: 600;
  margin-top: 2px;
}
.sig-recap-img-wrap {
  border: 1px solid #e0e4ea;
  border-radius: 8px;
  background: #fafafa;
  padding: 10px;
  display: flex;
  justify-content: center;
}
.sig-recap-img {
  max-width: 100%;
  max-height: 100px;
  object-fit: contain;
}

/* Buttons */
.btn {
  padding: 9px 18px;
  border: none;
  border-radius: 8px;
  font-size: 0.88rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.18s;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  white-space: nowrap;
  font-family: inherit;
}
.btn:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}
.btn-sm {
  padding: 6px 12px;
  font-size: 0.82rem;
}
.btn-full {
  width: 100%;
  justify-content: center;
}
.btn-primary {
  background: #3498db;
  color: #fff;
}
.btn-primary:hover:not(:disabled) {
  background: #2980b9;
  box-shadow: 0 4px 12px rgba(52, 152, 219, 0.25);
}
.btn-ghost {
  background: #f0f2f5;
  color: #495057;
}
.btn-ghost:hover:not(:disabled) {
  background: #e2e6ea;
}
.btn-danger-ghost {
  background: #fef2f2;
  color: #dc2626;
  border: 1px solid #fecaca;
}
.btn-danger-ghost:hover:not(:disabled) {
  background: #fee2e2;
}

/* Table */
.table-wrap {
  overflow-x: auto;
}
.data-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.88rem;
}
.data-table thead tr {
  background: #f8fafc;
  border-bottom: 2px solid #e9ecef;
}
.data-table th {
  padding: 11px 14px;
  text-align: left;
  font-size: 0.72rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: #6c757d;
  white-space: nowrap;
}
.data-table td {
  padding: 11px 14px;
  vertical-align: middle;
  color: #212529;
}
.data-table tbody tr {
  border-bottom: 1px solid #f0f2f5;
  transition: background 0.12s;
}
.data-table tbody tr:last-child {
  border-bottom: none;
}
.data-table tbody tr:hover {
  background: #f8fafc;
}
.row-absent {
  background: rgba(231, 76, 60, 0.04);
}
.row-canceled {
  opacity: 0.6;
}
.col-cb {
  width: 120px;
  text-align: left;
  padding: 11px 10px;
}
.cb {
  width: 16px;
  height: 16px;
  accent-color: #27ae60;
  cursor: pointer;
  vertical-align: middle;
}
.cb:disabled {
  opacity: 0.35;
  cursor: not-allowed;
}
.cb-cell-wrap {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 20px;
}
.cb-header {
  display: flex;
  align-items: center;
  gap: 8px;
}
.cb-all-label {
  font-size: 0.72rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.3px;
  color: #495057;
  white-space: nowrap;
}
.cell-name {
  font-weight: 600;
}

/* Status pills */
.status-pill {
  display: inline-flex;
  align-items: center;
  padding: 3px 10px;
  border-radius: 20px;
  font-size: 0.78rem;
  font-weight: 700;
  white-space: nowrap;
}
.pill-present {
  background: #d4edda;
  color: #155724;
}
.pill-absent {
  background: #f8d7da;
  color: #721c24;
}
.pill-canceled {
  background: #e9ecef;
  color: #6c757d;
}

/* Comment */
.col-comment {
  min-width: 160px;
}
.comment-display {
  display: flex;
  align-items: center;
  gap: 6px;
}
.comment-text {
  font-size: 0.85rem;
  color: #495057;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 140px;
  flex: 1;
}
.comment-empty {
  color: #adb5bd;
  font-style: italic;
}
.btn-edit-comment {
  background: none;
  border: none;
  color: #3498db;
  cursor: pointer;
  font-size: 0.95rem;
  padding: 2px 5px;
  border-radius: 4px;
  transition: background 0.15s;
  flex-shrink: 0;
}
.btn-edit-comment:hover {
  background: #eef6ff;
}
.comment-edit {
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.comment-textarea {
  width: 100%;
  padding: 7px 10px;
  border: 1px solid #d1d5db;
  border-radius: 7px;
  font-size: 0.85rem;
  font-family: inherit;
  resize: vertical;
  outline: none;
  transition: border-color 0.2s;
  box-sizing: border-box;
}
.comment-textarea:focus {
  border-color: #3498db;
  box-shadow: 0 0 0 3px rgba(52, 152, 219, 0.1);
}
.comment-btns {
  display: flex;
  gap: 6px;
  justify-content: flex-end;
}

/* States */
.state-card {
  text-align: center;
  padding: 48px 24px;
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 12px;
  color: #6c757d;
  margin-bottom: 24px;
}
.state-card--inline {
  margin: 16px 20px;
  padding: 36px 24px;
  border: 1px dashed #dee2e6;
  background: #f8fafc;
}
.state-error {
  border-color: #f5c6cb;
  color: #721c24;
}
.empty-icon {
  font-size: 2.2rem;
  margin-bottom: 10px;
}
.state-inline {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 20px;
  color: #6c757d;
  font-size: 0.9rem;
}
.spinner {
  width: 28px;
  height: 28px;
  border: 3px solid #e0e4ea;
  border-top-color: #3498db;
  border-radius: 50%;
  animation: spin 0.7s linear infinite;
  margin: 0 auto 14px;
}
.spinner-sm {
  width: 18px;
  height: 18px;
  margin: 0;
  border-width: 2px;
}
@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}
.spinning {
  display: inline-block;
  animation: spin 0.7s linear infinite;
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

/* Responsive */
@media (max-width: 1024px) {
  .content-grid {
    grid-template-columns: 1fr;
  }
}
@media (max-width: 768px) {
  .page-header {
    flex-direction: column;
  }
  .info-grid {
    grid-template-columns: repeat(2, auto);
  }
  .info-row {
    flex-direction: column;
  }
  .professors-row {
    flex-direction: column;
  }
  .prof-card {
    min-width: 0;
  }
}
@media (max-width: 600px) {
  .prof-page {
    padding: 0;
  }
  .code-chip-value {
    letter-spacing: 3px;
    font-size: 1.1rem;
  }
  .form-row {
    grid-template-columns: 1fr;
  }
  .data-table thead {
    display: none;
  }
  .data-table tbody tr {
    display: block;
    padding: 12px 14px;
    margin-bottom: 12px;
    border: 1px solid #e0e4ea;
    border-radius: 10px;
    background: #fff;
    box-shadow: 0 1px 4px rgba(0, 0, 0, 0.05);
  }
  .data-table tbody tr:last-child {
    border-bottom: 1px solid #e0e4ea;
  }
  .data-table td {
    display: flex;
    align-items: center;
    padding: 6px 0;
    font-size: 0.88rem;
    border: none;
  }
  .data-table td::before {
    content: attr(data-label);
    font-weight: 700;
    font-size: 0.72rem;
    text-transform: uppercase;
    color: #6c757d;
    min-width: 90px;
    flex-shrink: 0;
  }
  .col-cb {
    justify-content: flex-start;
  }
  .col-comment {
    align-items: flex-start;
    flex-direction: column;
  }
  .comment-text {
    max-width: 100%;
    white-space: normal;
  }
}
</style>
