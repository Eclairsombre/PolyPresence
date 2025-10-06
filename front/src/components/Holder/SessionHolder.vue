<template>
    <div class="attendance-sheet">
        <div v-if="authStore.user && authStore.user.existsInDb === false" class="error-state">
            <p>Vous n'êtes pas présent dans la base de données. Veuillez contacter un administrateur pour être ajouté.</p>
        </div>
        <div v-else>
            <div v-if="loading" class="loading-state">
                <div class="spinner"></div>
                <p>Chargement des données...</p>
            </div>
            <div v-else-if="error" class="error-state">
                <p>{{ error }}</p>
            </div>
            <div v-else-if="currentSession" class="session-content">
                <div class="session-info">
                    <div class="session-header">
                        <h2>Session du {{ formatDate(currentSession.date) }}</h2>
                        <span v-if="currentSession.name" class="session-name">{{ currentSession.name }}</span>
                        <div class="session-status" :class="{'status-present': attendance && attendance.status !== 1, 'status-absent': attendance && attendance.status === 1, 'status-annule': attendance && attendance.status === 2}">
                            {{ attendance && attendance.status === 2 ? 'Annulé' : (attendance && attendance.status !== 1 ? 'Présent' : 'Absent') }}
                        </div>
                    </div>
                    <div class="session-details">
                        <div class="detail-item">
                            <span class="detail-label">Horaires: </span>
                            <span class="detail-value">{{ formatTime(currentSession.startTime) }} - {{ formatTime(currentSession.endTime) }}</span>
                        </div>
                        <div class="detail-item">
                            <span class="detail-label">Année:</span>
                            <span class="detail-value">{{ currentSession.year }}</span>
                        </div>
                        <div class="detail-item">
                            <span class="detail-label">Salle :</span>
                            <span class="detail-value">{{ currentSession.room }}</span>
                        </div>
                        <div v-if="isDelegate" class="detail-item">
                            <span class="detail-label">Code de validation:</span>
                            <template v-if="currentSession.validationCode">
                                <span class="detail-value code-value" :class="{ blurred: !showCode }">{{ currentSession.validationCode }}</span>
                                <button type="button" class="show-code-btn" @click="handleShowCodeClick">
                                    {{ showCode ? 'Cacher' : 'Voir' }}
                                </button>
                            </template>
                        </div>
                        <div class="detail-item">
                            <span class="detail-label">Email du professeur 1 :</span>
                            <span class="detail-value" v-if="!isDelegate || profEmailEditMode">{{ currentSession.profEmail }}</span>
                            <template v-if="isDelegate">
                                <template v-if="!profEmailEditMode">
                                    <span class="detail-value">{{ currentSession.profEmail }}</span>
                                    <button @click="showEditProfMailPopup = true" class="edit-btn">Modifier</button>
                                    <button @click="showResendProf1MailPopup = true" class="resend-btn">Renvoyer le mail</button>
                                </template>
                                <template v-else>
                                    <input v-model="profEmailInput" type="email" class="edit-input" />
                                    <button @click="saveProfEmail" class="save-btn">Enregistrer</button>
                                    <button @click="cancelProfEmailEdit" class="cancel-btn">Annuler</button>
                                </template>
                            </template>
                        </div>
                        <div v-if="currentSession.profEmail2 || (isDelegate && currentSession.profFirstname2)" class="detail-item">
                            <span class="detail-label">Email du professeur 2 :</span>
                            <span class="detail-value" v-if="!isDelegate || prof2EmailEditMode">{{ currentSession.profEmail2 }}</span>
                            <template v-if="isDelegate">
                                <template v-if="!prof2EmailEditMode">
                                    <span class="detail-value">{{ currentSession.profEmail2 || 'Non défini' }}</span>
                                    <button @click="showEditProf2MailPopup = true" class="edit-btn">Modifier</button>
                                    <button v-if="currentSession.profEmail2" @click="showResendProf2MailPopup = true" class="resend-btn">Renvoyer le mail</button>
                                </template>
                                <template v-else>
                                    <input v-model="prof2EmailInput" type="email" class="edit-input" />
                                    <button @click="saveProf2Email" class="save-btn">Enregistrer</button>
                                    <button @click="cancelProf2EmailEdit" class="cancel-btn">Annuler</button>
                                </template>
                            </template>
                        </div>
                        <div v-if="mailSentMessage" class="mail-sent-message">{{ mailSentMessage }}</div>
                    </div>
                </div>
                <div v-if="attendance && attendance.status === 1 " class="validate-presence">
                    <ValidatePresence @presence-validated="loadData" :hasSignature="hasSignature" />
                </div>
            </div>
            <div v-else class="no-session">
                <p>Aucune session en cours.</p>
            </div>
            <PopUpEditProfMail v-if="showEditProfMailPopup" :value="profEmailInput" @close="showEditProfMailPopup = false" @save="onProfMailPopupSave" />
            <PopUpEditProfMail v-if="showEditProf2MailPopup" :value="prof2EmailInput" @close="showEditProf2MailPopup = false" @save="onProf2MailPopupSave" />
            <PopUpResendProfMail v-if="showResendProf1MailPopup" @close="showResendProf1MailPopup = false" @confirm="confirmResendProf1Mail" />
            <PopUpResendProfMail v-if="showResendProf2MailPopup" @close="showResendProf2MailPopup = false" @confirm="confirmResendProf2Mail" />
            <PopUpResendProfMail v-if="showResendProfMailPopup" @close="showResendProfMailPopup = false" @confirm="confirmResendProfMail" />
            <PopUpShowCode v-if="showCodePopup" @confirm="confirmShowCode" @close="showCodePopup = false" />
        </div>
    </div>
</template>

<script setup>
import { ref, onMounted,watch } from 'vue';
import { useSessionStore } from '../../stores/sessionStore';
import { useAuthStore } from '../../stores/authStore';
import { useStudentsStore } from '../../stores/studentsStore';

import ValidatePresence from '../buttons/ValidatePresence.vue';
import PopUpEditProfMail from '../popups/PopUpEditProfMail.vue';
import PopUpResendProfMail from '../popups/PopUpResendProfMail.vue';
import PopUpShowCode from '../popups/PopUpShowCode.vue';

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

const sessionStore = useSessionStore();
const authStore = useAuthStore();
const studentsStore = useStudentsStore();


const formatDate = (dateString) => {
    const options = { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' };
    return new Date(dateString).toLocaleDateString('fr-FR', options);
};

const formatTime = (timeString) => {
    return timeString.substring(0, 5);
};

const loadData = async () => {
    loading.value = true;
    error.value = null;
    
    try {
        if (!authStore.user || !authStore.user.studentId) {
            error.value = "Veuillez vous connecter pour accéder à cette page.";
            return;
        }
        const studentData = await studentsStore.getStudent(authStore.user.studentId);
        if(studentData){
            if (studentData.signature && studentData.signature !== " ") {
                hasSignature.value = true;
            }
            if(studentData.isDelegate) {
                isDelegate.value = true;
            }
            studentYear.value = studentData.year;
            const session = await sessionStore.getCurrentSession(studentYear.value);
            if (!session) {
                return;
            }
            currentSession.value = session;
            profEmailInput.value = session.profEmail || "";
            prof2EmailInput.value = session.profEmail2 || "";
            
            const at = await sessionStore.getAttendance(authStore.user.studentId, currentSession.value.id);
            attendance.value = at;
            if (!currentSession.value) {
                error.value = "Aucune session en cours pour votre année.";
            }
        }
         else {
            error.value = "Impossible de récupérer vos données d'étudiant.";
        }
    } catch (err) {
        if(err.response && err.response.status !== 404) {
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
        await sessionStore.setProfEmail(currentSession.value.id, profEmailInput.value);
        currentSession.value.profEmail = profEmailInput.value;
    } catch (e) {
        error.value = e.response?.data?.message || "Erreur lors de la modification de l'email du professeur.";
        console.error("Erreur:", e);
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
        console.error("Erreur:", e);
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
        await sessionStore.setProf2Email(currentSession.value.id, prof2EmailInput.value);
        currentSession.value.profEmail2 = prof2EmailInput.value;
    } catch (e) {
        error.value = e.response?.data?.message || "Erreur lors de la modification de l'email du professeur 2.";
        console.error("Erreur:", e);
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
        console.error("Erreur:", e);
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

watch(
    () => authStore.user?.studentId, 
    (newStudentId, oldStudentId) => {
        if (newStudentId !== oldStudentId && newStudentId) {
            loadData();
        }
    }
);
</script>

<style scoped>
.attendance-sheet {
    max-width: 800px;
    margin: 0 auto;
    padding: 20px;
}

.attendance-sheet h1 {
    color: #2c3e50;
    margin-bottom: 20px;
    text-align: center;
    font-size: 1.8rem;
}

.loading-state, .error-state, .no-session {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    padding: 30px;
    border-radius: 8px;
    background-color: #f8f9fa;
    text-align: center;
    margin: 20px 0;
}

.spinner {
    border: 4px solid rgba(0, 0, 0, 0.1);
    border-radius: 50%;
    border-top: 4px solid #2c3e50;
    width: 30px;
    height: 30px;
    animation: spin 1s linear infinite;
    margin-bottom: 15px;
}

@keyframes spin {
    0% { transform: rotate(0deg); }
    100% { transform: rotate(360deg); }
}

.error-state {
    background-color: #fff5f5;
    border-left: 4px solid #e53e3e;
    color: #c53030;
}

.error-icon, .info-icon {
    font-size: 24px;
    margin-bottom: 10px;
}

.no-session {
    background-color: #f0f4ff;
    border-left: 4px solid #4c6ef5;
    color: #3b5bdb;
}


.session-content {
    margin-top: 20px;
}

.session-info {
    background-color: white;
    border-radius: 8px;
    padding: 20px;
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
    margin-bottom: 20px;
}

.session-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 20px;
    padding-bottom: 15px;
    border-bottom: 1px solid #eee;
}

.session-header h2 {
    margin: 0;
    color: #2c3e50;
    font-size: 1.4rem;
}

.session-status {
    padding: 6px 12px;
    border-radius: 20px;
    font-weight: bold;
    font-size: 0.9rem;
}

.status-present {
    background-color: #d4edda;
    color: #155724;
}

.status-absent {
    background-color: #f8d7da;
    color: #721c24;
}

.status-annule {
    background-color: #ececec;
    color: #888;
    font-style: italic;
    text-decoration: line-through;
}

.session-details {
    display: flex;
    flex-direction: column;
    gap: 12px;
}

.detail-item {
    display: flex;
    align-items: baseline;
}

.detail-label {
    font-weight: bold;
    min-width: 140px;
    color: #6c757d;
}

.detail-value {
    flex: 1;
}

.code-value {
    font-family: monospace;
    font-size: 1.1rem;
    color: #2c3e50;
    letter-spacing: 1px;
}

.signature-section {
    margin-top: 20px;
    display: flex;
    align-items: center;
    gap: 10px;
}

.signature-link {
    color: #007bff;
    text-decoration: none;
}

.signature-link:hover {
    text-decoration: underline;
}


.validate-presence {
    margin-top: 20px;
    padding: 20px;
    border-radius: 8px;
    background-color: #fff;
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
    text-align: center;
}

.edit-btn, .resend-btn, .save-btn, .cancel-btn {
    margin-left: 8px;
    padding: 6px 14px;
    border-radius: 4px;
    border: none;
    font-size: 0.95rem;
    cursor: pointer;
    transition: background 0.2s, color 0.2s;
}
.edit-btn {
    background: #f1c40f;
    color: #fff;
}
.edit-btn:hover {
    background: #f39c12;
}
.resend-btn {
    background: #2980b9;
    color: #fff;
}
.resend-btn:hover {
    background: #1c5d8c;
}
.save-btn {
    background: #27ae60;
    color: #fff;
}
.save-btn:hover {
    background: #219150;
}
.cancel-btn {
    background: #e74c3c;
    color: #fff;
}
.cancel-btn:hover {
    background: #c0392b;
}
.mail-sent-message {
    color: #27ae60;
    margin-top: 8px;
    font-size: 0.98rem;
    font-weight: 500;
}
.edit-input {
    padding: 7px 10px;
    border-radius: 4px;
    border: 1px solid #ccc;
    font-size: 1rem;
    margin-right: 8px;
}

.blurred {
    filter: blur(5px);
}

.show-code-btn {
    background: #3498db;
    color: #fff;
    border: none;
    padding: 5px 10px;
    border-radius: 4px;
    cursor: pointer;
    margin-left: 10px;
}

.show-code-btn:hover {
    background: #2980b9;
}

.mail-status {
    margin-left: 10px;
    padding: 4px 8px;
    border-radius: 4px;
    font-size: 0.85rem;
    font-weight: 500;
}

.mail-status.sent {
    background-color: #d4edda;
    color: #155724;
    border: 1px solid #c3e6cb;
}

.mail-status.not-sent {
    background-color: #f8d7da;
    color: #721c24;
    border: 1px solid #f5c6cb;
}

@media (max-width: 768px) {
    .session-header {
        flex-direction: column;
        align-items: flex-start;
        gap: 10px;
    }
    
    .detail-item {
        flex-direction: column;
    }
    
    .detail-label {
        margin-bottom: 5px;
    }
}
</style>