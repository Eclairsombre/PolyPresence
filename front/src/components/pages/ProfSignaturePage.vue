<template>
  <div class="prof-signature-page">
    <div v-if="session" class="session-infos">
      <div class="session-header">
        <h2>Informations de la session</h2>
        <div class="validation-code-container">
          <div class="validation-code-label">Code de validation :</div>
          <span class="validation-code fancy-validation-code">
            <span class="validation-code-inner">{{ validationCode }}</span>
          </span>
        </div>
      </div>
      
      <div class="session-details">
        <div class="session-column">
          <div class="detail-item">
            <div class="detail-label">Année :</div>
            <div class="detail-value">{{ session.year || session.Year }}</div>
          </div>
          <div class="detail-item">
            <div class="detail-label">Date :</div>
            <div class="detail-value">{{ session.date ? new Date(session.date).toLocaleDateString() : (session.Date ? new Date(session.Date).toLocaleDateString() : '') }}</div>
          </div>
        </div>
        
        <div class="session-column">
          <div class="detail-item">
            <div class="detail-label">Heure de début :</div>
            <div class="detail-value">{{ session.startTime || session.StartTime }}</div>
          </div>
          <div class="detail-item">
            <div class="detail-label">Heure de fin :</div>
            <div class="detail-value">{{ session.endTime || session.EndTime }}</div>
          </div>
        </div>
      </div>
      
      <div class="professors-container">
        <div v-if="session.profName || session.profFirstname" class="professor-info">
          <div class="professor-title">Professeur 1 :</div>
          <div class="professor-name">{{ session.profFirstname }} {{ session.profName }}</div>
          <span v-if="isMainProfessor" class="current-professor-badge">👤 Vous</span>
        </div>
        <div v-if="session.profName2 || session.profFirstname2" class="co-professor-info">
          <div class="professor-title">Professeur 2 :</div>
          <div class="professor-name">{{ session.profFirstname2 }} {{ session.profName2 }}</div>
          <span v-if="!isMainProfessor" class="current-professor-badge">👤 Vous</span>
        </div>
      </div>
    </div>
    <div v-if="loading" class="loading-container">
      <div class="loading-spinner"></div>
      <div class="loading-text">Chargement des informations de session...</div>
    </div>
    <div v-else-if="error" class="error">
      <div class="error-icon">⚠️</div>
      <div class="error-message">{{ error }}</div>
    </div>
    <div v-else class="prof-content-row">
      <div class="prof-signature-form">
        <h3 class="section-title">Signature du Professeur</h3>
        
        <form @submit.prevent="submitSignature" class="prof-form">
          <div class="form-row">
            <div class="form-group">
              <label>Prénom :</label>
              <input v-model="profFirstname" type="text" required placeholder="Entrez votre prénom" />
            </div>
            <div class="form-group">
              <label>Nom :</label>
              <input v-model="profName" type="text" required placeholder="Entrez votre nom" />
            </div>
          </div>
          <div class="form-group signature-zone">
            <label>Signature :</label>
            <div class="signature-container">
              <SignatureCreator v-bind:hideSaveButton="true" ref="signaturePad" />
              <div class="signature-instruction">Veuillez signer dans la zone ci-dessus</div>
            </div>
          </div>
          <button type="submit" class="submit-btn">
            <span class="btn-icon">✓</span>
            <span class="btn-text">Valider la signature</span>
          </button>
        </form>
        <div v-if="success" class="success-message">
          <div class="success-icon">✓</div>
          <div class="success-text">Signature enregistrée avec succès !</div>
        </div>
      </div>
      <div class="attendances">
        <div class="attendances-header">
          <h3 class="section-title">Liste des présences</h3>
          <div class="attendances-actions">
            <button @click="loadAttendances" class="reload-btn" :disabled="attendancesLoading">
              <span class="reload-icon">↻</span>
              <span class="reload-text">Rafraîchir</span>
            </button>
          </div>
        </div>
        
        <div v-if="attendancesLoading" class="loading-container attendance-loading">
          <div class="loading-spinner"></div>
          <div class="loading-text">Chargement des présences...</div>
        </div>
        
        <div v-else-if="attendances.length === 0" class="no-attendances">
          <div class="no-data-icon">📋</div>
          <div class="no-data-text">Aucune présence enregistrée pour cette session</div>
        </div>
        
        <div v-else class="attendances-table-container">
          <table class="attendances-table">
            <colgroup>
              <col style="width: 5%;">
              <col style="width: 18%;">
              <col style="width: 18%;">
              <col style="width: 15%;">
              <col style="width: 18%;">
              <col style="width: 26%;">
            </colgroup>
            <thead>
              <tr>
                <th class="column-id">#</th>
                <th class="column-name">Nom</th>
                <th class="column-firstname">Prénom</th>
                <th class="column-status">Statut</th>
                <th class="column-action">Action</th>
                <th class="column-comment">Commentaire</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(attendance, idx) in attendances" :key="attendance.item1.id" 
                  :class="[attendance.item2 === 0 ? 'status-present' : 
                          attendance.item2 === 1 ? 'status-absent' : 'status-canceled']">
                <td class="cell-id">{{ idx + 1 }}</td>
                <td class="cell-name">{{ attendance.item1.name }}</td>
                <td class="cell-firstname">{{ attendance.item1.firstname }}</td>
                <td class="cell-status">
                  <div class="status-badge" :class="getStatusClass(attendance.item2)">
                    <span class="status-icon">{{ getStatusIcon(attendance.item2) }}</span>
                    <span class="status-text">
                      {{ attendance.item2 === 2 ? 'Présence annulée' : (attendance.item2 === 1 ? 'Absent' : 'Présent') }}
                    </span>
                  </div>
                </td>
                <td class="cell-action">
                  <button @click="makeAction(attendance.item2,attendance.item1.studentNumber)" 
                          class="action-btn"
                          :class="getActionClass(attendance.item2)">
                    {{ attendance.item2 === 2 || attendance.item2 === 1 ? 'Marquer présent' : 'Annuler présence' }}
                  </button>
                </td>
                <td class="comment-cell">
                  <div v-if="editingCommentFor === attendance.item1.studentNumber" class="comment-edit">
                    <textarea
                        v-model="editingComment"
                        class="comment-input"
                        placeholder="Ajouter un commentaire..."
                        @keyup.esc="cancelCommentEdit"
                        ref="commentTextarea"
                    ></textarea>
                    <div class="comment-actions">
                      <button class="save-comment-btn" @click="saveComment(attendance.item1.studentNumber)">
                        <span class="icon">✓</span> Enregistrer
                      </button>
                      <button class="cancel-comment-btn" @click="cancelCommentEdit">
                        <span class="icon">✕</span> Annuler
                      </button>
                    </div>
                  </div>
                  <div v-else class="comment-display">
                    <span class="comment-text" :class="{ 'no-comment': !attendance.item1.comment }">
                      {{ attendance.item1.comment || 'Aucun commentaire' }}
                    </span>
                    <button class="edit-comment-btn" @click="startCommentEdit(attendance.item1.studentNumber, attendance.item1.comment || '')">
                      <span class="icon">✎</span>
                    </button>
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

<script setup>
import { ref, onMounted, nextTick, computed, watch } from 'vue';
import { useRoute } from 'vue-router';
import SignatureCreator from '../signature/SignatureCreator.vue';
import { useProfSignatureStore } from '../../stores/profSignatureStore';
import { useSessionStore } from '../../stores/sessionStore';

const route = useRoute();
const token = route.params.token;
const profName = ref('');
const profFirstname = ref('');
const loading = ref(true);
const error = ref('');
const success = ref(false);
const signaturePad = ref(null);
const validationCode = ref('');
const session = ref(null);
const profSignatureStore = useProfSignatureStore();
const sessionStore = useSessionStore();
const attendances = ref([]);
const attendancesLoading = ref(false);
const editingCommentFor = ref(null);
const editingComment = ref('');
const commentTextarea = ref(null);

const isMainProfessor = computed(() => {
  if (!session.value) return true;
  return session.value.profSignatureToken === token;
});

const getStatusClass = (status) => {
  switch(status) {
    case 0: return 'status-present';
    case 1: return 'status-absent';
    case 2: return 'status-canceled';
    default: return '';
  }
};

const getStatusIcon = (status) => {
  switch(status) {
    case 0: return '✓';
    case 1: return '✗';
    case 2: return '○';
    default: return '';
  }
};

const getActionClass = (status) => {
  switch(status) {
    case 0: return 'action-cancel';
    case 1:
    case 2: return 'action-mark-present';
    default: return '';
  }
};


async function loadAttendances() {
  if (!session.value?.id) return;
  attendancesLoading.value = true;
  attendances.value = (await sessionStore.getSessionAttendances(session.value.id)).sort((a, b) => a.item1.name.localeCompare(b.item1.name));
  attendancesLoading.value = false;
}

onMounted(async () => {
  
  const pathParts = window.location.pathname.split("/");
  const profSignatureTokenIndex = pathParts.indexOf("prof-signature") + 1;
  if (profSignatureTokenIndex > 0 && profSignatureTokenIndex < pathParts.length) {
    console.log("Token extrait par l'intercepteur:", pathParts[profSignatureTokenIndex]);
  } else {
    console.log("Impossible d'extraire le token comme le fait l'intercepteur:", pathParts);
  }
  
  const data = await profSignatureStore.fetchSessionByProfSignatureToken(token);
  if (data) {
    session.value = data;
    validationCode.value = data.validationCode || '';
    loading.value = false;
    await loadAttendances();
    
    await nextTick();
    setTimeout(() => {
      if (signaturePad.value && signaturePad.value.forceCanvasReset) {
        signaturePad.value.forceCanvasReset();
      }
    }, 300);
  } else {
    error.value = profSignatureStore.error;
    loading.value = false;
  }
});

const startCommentEdit = (studentNumber, currentComment) => {
  editingCommentFor.value = studentNumber;
  editingComment.value = currentComment;
  
  nextTick(() => {
    if (commentTextarea.value) {
      commentTextarea.value.focus();
    }
  });
};

const cancelCommentEdit = () => {
  editingCommentFor.value = null;
  editingComment.value = '';
};

watch(editingCommentFor, (newValue) => {
  if (newValue) {
    nextTick(() => {
      if (commentTextarea.value) {
        commentTextarea.value.focus();
      }
    });
  }
});

const saveComment = async (studentNumber) => {
  if (!session.value?.id) return;
  
  const pathParts = window.location.pathname.split("/");
  const profSignatureTokenIndex = pathParts.indexOf("prof-signature") + 1;
  if (profSignatureTokenIndex > 0 && profSignatureTokenIndex < pathParts.length) {
    console.log("Token trouvé dans l'URL pour commentaire:", pathParts[profSignatureTokenIndex]);
  } else {
    console.log("Token non trouvé dans l'URL pour commentaire, parties du chemin:", pathParts);
  }
  
  try {
    const result = await sessionStore.updateAttendanceComment(
      session.value.id,
      studentNumber,
      editingComment.value
    );
    
    if (result) {
      const attendanceIndex = attendances.value.findIndex(
        a => a.item1.studentNumber === studentNumber
      );
      
      if (attendanceIndex !== -1) {
        attendances.value[attendanceIndex].item1.comment = editingComment.value;
      }
      
      editingCommentFor.value = null;
      editingComment.value = '';
    }
  } catch (e) {
    console.error("Erreur détaillée lors de l'enregistrement du commentaire:", e);
    error.value = "Erreur lors de l'enregistrement du commentaire: " + (e.response?.status === 403 ? "Autorisation refusée" : e.message);
  }
};

const submitSignature = async () => {
  const signatureData = signaturePad.value.getSignature();
  if (!signatureData || signatureData.length < 30) {
    error.value = "Merci de signer dans la zone prévue.";
    return;
  }
  const payload = {
    Signature: signatureData,
    Name: profName.value,
    Firstname: profFirstname.value
  };
  const result = await profSignatureStore.saveProfSignature(token, payload);
  if (result) {
    success.value = true;
    error.value = '';
  } else {
    error.value = profSignatureStore.error;
  }
};

const makeAction = async (action, studentNumber) => {
  if (!session.value?.id) return;
  
  const pathParts = window.location.pathname.split("/");
  const profSignatureTokenIndex = pathParts.indexOf("prof-signature") + 1;
  if (profSignatureTokenIndex > 0 && profSignatureTokenIndex < pathParts.length) {
    console.log("Token trouvé dans l'URL:", pathParts[profSignatureTokenIndex]);
  } else {
    console.log("Token non trouvé dans l'URL, parties du chemin:", pathParts);
  }
  
  let newStatus;
  switch (action) {
    case 1:
    case 2:
      newStatus = 0;
      break;
    case 0:
      newStatus = 2;
      break;
    default:
      return;
  }
  
  try {
    
    const headers = { "Prof-Signature-Token": token };
    await sessionStore.changeAttendanceStatus(session.value.id, studentNumber, newStatus, headers);
    
    const attendanceIndex = attendances.value.findIndex(
      a => a.item1.studentNumber === studentNumber
    );
    
    if (attendanceIndex !== -1) {
      attendances.value[attendanceIndex].item2 = newStatus;
    }
  } catch (e) {
    error.value = "Erreur lors du changement de statut: " + (e.response?.status === 403 ? "Autorisation refusée" : e.message);
  }
};
</script>

<style scoped>
.prof-signature-page {
  max-width: 1100px;
  margin: 40px auto;
  background: #fff;
  border-radius: 16px;
  box-shadow: 0 4px 24px rgba(52,152,219,0.15);
  padding: 36px 32px 32px 32px;
}

/* Session info styling */
.session-infos {
  background: #f8fafc;
  border: 1px solid #e0e7ff;
  border-radius: 12px;
  padding: 20px;
  margin-bottom: 30px;
  box-shadow: 0 2px 10px rgba(52,152,219,0.08);
}

.session-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 20px;
  margin-bottom: 20px;
  border-bottom: 1px solid #e0e7ff;
  padding-bottom: 15px;
}

.session-header h2 {
  margin: 0;
  font-size: 1.4em;
  color: #2c3e50;
  font-weight: 700;
}

.validation-code-container {
  display: flex;
  align-items: center;
  gap: 15px;
}

.validation-code-label {
  font-weight: 600;
  color: #2980b9;
  font-size: 1.1em;
}

.session-details {
  display: flex;
  gap: 40px;
  flex-wrap: wrap;
}

.session-column {
  flex: 1;
  min-width: 200px;
}

.detail-item {
  display: flex;
  margin-bottom: 12px;
  align-items: center;
}

.detail-label {
  width: 120px;
  font-weight: 600;
  color: #2980b9;
}

.detail-value {
  flex: 1;
  font-weight: 500;
}

.professors-container {
  display: flex;
  gap: 20px;
  margin-top: 20px;
  flex-wrap: wrap;
}

.professor-info, .co-professor-info {
  flex: 1;
  min-width: 250px;
  padding: 12px 15px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}

.professor-info {
  background-color: #ecf0f1;
  border-left: 4px solid #3498db;
}

.co-professor-info {
  background-color: #ecf0f1;
  border-left: 4px solid #9b59b6;
}

.professor-title {
  font-weight: 600;
  color: #2c3e50;
}

.professor-name {
  font-weight: 500;
}

.validation-code {
  font-family: 'Courier New', Courier, monospace;
  font-size: 1.6em;
  color: #fff;
  background: #217dbb;
  padding: 6px 28px;
  border-radius: 10px;
  letter-spacing: 6px;
  font-weight: bold;
  box-shadow: 0 2px 8px rgba(41,128,185,0.10);
  border: 2px solid #217dbb;
}
.fancy-validation-code {
  display: inline-flex;
  align-items: center;
  background: #fffceb;
  border: 2px solid #f7c948;
  border-radius: 10px;
  box-shadow: 0 4px 12px rgba(247,201,72,0.15);
  padding: 8px 20px;
  position: relative;
  font-size: 1em;
  transition: all 0.3s ease;
}

.fancy-validation-code:hover {
  box-shadow: 0 6px 16px rgba(247,201,72,0.2);
  transform: translateY(-1px);
}

.fancy-validation-code::before {
  content: '\1F511';
  font-size: 1.3em;
  margin-right: 12px;
  color: #e8b215;
  filter: drop-shadow(0 1px 2px rgba(184, 134, 11, 0.3));
}

.validation-code-inner {
  font-family: 'JetBrains Mono', 'Fira Mono', 'Consolas', monospace;
  font-size: 1.2em;
  color: #b8860b;
  letter-spacing: 8px;
  font-weight: 700;
  text-shadow: 0 1px 1px rgba(255, 251, 230, 0.8);
  padding: 0 2px;
}

.current-professor-badge {
  background: linear-gradient(to right, #27ae60, #2ecc71);
  color: white;
  padding: 4px 12px;
  border-radius: 30px;
  font-size: 0.8em;
  font-weight: bold;
  box-shadow: 0 2px 6px rgba(46, 204, 113, 0.2);
}

/* Layout */
.prof-content-row {
  display: flex;
  flex-direction: column;
  gap: 30px;
  margin-top: 32px;
  width: 100%;
}

.prof-signature-form {
  width: 100%;
  background: #ffffff;
  border-radius: 12px;
  box-shadow: 0 4px 20px rgba(52,152,219,0.12);
  padding: 25px 22px;
  display: flex;
  flex-direction: column;
}

.section-title {
  font-size: 1.25em;
  color: #2c3e50;
  margin-top: 0;
  margin-bottom: 20px;
  padding-bottom: 10px;
  border-bottom: 2px solid #e0e7ff;
  font-weight: 600;
}

.form-row {
  display: flex;
  gap: 15px;
}

.signature-zone {
  margin-bottom: 25px;
}

.signature-container {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.signature-instruction {
  color: #7f8c8d;
  font-size: 0.9em;
  text-align: center;
  font-style: italic;
}

/* Attendances section */
.attendances {
  width: 100%;
  background: #ffffff;
  border-radius: 12px;
  box-shadow: 0 4px 20px rgba(52,152,219,0.12);
  padding: 25px 22px;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.attendances-header {
  display: flex;
  flex-direction: column;
  gap: 15px;
  margin-bottom: 20px;
}

.attendances-actions {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 15px;
}


.attendances-table-container {
  border-radius: 8px;
  width: 100%;
}

.attendances-table {
  width: 100%;
  table-layout: fixed;
  border-collapse: separate;
  border-spacing: 0;
  background: #fff;
  border-radius: 8px;
  overflow: hidden;
  box-shadow: 0 1px 8px rgba(52,152,219,0.12);
  font-size: 0.95em;
}

.attendances-table th, .attendances-table td {
  padding: 14px 10px;
  text-align: left;
  word-wrap: break-word;
  overflow: hidden;
}

.attendances-table th {
  background: #f8fafc;
  color: #2c3e50;
  font-weight: 600;
  font-size: 0.8em;
  position: sticky;
  top: 0;
  box-shadow: 0 1px 0 rgba(52,152,219,0.15);
  white-space: nowrap;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.attendances-table tr {
  transition: background-color 0.15s;
}

.attendances-table tbody tr:hover {
  background-color: #f8fafc;
}

.attendances-table tbody tr:not(:last-child) td {
  border-bottom: 1px solid #edf2f7;
}

.cell-id {
  text-align: center;
  font-weight: 600;
  color: #7f8c8d;
}

.cell-name, .cell-firstname {
  font-weight: 600;
  letter-spacing: 0.2px;
  max-width: 100%;
  text-overflow: ellipsis;
  overflow: hidden;
}

/* Status styling */
.status-badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 10px;
  border-radius: 30px;
  font-weight: 500;
  font-size: 0.9em;
  white-space: nowrap;
}

.status-present {
  background-color: rgba(46, 204, 113, 0.15);
  color: #27ae60;
}

.status-absent {
  background-color: rgba(231, 76, 60, 0.15);
  color: #c0392b;
}

.status-canceled {
  background-color: rgba(149, 165, 166, 0.15);
  color: #7f8c8d;
  text-decoration: line-through;
}

/* Actions et boutons */
.reload-btn {
  background: #3498db;
  color: #fff;
  border: none;
  border-radius: 8px;
  padding: 8px 15px;
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 0.9em;
  cursor: pointer;
  transition: all 0.2s;
  font-weight: 500;
  box-shadow: 0 2px 6px rgba(52,152,219,0.15);
}

.reload-icon {
  font-size: 1.1em;
}

.reload-btn:disabled {
  background: #b2cbe4;
  cursor: not-allowed;
  opacity: 0.7;
}

.reload-btn:hover:not(:disabled) {
  background: #217dbb;
  transform: translateY(-1px);
  box-shadow: 0 4px 8px rgba(52,152,219,0.2);
}

/* Form elements */
.form-group {
  margin-bottom: 20px;
  display: flex;
  flex-direction: column;
  gap: 8px;
  flex: 1;
}

.form-group label {
  font-weight: 500;
  color: #2c3e50;
  font-size: 0.95em;
}

input[type="text"] {
  padding: 12px 15px;
  border-radius: 8px;
  border: 2px solid #e2e8f0;
  font-size: 1em;
  background: #fff;
  transition: all 0.2s ease;
  box-shadow: 0 1px 3px rgba(0,0,0,0.05), inset 0 1px 0 rgba(255,255,255,0.8);
}
input[type="text"]:focus {
  border-color: #3498db;
  outline: none;
  box-shadow: 0 0 0 3px rgba(52,152,219,0.15), inset 0 1px 0 rgba(255,255,255,0.8);
}

.submit-btn {
  width: 100%;
  background: #3498db;
  color: #fff;
  border: none;
  border-radius: 8px;
  padding: 14px;
  font-size: 1.05em;
  cursor: pointer;
  margin-top: 15px;
  font-weight: 600;
  box-shadow: 0 4px 12px rgba(52,152,219,0.15);
  transition: all 0.3s ease;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 10px;
  letter-spacing: 0.5px;
}

.btn-icon {
  font-size: 1.1em;
}

.submit-btn:hover {
  background: #2980b9;
  transform: translateY(-2px);
  box-shadow: 0 6px 16px rgba(52,152,219,0.2);
}

.submit-btn:active {
  transform: translateY(0);
  box-shadow: 0 2px 8px rgba(52,152,219,0.15);
}

/* États et messages */
.success-message {
  display: flex;
  align-items: center;
  gap: 10px;
  background-color: rgba(46, 204, 113, 0.1);
  border-left: 4px solid #2ecc71;
  padding: 12px 15px;
  border-radius: 6px;
  margin-top: 20px;
}

.success-icon {
  background-color: #2ecc71;
  color: white;
  width: 24px;
  height: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  font-size: 0.9em;
}

.success-text {
  color: #27ae60;
  font-weight: 600;
  font-size: 1em;
}

.error {
  display: flex;
  align-items: center;
  gap: 10px;
  background-color: rgba(231, 76, 60, 0.1);
  border-left: 4px solid #e74c3c;
  padding: 12px 15px;
  border-radius: 6px;
  margin: 20px 0;
}

.error-icon {
  font-size: 1.5em;
  color: #e74c3c;
}

.error-message {
  color: #c0392b;
  font-weight: 500;
}

/* Loader */
.loading-container {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 15px;
  padding: 30px 0;
}

.loading-spinner {
  width: 40px;
  height: 40px;
  border: 3px solid rgba(52,152,219,0.2);
  border-radius: 50%;
  border-top-color: #3498db;
  animation: spin 1s linear infinite;
}

.loading-text {
  color: #7f8c8d;
  font-weight: 500;
  font-size: 1em;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}
.action-btn {
  border: none;
  border-radius: 6px;
  padding: 8px 12px;
  font-size: 0.85em;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
  box-shadow: 0 2px 6px rgba(0,0,0,0.08);
  margin: 0;
  white-space: nowrap;
  min-width: 115px;
  text-align: center;
}

.action-mark-present {
  background: #3498db;
  color: #fff;
}

.action-cancel {
  background: #e74c3c;
  color: #fff;
}

.action-btn:hover {
  transform: translateY(-1px);
  box-shadow: 0 4px 8px rgba(0,0,0,0.12);
}

.action-mark-present:hover {
  background: #2980b9;
}

.action-cancel:hover {
  background: #c0392b;
}

.action-btn:disabled {
  background: #b2cbe4;
  color: #eee;
  cursor: not-allowed;
  opacity: 0.7;
}

/* Attendances Table Container */
.attendances-table-container {
  border-radius: 8px;
  overflow: hidden;
}

/* Styles pour les commentaires */
.comment-cell {
  position: relative;
  width: 26%;
}

.no-attendances {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 40px 0;
  color: #7f8c8d;
  gap: 15px;
}

.no-data-icon {
  font-size: 3em;
  color: #b2bec3;
}

.no-data-text {
  font-size: 1.1em;
  font-weight: 500;
}

.comment-display {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 8px;
  position: relative;
}

.comment-text {
  font-size: 0.9em;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  flex: 1;
  padding: 4px 0;
  transition: all 0.2s;
  border-radius: 4px;
}

.comment-text:hover {
  white-space: normal;
  overflow: visible;
  background-color: #f8f9fa;
  padding: 4px 8px;
  box-shadow: 0 2px 5px rgba(0,0,0,0.05);
  position: absolute;
  z-index: 10;
  left: 0;
  width: calc(100% - 40px);
}

.no-comment {
  font-style: italic;
  color: #b2bec3;
}

.edit-comment-btn {
  background: none;
  border: none;
  color: #3498db;
  cursor: pointer;
  font-size: 1em;
  width: 28px;
  height: 28px;
  padding: 0;
  border-radius: 50%;
  transition: all 0.2s;
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: center;
}

.edit-comment-btn:hover {
  background: #eaf6fb;
  color: #217dbb;
  transform: scale(1.1);
}

.icon {
  font-size: 0.9em;
  display: inline-block;
  line-height: 1;
}

.comment-edit {
  width: 100%;
  position: relative;
  z-index: 5;
}

.comment-input {
  width: 100%;
  padding: 10px 12px;
  border: 2px solid #dfe6e9;
  border-radius: 8px;
  font-size: 0.9em;
  font-family: inherit;
  resize: vertical;
  min-height: 80px;
  margin-bottom: 10px;
  transition: all 0.2s ease;
  box-shadow: inset 0 1px 3px rgba(0,0,0,0.05);
}

.comment-input:focus {
  outline: none;
  border-color: #3498db;
  box-shadow: 0 0 0 3px rgba(52, 152, 219, 0.15), inset 0 1px 3px rgba(0,0,0,0.02);
}

.comment-actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
}

.save-comment-btn, .cancel-comment-btn {
  padding: 7px 12px;
  border-radius: 6px;
  border: none;
  font-size: 0.85em;
  cursor: pointer;
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  gap: 6px;
  font-weight: 500;
}

.save-comment-btn {
  background: #3498db;
  color: white;
  box-shadow: 0 2px 6px rgba(52,152,219,0.2);
}

.save-comment-btn:hover {
  background: #2980b9;
  transform: translateY(-1px);
  box-shadow: 0 3px 8px rgba(52,152,219,0.25);
}

.cancel-comment-btn {
  background: #f5f6fa;
  color: #576574;
  box-shadow: 0 2px 6px rgba(0,0,0,0.05);
}

.cancel-comment-btn:hover {
  background: #e9ecf2;
  transform: translateY(-1px);
  box-shadow: 0 3px 8px rgba(0,0,0,0.08);
}

/* Responsive Styles */
@media (max-width: 1100px) {
  .prof-signature-page {
    padding: 25px 20px;
    margin: 20px auto;
    max-width: 95%;
  }
  
  .prof-content-row {
    gap: 20px;
  }
  
  .validation-code-inner {
    letter-spacing: 6px;
  }
  
  .session-details {
    gap: 20px;
  }
  
  .form-row {
    flex-direction: column;
  }
}

@media (max-width: 900px) {
  .session-header {
    flex-direction: column;
    align-items: flex-start;
  }
  
  .validation-code-container {
    width: 100%;
    justify-content: center;
    margin-top: 10px;
  }
  
  .attendances-actions {
    display: flex;
    justify-content: flex-end;
  }
  
  .reload-btn {
    width: auto;
    justify-content: center;
  }
  
  .column-action {
    width: 22%;
  }
  
  .column-comment {
    width: 22%;
  }
}

@media (max-width: 768px) {
  .prof-signature-page {
    padding: 20px 15px;
    margin: 15px auto;
    border-radius: 10px;
  }
  
  .session-details {
    flex-direction: column;
    gap: 5px;
  }
  
  .attendances-table th, .attendances-table td {
    padding: 12px 8px;
    font-size: 0.9em;
  }
  
  .action-btn {
    padding: 6px 8px;
    font-size: 0.8em;
    min-width: auto;
  }
  
  table.attendances-table {
    table-layout: fixed;
  }
  
  table.attendances-table colgroup col:nth-child(1) {
    width: 5%;
  }
  table.attendances-table colgroup col:nth-child(2) {
    width: 17%;
  }
  table.attendances-table colgroup col:nth-child(3) {
    width: 17%;
  }
  table.attendances-table colgroup col:nth-child(4) {
    width: 15%;
  }
  table.attendances-table colgroup col:nth-child(5) {
    width: 20%;
  }
  table.attendances-table colgroup col:nth-child(6) {
    width: 26%;
  }
}
  

@media (max-width: 600px) {
  .prof-signature-page {
    padding: 15px 12px;
    margin: 10px;
    border-radius: 8px;
    box-shadow: 0 2px 15px rgba(52,152,219,0.10);
  }
  
  .prof-content-row {
    gap: 15px;
  }
  
  .prof-signature-form, .attendances {
    padding: 15px;
    border-radius: 8px;
  }
  
  /* Réorganisation de la table pour téléphone */
  .attendances-table {
    display: block;
  }
  
  .attendances-table thead {
    display: none; /* Cacher l'en-tête sur mobile */
  }
  
  .attendances-table tbody {
    display: block;
    width: 100%;
  }
  
  .attendances-table tr {
    display: block;
    margin-bottom: 15px;
    padding: 10px;
    border-radius: 8px;
    background-color: #f8f9fa;
    border: 1px solid #e0e7ff;
    box-shadow: 0 2px 5px rgba(0,0,0,0.05);
  }
  
  .attendances-table td {
    display: block;
    padding: 8px 4px;
    font-size: 0.9em;
    border: none;
    text-align: left;
    width: 100%;
  }
  
  /* Ajouter des labels pour remplacer les en-têtes de colonne */
  .cell-name::before {
    content: "Nom: ";
    font-weight: bold;
    color: #2980b9;
  }
  
  .cell-firstname::before {
    content: "Prénom: ";
    font-weight: bold;
    color: #2980b9;
  }
  
  /* Cacher le numéro pour économiser de l'espace */
  .cell-id {
    display: none;
  }
  
  .cell-status {
    margin: 10px 0;
  }
  
  .status-badge {
    display: inline-flex;
    padding: 6px 10px;
    font-size: 0.95em;
  }
  
  .action-btn {
    padding: 10px 15px;
    font-size: 0.9em;
    min-width: 0;
    width: 100%;
    margin: 8px 0;
  }
  
  .comment-cell {
    border-top: 1px solid #e5e7eb;
    margin-top: 10px;
    padding-top: 10px !important;
  }
  
  .comment-cell::before {
    content: "Commentaire: ";
    font-weight: bold;
    color: #2980b9;
    display: block;
    margin-bottom: 5px;
  }
  
  .comment-text {
    white-space: normal;
    padding: 5px 0;
  }
  
  .comment-text:hover {
    white-space: normal;
    overflow: visible;
    background-color: transparent;
    box-shadow: none;
    position: static;
    width: auto;
    padding: 5px 0;
  }
  
  .validation-code-inner {
    letter-spacing: 3px;
    font-size: 0.9em;
  }
  
  .fancy-validation-code::before {
    font-size: 1.1em;
    margin-right: 8px;
  }
  
  .fancy-validation-code {
    padding: 5px 10px;
  }
  
  .submit-btn {
    padding: 12px;
  }
  
  .section-title {
    font-size: 1.1em;
  }
}

/* Pour les très petits écrans */
@media (max-width: 400px) {
  .prof-signature-page {
    padding: 10px 8px;
    margin: 5px;
  }
  
  .attendances-table tr {
    padding: 8px;
    margin-bottom: 12px;
  }
  
  .attendances-table td {
    padding: 5px 3px;
    font-size: 0.8em;
  }
  
  .action-btn {
    padding: 8px 10px;
    font-size: 0.8em;
  }
  
  .status-badge {
    padding: 5px 8px;
    font-size: 0.85em;
  }
  
  .comment-cell {
    padding-top: 8px !important;
  }
  
  .comment-input {
    min-height: 60px;
  }
  
  .comment-actions button {
    padding: 6px 8px;
    font-size: 0.8em;
  }
  
  .validation-code-inner {
    letter-spacing: 2px;
    font-size: 0.8em;
  }
}
</style>
