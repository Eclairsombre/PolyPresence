<template>
  <div class="prof-signature-page">
    <div v-if="session" class="session-infos">
      <h2>Informations de la session</h2>
      <ul>
        <li><strong>Année :</strong> {{ session.year || session.Year }}</li>
        <li><strong>Date :</strong> {{ session.date ? new Date(session.date).toLocaleDateString() : (session.Date ? new Date(session.Date).toLocaleDateString() : '') }}</li>
        <li><strong>Heure de début :</strong> {{ session.startTime || session.StartTime }}</li>
        <li><strong>Heure de fin :</strong> {{ session.endTime || session.EndTime }}</li>
        <li class="validation-code-row">
          <strong>Code de validation :</strong>
          <span class="validation-code fancy-validation-code">
            <span class="validation-code-inner">{{ validationCode }}</span>
          </span>
        </li>
      </ul>
    </div>
    <div v-if="loading" class="loading">Chargement...</div>
    <div v-else-if="error" class="error">{{ error }}</div>
    <div v-else class="prof-content-row">
      <div class="prof-signature-form">
        <h2>Signature du professeur</h2>

        <form @submit.prevent="submitSignature" class="prof-form">
          <div class="form-group">
            <label>Nom :</label>
            <input v-model="profName" type="text" required />
          </div>
          <div class="form-group">
            <label>Prénom :</label>
            <input v-model="profFirstname" type="text" required />
          </div>
          <div class="form-group signature-zone">
            <label>Signature :</label>
            <SignatureCreator v-bind:hideSaveButton="true" ref="signaturePad" />
          </div>
          <button type="submit" class="submit-btn">Valider</button>
        </form>
        <div v-if="success" class="success">Signature enregistrée avec succès !</div>
      </div>
      <div class="attendances">
        <div class="attendances-header">
          <h2>Liste des présences</h2>
          <button @click="loadAttendances" class="reload-btn" :disabled="attendancesLoading">Rafraîchir</button>
        </div>
        <div v-if="attendancesLoading" class="loading">Chargement des présences...</div>
        <table v-else class="attendances-table">
          <thead>
            <tr>
              <th>#</th>
              <th>Nom</th>
              <th>Prénom</th>
              <th>Statut</th>
              <th>Action</th>
              <th>Commentaire</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(attendance, idx) in attendances" :key="attendance.item1.id">
              <td>{{ idx + 1 }}</td>
              <td class="cell-name">{{ attendance.item1.name }}</td>
              <td class="cell-firstname">{{ attendance.item1.firstname }}</td>
              <td>
                <span :class="attendance.item2 === 2 ? 'annule' : (attendance.item2 === 1 ? 'absent' : 'present')">
                  {{ attendance.item2 === 2 ? 'Présence annulé' : (attendance.item2 === 1 ? 'Absent' : 'Présent') }}
                </span>
              </td>
              <td>
                <button @click="makeAction(attendance.item2,attendance.item1.studentNumber)" class="action-btn">
                  {{ attendance.item2 === 2 ? 'Marquer comme présent' : (attendance.item2 === 1 ? 'Marquer comme présent' : 'Annuler la présence') }}
                </button>
              </td>
              <td class="comment-cell">
                <div v-if="editingCommentFor === attendance.item1.studentNumber" class="comment-edit">
                  <textarea
                      v-model="editingComment"
                      class="comment-input"
                      placeholder="Ajouter un commentaire..."
                      @keyup.esc="cancelCommentEdit"
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
</template>

<script setup>
import { ref, onMounted } from 'vue';
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

async function loadAttendances() {
  if (!session.value?.id) return;
  attendancesLoading.value = true;
  attendances.value = (await sessionStore.getSessionAttendances(session.value.id)).sort((a, b) => a.item1.name.localeCompare(b.item1.name));
  attendancesLoading.value = false;
}

onMounted(async () => {
  const data = await profSignatureStore.fetchSessionByProfSignatureToken(token);
  if (data) {
    session.value = data;
    validationCode.value = data.validationCode || data.ValidationCode || data.validation_code || '';
    loading.value = false;
    await loadAttendances();
  } else {
    error.value = profSignatureStore.error;
    loading.value = false;
  }
});

const startCommentEdit = (studentNumber, currentComment) => {
  editingCommentFor.value = studentNumber;
  editingComment.value = currentComment;
};

const cancelCommentEdit = () => {
  editingCommentFor.value = null;
  editingComment.value = '';
};

const saveComment = async (studentNumber) => {
  if (!session.value?.id) return;
  
  try {
    const result = await sessionStore.updateAttendanceComment(
      session.value.id,
      studentNumber,
      editingComment.value
    );
    
    if (result) {
      // Mise à jour locale du commentaire
      const attendanceIndex = attendances.value.findIndex(
        a => a.item1.studentNumber === studentNumber
      );
      
      if (attendanceIndex !== -1) {
        attendances.value[attendanceIndex].item1.comment = editingComment.value;
      }
      
      // Fin du mode édition
      editingCommentFor.value = null;
      editingComment.value = '';
    }
  } catch (e) {
    error.value = "Erreur lors de l'enregistrement du commentaire.";
    console.error("Erreur lors de l'enregistrement du commentaire :", e);
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
    await sessionStore.changeAttendanceStatus(session.value.id, studentNumber, newStatus);
    await loadAttendances();
  } catch (e) {
    error.value = "Erreur lors du changement de statut.";
  }
};
</script>

<style scoped>
.session-infos {
  background: #f6f8fa;
  border: 1px solid #e0e4ea;
  border-radius: 8px;
  padding: 18px 16px 10px 16px;
  margin-bottom: 24px;
  box-shadow: 0 1px 4px rgba(52,152,219,0.07);
}
.session-infos h2 {
  margin-top: 0;
  font-size: 1.15em;
  color: #2c3e50;
  margin-bottom: 10px;
}
.session-infos ul {
  list-style: none;
  padding: 0;
  margin: 0;
}
.session-infos li {
  margin-bottom: 6px;
  font-size: 1em;
  color: #34495e;
}
.session-infos strong {
  color: #2980b9;
}
.prof-signature-page {
  max-width: 1100px;
  margin: 40px auto;
  background: #fff;
  border-radius: 16px;
  box-shadow: 0 4px 24px rgba(52,152,219,0.10);
  padding: 36px 32px 32px 32px;
}
.main-title {
  text-align: center;
  margin-bottom: 32px;
  color: #217dbb;
  font-size: 2em;
  letter-spacing: 1px;
  font-weight: 700;
}
.validation-code-row {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-top: 8px;
}
.validation-code {
  font-family: 'Courier New', Courier, monospace;
  font-size: 2em;
  color: #fff;
  background: #217dbb;
  padding: 6px 28px;
  border-radius: 10px;
  letter-spacing: 6px;
  font-weight: bold;
  box-shadow: 0 2px 8px rgba(41,128,185,0.10);
  border: 2px solid #217dbb;
  margin-left: 8px;
}
.fancy-validation-code {
  display: inline-flex;
  align-items: center;
  background: #fffbe6;
  border: 2px solid #f7c948;
  border-radius: 999px;
  box-shadow: 0 2px 8px rgba(247,201,72,0.10);
  padding: 2px 18px;
  margin-left: 8px;
  position: relative;
  font-size: 1em;
  transition: box-shadow 0.2s;
}
.fancy-validation-code::before {
  content: '\1F511';
  font-size: 1.1em;
  margin-right: 8px;
  color: #f7c948;
  filter: drop-shadow(0 1px 2px #f7c94833);
}
.validation-code-inner {
  font-family: 'JetBrains Mono', 'Fira Mono', 'Consolas', monospace;
  font-size: 1.08em;
  color: #b8860b;
  letter-spacing: 6px;
  font-weight: 700;
  text-shadow: 0 1px 4px #fffbe6, 0 1px 0 #f7c94844;
  padding: 0 2px;
  background: none;
}
@media (max-width: 600px) {
  .fancy-validation-code {
    font-size: 0.98em;
    padding: 2px 10px;
  }
  .validation-code-inner {
    letter-spacing: 3px;
  }
}
.prof-content-row {
  display: flex;
  gap: 40px;
  align-items: flex-start;
  margin-top: 32px;
}
.prof-signature-form {
  flex: 1 1 350px;
  min-width: 320px;
  background: #fafdff;
  border-radius: 10px;
  box-shadow: 0 2px 8px rgba(52,152,219,0.08);
  padding: 24px 18px 18px 18px;
}
.signature-zone {
  margin-bottom: 24px;
}
.attendances {
  flex: 1 1 420px;
  min-width: 340px;
  margin-top: 0;
  background: #fafdff;
  border-radius: 10px;
  box-shadow: 0 2px 8px rgba(52,152,219,0.08);
  padding: 24px 18px 18px 18px;
  overflow: hidden; /* Empêche le contenu de déborder */
  display: flex;
  flex-direction: column; /* Organisation du contenu en colonne */
}
.attendances-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 18px;
}
.attendances-table {
  width: 100%;
  border-collapse: collapse;
  background: #fff;
  border-radius: 8px;
  overflow: hidden;
  box-shadow: 0 1px 4px rgba(52,152,219,0.07);
  font-size: 1.08em;
}
.attendances-table th, .attendances-table td {
  padding: 12px 14px;
  text-align: left;
  border-bottom: 1px solid #e0e4ea;
}
.attendances-table th {
  background: #f6f8fa;
  color: #217dbb;
  font-weight: 700;
  font-size: 1.08em;
}
.attendances-table tr:last-child td {
  border-bottom: none;
}
.cell-name, .cell-firstname {
  font-weight: 500;
  letter-spacing: 0.5px;
}
.present {
  color: #27ae60;
  font-weight: bold;
}
.absent {
  color: #e74c3c;
  font-weight: bold;
}
.annule {
  color: #888;
  font-weight: bold;
  font-style: italic;
  text-decoration: line-through;
}
.reload-btn {
  background: #3498db;
  color: #fff;
  border: none;
  border-radius: 4px;
  padding: 7px 18px;
  font-size: 1em;
  cursor: pointer;
  transition: background 0.2s;
  font-weight: 600;
  box-shadow: 0 1px 4px rgba(52,152,219,0.07);
}
.reload-btn:disabled {
  background: #b2cbe4;
  cursor: not-allowed;
}
.reload-btn:hover:not(:disabled) {
  background: #217dbb;
}
.form-group {
  margin-bottom: 22px;
  display: flex;
  flex-direction: column;
}
input[type="text"] {
  padding: 12px;
  border-radius: 5px;
  border: 1.5px solid #bfc9d1;
  font-size: 17px;
  background: #fafdff;
  transition: border 0.2s;
}
input[type="text"]:focus {
  border: 2px solid #3498db;
  outline: none;
}
.submit-btn {
  width: 100%;
  background: linear-gradient(90deg, #3498db 60%, #217dbb 100%);
  color: #fff;
  border: none;
  border-radius: 5px;
  padding: 15px;
  font-size: 18px;
  cursor: pointer;
  margin-top: 12px;
  font-weight: 700;
  box-shadow: 0 2px 8px rgba(52,152,219,0.08);
  transition: background 0.2s;
  letter-spacing: 1px;
}
.submit-btn:hover {
  background: linear-gradient(90deg, #217dbb 60%, #3498db 100%);
}
.success {
  color: #27ae60;
  text-align: center;
  margin-top: 22px;
  font-weight: 700;
  font-size: 1.15em;
}
.error {
  color: #e74c3c;
  text-align: center;
  margin-top: 22px;
  font-weight: 700;
  font-size: 1.15em;
}
.loading {
  text-align: center;
  color: #888;
  font-size: 1.1em;
}
.action-btn {
  background: #3498db;
  color: #fff;
  border: none;
  border-radius: 4px;
  padding: 8px 12px;
  font-size: 0.95em;
  font-weight: 500;
  cursor: pointer;
  transition: background 0.2s, color 0.2s;
  box-shadow: 0 1px 4px rgba(52,152,219,0.07);
  margin: 0 2px;
  white-space: nowrap;
}
.action-btn:hover:not(:disabled) {
  background: #217dbb;
}
.action-btn:disabled {
  background: #b2cbe4;
  color: #eee;
  cursor: not-allowed;
}

/* Conteneur de la table avec défilement */
.attendances > table {
  display: block;
  overflow-x: auto;
  max-width: 100%;
  -webkit-overflow-scrolling: touch;
}

/* Styles pour les commentaires */
.comment-cell {
  position: relative;
  min-width: 200px;
  word-break: break-word;
}

.comment-display {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.comment-text {
  font-size: 0.95em;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 150px;
}

.no-comment {
  font-style: italic;
  color: #aaa;
}

.edit-comment-btn {
  background: none;
  border: none;
  color: #3498db;
  cursor: pointer;
  font-size: 1.1em;
  padding: 2px 8px;
  border-radius: 50%;
  transition: background 0.2s, color 0.2s;
  flex-shrink: 0;
}

.edit-comment-btn:hover {
  background: #eaf6fb;
  color: #217dbb;
}

.icon {
  font-size: 1em;
  display: inline-block;
}

.comment-edit {
  width: 100%;
}

.comment-input {
  width: 100%;
  padding: 8px;
  border: 1px solid #bfc9d1;
  border-radius: 4px;
  font-size: 0.95em;
  font-family: inherit;
  resize: vertical;
  min-height: 70px;
  margin-bottom: 8px;
}

.comment-input:focus {
  outline: none;
  border-color: #3498db;
  box-shadow: 0 0 0 2px rgba(52, 152, 219, 0.2);
}

.comment-actions {
  display: flex;
  justify-content: space-between;
  gap: 8px;
}

.save-comment-btn, .cancel-comment-btn {
  padding: 5px 10px;
  border-radius: 4px;
  border: none;
  font-size: 0.85em;
  cursor: pointer;
  transition: background 0.2s;
  display: flex;
  align-items: center;
  gap: 4px;
}

.save-comment-btn {
  background: #3498db;
  color: white;
}

.save-comment-btn:hover {
  background: #217dbb;
}

.cancel-comment-btn {
  background: #f1f2f6;
  color: #576574;
}

.cancel-comment-btn:hover {
  background: #dfe4ea;
}

@media (max-width: 1100px) {
  .prof-signature-page {
    padding: 24px 20px;
    margin: 20px auto;
  }
  .prof-content-row {
    gap: 18px;
  }
  
  .comment-text {
    max-width: 120px;
  }
}

@media (max-width: 900px) {
  .prof-content-row {
    flex-direction: column;
    gap: 18px;
  }
  .attendances, .prof-signature-form {
    min-width: unset;
    width: 100%;
  }
  
  .comment-text {
    max-width: 180px;
  }
}

@media (max-width: 768px) {
  .prof-signature-page {
    padding: 16px 12px;
    margin: 10px auto;
    border-radius: 8px;
  }
  
  .attendances-table th, .attendances-table td {
    padding: 10px 8px;
  }
  
  .action-btn {
    padding: 6px 10px;
    font-size: 0.9em;
  }
}

@media (max-width: 600px) {
  .prof-signature-page {
    padding: 12px 8px;
    margin: 0;
    border-radius: 0;
    box-shadow: none;
  }
  .prof-content-row {
    flex-direction: column;
    gap: 10px;
  }
  .prof-signature-form, .attendances {
    padding: 12px 10px;
    min-width: unset;
    width: 100%;
  }
  .attendances-table th, .attendances-table td {
    padding: 6px 4px;
    font-size: 0.9em;
  }
  
  .comment-cell {
    min-width: 120px;
  }
  
  .comment-text {
    max-width: 80px;
  }
  
  .comment-input {
    min-height: 50px;
  }
}
</style>
