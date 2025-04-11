<template>
  <div class="sessions-page">
    <h1>Mes Sessions</h1>
    
    <div class="filter-container">
      <select v-model="selectedYear" @change="loadSessions" class="year-filter">
        <option value="">Toutes les années</option>
        <option value="3A">3A</option>
        <option value="4A">4A</option>
        <option value="5A">5A</option>
      </select>
      
      <button @click="showCreateSessionForm = !showCreateSessionForm" class="create-button">
        {{ showCreateSessionForm ? 'Annuler' : 'Créer une session' }}
      </button>
    </div>
    
    <!-- Formulaire de création de session -->
    <div v-if="showCreateSessionForm" class="session-form-container">
      <h2>Nouvelle Session</h2>
      <form @submit.prevent="createNewSession" class="session-form">
        <div class="form-group">
          <label for="session-date">Date:</label>
          <input type="date" id="session-date" v-model="newSession.date" required class="form-control">
        </div>
        
        <div class="form-group">
          <label for="session-start">Heure de début:</label>
          <input type="time" id="session-start" v-model="newSession.startTime" required class="form-control">
        </div>
        
        <div class="form-group">
          <label for="session-end">Heure de fin:</label>
          <input type="time" id="session-end" v-model="newSession.endTime" required class="form-control">
        </div>
        
        <div class="form-group">
          <label for="session-year">Année:</label>
          <select id="session-year" v-model="newSession.year" required class="form-control" @change="loadStudentsByYear">
            <option value="">Sélectionner une année</option>
            <option value="3A">3A</option>
            <option value="4A">4A</option>
            <option value="5A">5A</option>
          </select>
        </div>
        
        <div v-if="studentLoading" class="loading-info">
          Chargement des étudiants...
        </div>
        
        <div v-else-if="students && students.length > 0" class="student-count-info">
          {{ students.length }} étudiants seront ajoutés à cette session.
        </div>
        
        <div v-else-if="newSession.year && !studentLoading" class="student-count-info warning">
          Aucun étudiant trouvé pour l'année {{ newSession.year }}.
        </div>
        
        <div class="form-actions">
          <button type="submit" class="submit-button" :disabled="sessionStore.loading || studentLoading">Créer la session</button>
        </div>
      </form>
    </div>
    
    <!-- Message de création réussie -->
    <div v-if="showSuccessMessage" class="success-message">
      Session créée avec succès!
    </div>
    
    <!-- Sessions existantes -->
    <div v-if="sessionStore.loading" class="loading-state">
      Chargement des sessions...
    </div>
    
    <div v-else-if="sessionStore.error" class="error-state">
      <p>{{ sessionStore.error }}</p>
      <button @click="loadSessions" class="retry-button">Réessayer</button>
    </div>
    
    <div v-else-if="sessions.length === 0" class="empty-state">
      <p>Aucune session trouvée.</p>
    </div>
    
    <div v-else class="sessions-list">
      <div v-for="session in sessions" :key="session.id" class="session-card">
        <div class="session-header">
          <h3>Session du {{ formatDate(session.date) }}</h3>
          <span class="session-year">{{ session.year }}</span>
        </div>
        <div class="session-details">
          <p><strong>Horaires:</strong> {{ formatTime(session.startTime) }} - {{ formatTime(session.endTime) }}</p>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import { defineComponent, ref, computed, onMounted, reactive } from 'vue';
import { useSessionStore } from '../../stores/sessionStore';
import { useStudentsStore } from '../../stores/studentsStore';
import { useAuthStore } from '../../stores/authStore';
import axios from 'axios';

export default defineComponent({
  name: 'StudentsSessionPage',
  setup() {
    const sessionStore = useSessionStore();
    const authStore = useAuthStore();
    const studentsStore = useStudentsStore();
    const selectedYear = ref('');
    const showCreateSessionForm = ref(false);
    const showSuccessMessage = ref(false);
    const studentLoading = ref(false);
    const students = ref([]);
    
    // Initialiser le nouvel objet session
    const newSession = reactive({
      date: '',
      startTime: '',
      endTime: '',
      year: ''
    });
    
    const sessions = computed(() => {
      return sessionStore.sessions;
    });
    
    const loadSessions = async () => {
      if (selectedYear.value) {
        await sessionStore.fetchSessionsByYear(selectedYear.value);
      } else {
        await sessionStore.fetchAllSessions();
      }
    };
    
    // Charger les étudiants par année
    const loadStudentsByYear = async () => {
      if (!newSession.year) {
        students.value = [];
        return;
      }
      
      studentLoading.value = true;
      console.log('Chargement des étudiants pour l\'année:', newSession.year);
      studentsStore.fetchStudents(newSession.year)
        .then(response => {
          students.value = response;
        })
        .catch(error => {
          console.error('Erreur lors du chargement des étudiants:', error);
        })
        .finally(() => {
          studentLoading.value = false;
        });

    };
    
    // Créer une nouvelle session
    const createNewSession = async () => {
      if (!newSession.date || !newSession.startTime || !newSession.endTime || !newSession.year) {
        return;
      }
      
      let validationCode ='';
      for (let i = 0; i < 4; i++) {
        validationCode += Math.floor(Math.random() * 10).toString();
      }
      // Formater les données pour l'API
      const sessionData = {
        date: newSession.date,
        startTime: newSession.startTime,
        endTime: newSession.endTime,
        year: newSession.year,
        validationCode: validationCode
      };
      
      try {
        // Créer la session
        const createdSession = await sessionStore.createSession(sessionData);
        
                // Si des étudiants existent, les ajouter à la session
        if (createdSession && students.value.length > 0) {
          console.log('Ajout des étudiants à la session:', createdSession.id, students.value);
          try {
            // Modifier la façon dont nous préparons les données des étudiants pour l'API
            await sessionStore.addStudentsToSessionByNumber(createdSession.id, students.value);
          } catch (error) {
            console.error("Erreur lors de l'ajout des étudiants à la session:", error);
          }
        }
        
        // Réinitialiser le formulaire
        newSession.date = '';
        newSession.startTime = '';
        newSession.endTime = '';
        newSession.year = '';
        students.value = [];
        showCreateSessionForm.value = false;
        
        // Afficher le message de succès et le cacher après 3 secondes
        showSuccessMessage.value = true;
        setTimeout(() => {
          showSuccessMessage.value = false;
        }, 3000);
        
        // Recharger les sessions
        loadSessions();
        
      } catch (error) {
        console.error('Erreur lors de la création de la session:', error);
      }
    };
    
    const formatDate = (dateString) => {
      const options = { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' };
      return new Date(dateString).toLocaleDateString('fr-FR', options);
    };
    
    const formatTime = (timeString) => {
      return timeString.substring(0, 5);
    };
    
    onMounted(() => {
      loadSessions();
    });
    
    return {
      sessionStore,
      sessions,
      selectedYear,
      loadSessions,
      formatDate,
      formatTime,
      showCreateSessionForm,
      newSession,
      createNewSession,
      loadStudentsByYear,
      students,
      studentLoading,
      showSuccessMessage
    };
  }
});
</script>

<style scoped>
.sessions-page {
  width: 100%;
  max-width: 1200px;
  margin: 0 auto;
  padding: 20px;
}

.filter-container {
  margin: 20px 0;
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 10px;
}

.year-filter {
  padding: 8px 12px;
  border-radius: 4px;
  border: 1px solid #ccc;
  font-size: 16px;
  min-width: 150px;
}

.loading-state, .error-state, .empty-state {
  padding: 40px;
  text-align: center;
  background-color: #f9f9f9;
  border-radius: 8px;
  margin: 20px 0;
}

.error-state {
  border: 1px solid #e74c3c;
  color: #e74c3c;
}

.retry-button {
  background-color: #3498db;
  color: white;
  border: none;
  padding: 8px 16px;
  border-radius: 4px;
  margin-top: 10px;
  cursor: pointer;
}

.retry-button:hover {
  background-color: #2980b9;
}

.sessions-list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(350px, 1fr));
  gap: 20px;
}

.session-card {
  background-color: #fff;
  border-radius: 8px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
  overflow: hidden;
  transition: transform 0.3s, box-shadow 0.3s;
}

.session-card:hover {
  transform: translateY(-5px);
  box-shadow: 0 5px 15px rgba(0, 0, 0, 0.1);
}

.session-header {
  background-color: #3498db;
  color: white;
  padding: 15px;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.session-header h3 {
  margin: 0;
  font-weight: 500;
}

.session-year {
  background-color: rgba(255, 255, 255, 0.2);
  padding: 3px 8px;
  border-radius: 4px;
  font-size: 0.9rem;
}

.session-details {
  padding: 15px;
}

.session-details p {
  margin: 10px 0;
}

.create-button {
  background-color: #27ae60;
  color: white;
  border: none;
  padding: 8px 16px;
  border-radius: 4px;
  cursor: pointer;
  font-weight: 500;
  transition: background-color 0.3s;
}

.create-button:hover {
  background-color: #219653;
}

.session-form-container {
  background-color: #fff;
  border-radius: 8px;
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
  padding: 20px;
  margin-bottom: 30px;
}

.session-form-container h2 {
  margin-bottom: 20px;
  color: #2c3e50;
  font-size: 1.4rem;
}

.session-form {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
  gap: 20px;
}

.form-group {
  display: flex;
  flex-direction: column;
}

.form-group label {
  margin-bottom: 5px;
  font-weight: 500;
}

.form-control {
  padding: 10px;
  border: 1px solid #ddd;
  border-radius: 4px;
  font-size: 16px;
}

.form-actions {
  grid-column: 1 / -1;
  margin-top: 10px;
}

.submit-button {
  background-color: #3498db;
  color: white;
  border: none;
  padding: 12px 20px;
  border-radius: 4px;
  cursor: pointer;
  font-weight: 500;
  width: 100%;
  transition: background-color 0.3s;
}

.submit-button:hover {
  background-color: #2980b9;
}

.submit-button:disabled {
  background-color: #95a5a6;
  cursor: not-allowed;
}

.loading-info, .student-count-info {
  grid-column: 1 / -1;
  padding: 10px;
  background-color: #f8f9fa;
  border-radius: 4px;
  text-align: center;
}

.student-count-info.warning {
  background-color: #ffe6e6;
  color: #c0392b;
}

.success-message {
  background-color: #d4edda;
  color: #155724;
  padding: 15px;
  border-radius: 4px;
  text-align: center;
  margin-bottom: 20px;
}

@media (max-width: 768px) {
  .sessions-list {
    grid-template-columns: 1fr;
  }
}
</style>