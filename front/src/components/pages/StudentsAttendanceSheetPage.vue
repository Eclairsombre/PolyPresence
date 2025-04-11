<template>
    <div class="attendance-sheet">
        <h1>Feuille de présence</h1>
        <div v-if="loading">Chargement des données...</div>
        <div v-else-if="error">{{ error }}</div>
        <div v-else-if="currentSession">
            <div class="session-info">
                <h2>Session du {{ formatDate(currentSession.date) }}</h2>
                <p><strong>Horaires:</strong> {{ formatTime(currentSession.startTime) }} - {{ formatTime(currentSession.endTime) }}</p>
                <p><strong>Année:</strong> {{ currentSession.year }}</p>
                <p><strong>Code de validation:</strong> {{ currentSession.validationCode }}</p>
                <p><strong>Présence:</strong> {{ attendance.status !== 1 ? 'Présent' : 'Absent' }}</p>
            </div>
            <div v-if="attendance.status === 1" class="validate-presence">
                <ValidatePresence @presence-validated="loadData" />
            </div>
        </div>
        <div v-else>
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

// Initialiser les variables réactives
const loading = ref(true);
const error = ref(null);
const currentSession = ref(null);
const studentYear = ref(null);
const attendance = ref(null);

// Initialiser les stores
const sessionStore = useSessionStore();
const authStore = useAuthStore();
const studentsStore = useStudentsStore();

// Fonctions pour formater les dates et heures
const formatDate = (dateString) => {
    const options = { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' };
    return new Date(dateString).toLocaleDateString('fr-FR', options);
};

const formatTime = (timeString) => {
    return timeString.substring(0, 5);
};

// Fonction pour charger les données
const loadData = async () => {
    loading.value = true;
    error.value = null;
    
    try {
        // Vérifier si l'utilisateur est connecté
        if (!authStore.user || !authStore.user.studentId) {
            error.value = "Veuillez vous connecter pour accéder à cette page.";
            return;
        }
        console.log("ID de l'étudiant connecté:", authStore.user.studentId);
        // Récupérer l'année de l'étudiant connecté
        const studentData = await studentsStore.getStudent(authStore.user.studentId);
        console.log("Données de l'étudiant:", studentData.student);
        
        if (studentData) {
            studentYear.value = studentData.student.year;
            console.log("Année de l'étudiant:", studentYear.value);
            // Récupérer la session actuelle pour l'année de l'étudiant
            const session = await sessionStore.getCurrentSession(studentYear.value);
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
        error.value = "Une erreur est survenue lors du chargement des données.";
    } finally {
        loading.value = false;
    }
};

// Charger les données au montage du composant
onMounted(loadData);
</script>

<style scoped>
.attendance-sheet {
    max-width: 800px;
    margin: 0 auto;
    padding: 20px;
}

.session-info {
    background-color: #f8f9fa;
    border-radius: 8px;
    padding: 20px;
    margin-top: 20px;
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.session-info h2 {
    margin-top: 0;
    color: #2c3e50;
}

.validate-presence {
    margin-top: 30px;
}
</style>