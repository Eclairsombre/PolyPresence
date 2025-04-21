<template>
    <div class="attendance-sheet">
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
                    <div class="session-status" :class="{'status-present': attendance && attendance.status !== 1, 'status-absent': attendance && attendance.status === 1}">
                        {{ attendance && attendance.status !== 1 ? 'Présent' : 'Absent' }}
                    </div>
                </div>
                <div class="session-details">
                    <div class="detail-item">
                        <span class="detail-label">Horaires:</span>
                        <span class="detail-value">{{ formatTime(currentSession.startTime) }} - {{ formatTime(currentSession.endTime) }}</span>
                    </div>
                    <div class="detail-item">
                        <span class="detail-label">Année:</span>
                        <span class="detail-value">{{ currentSession.year }}</span>
                    </div>
                    <div class="detail-item">
                        <span class="detail-label">Code de validation:</span>
                        <span class="detail-value code-value">{{ currentSession.validationCode }}</span>
                    </div>
                </div>
            </div>
            <div v-if="attendance && attendance.status === 1" class="validate-presence">
                <ValidatePresence @presence-validated="loadData" :hasSignature="hasSignature" />
            </div>
        </div>
        <div v-else class="no-session">
            <p>Aucune session en cours.</p>
        </div>
    </div>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import { useSessionStore } from '../../stores/sessionStore';
import { useAuthStore } from '../../stores/authStore';
import { useStudentsStore } from '../../stores/studentsStore';

import ValidatePresence from '../buttons/ValidatePresence.vue';
import SignatureCreator from '../signature/SignatureCreator.vue';

const loading = ref(true);
const error = ref(null);
const currentSession = ref(null);
const studentYear = ref(null);
const attendance = ref(null);
const hasSignature = ref(false);

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
        console.log("ID de l'étudiant connecté:", authStore.user.studentId);
        const studentData = await studentsStore.getStudent(authStore.user.studentId);
        console.log("Données de l'étudiant:", studentData.student);
        
        if (studentData) {
            if (studentData.student.signature && studentData.student.signature !== " ") {
                hasSignature.value = true;
            }
            console.log("Signature de l'étudiant:", hasSignature.value);
            studentYear.value = studentData.student.year;
            console.log("Année de l'étudiant:", studentYear.value);
            const session = await sessionStore.getCurrentSession(studentYear.value);
            if (!session) {
                return;
            }
            currentSession.value = session;
            console.log("Session actuelle:", currentSession.value);

            const at = await sessionStore.getAttendance(authStore.user.studentId, currentSession.value.id);
            attendance.value = at;
            if (!currentSession.value) {
                error.value = "Aucune session en cours pour votre année.";
            }
        } else {
            error.value = "Impossible de récupérer vos données d'étudiant.";
        }
    } catch (err) {
        console.error("Erreur lors du chargement des données:", err);
        if(err.response && err.response.status !== 404) {
            error.value = "Une erreur est survenue lors du chargement des données.";
        } 
    } finally {
        loading.value = false;
    }
};

const validateSignature =  () => {
    hasSignature.value = true;
};

onMounted(loadData);
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

/* Informations de session */
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

/* Zone de validation de présence */
.validate-presence {
    margin-top: 20px;
    padding: 20px;
    border-radius: 8px;
    background-color: #fff;
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
    text-align: center;
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