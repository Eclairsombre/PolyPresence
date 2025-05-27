<template>
  <div class="sessions-page">
    <h1>Mes Sessions</h1>
    
    <div class="filter-container">
      <div class="filter-group">
        <select v-model="selectedYear" class="year-filter">
          <option value="">Toutes les années</option>
          <option value="3A">3A</option>
          <option value="4A">4A</option>
          <option value="5A">5A</option>
        </select>
        
        <div class="date-filter">
          <div class="date-input-group">
            <label for="startDate">Du:</label>
            <input 
              type="date" 
              id="startDate" 
              v-model="filters.startDate" 
              class="date-input"
            >
          </div>
          
          <div class="date-input-group">
            <label for="endDate">Au:</label>
            <input 
              type="date" 
              id="endDate" 
              v-model="filters.endDate" 
              class="date-input"
              :min="filters.startDate"
            >
          </div>
          
          <button @click="applyFilters" class="filter-button">Filtrer</button>
          <button @click="clearFilters" class="clear-filter-button">Réinitialiser</button>
        </div>
      </div>
      
      <button @click="showCreateSessionModal = true" class="create-button">
        Créer une session
      </button>
      <ExportSessionsPdf :sessions="sessions" :selectedYear="selectedYear" />

    </div>
    
    <PopUpCreateSession v-if="showCreateSessionModal" @close="showCreateSessionModal = false" @sessionCreated="handleSessionCreated" />
    
    <div v-if="showSuccessMessage" class="success-message">
      Session créée avec succès!
    </div>
    
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
    
    <div v-else>
      
      <div class="sessions-list">
        <div v-for="session in sessions" :key="session.id" class="session-card">
          <div class="session-header">
            <h3>Session du {{ formatDate(session.date) }}</h3>
            <span class="session-year">{{ session.year }}</span>
          </div>
          <div class="session-details">
            <p v-if="session.name"><strong>Nom :</strong> {{ session.name }}</p>
            <p><strong>Horaires:</strong> {{ formatTime(session.startTime) }} - {{ formatTime(session.endTime) }}</p>
            <p v-if="session.room"><strong>Salle :</strong> {{ session.room }}</p>
            <div class="session-actions">
              <router-link :to="`/sessions/${session.id}`" class="view-attendance-btn">
                Voir les présences
              </router-link>
              <button v-if="session.name && session.name.toLowerCase().includes('travail personnel')" class="view-attendance-btn" @click="openEditSessionModal(session)" style="min-width: 90px;">
                Signer
              </button>
            </div>
          </div>
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
import { defineComponent, ref, computed, onMounted, reactive } from 'vue';
import { useSessionStore } from '../../stores/sessionStore';
import { useStudentsStore } from '../../stores/studentsStore';
import ExportSessionsPdf from '../exports/ExportSessionsPdf.vue';
import PopUpCreateSession from '../popups/PopUpCreateSession.vue';
import PopUpSignSession from '../popups/PopUpSignSession.vue';

export default defineComponent({
  name: 'StudentsSessionPage',
  components: {
    ExportSessionsPdf,
    PopUpCreateSession,
    PopUpSignSession: PopUpSignSession
  },
  setup() {
    const sessionStore = useSessionStore();
    const studentsStore = useStudentsStore();
    const selectedYear = ref('');
    const showCreateSessionForm = ref(false);
    const showSuccessMessage = ref(false);
    const studentLoading = ref(false);
    const students = ref([]);
    const isFiltering = ref(false);
    const showCreateSessionModal = ref(false);
    const showEditSessionModal = ref(false);
    const selectedSession = ref(null);
    const today = new Date().toISOString().split('T')[0]; // Format YYYY-MM-DD
    
    const filters = reactive({
      startDate: today,
      endDate: '',
    });
    
    const newSession = reactive({
      date: '',
      startTime: '',
      endTime: '',
      year: '',
      profName: '',
      profFirstname: '',
      profEmail: ''
    });
    
    const sessions = computed(() => {
      return sessionStore.sessions.slice().sort((a, b) => {
      const dateComparison = new Date(a.date) - new Date(b.date);
      if (dateComparison !== 0) {
        return dateComparison;
      }
      return a.startTime.localeCompare(b.startTime);
      });
    });
    
    
    const loadSessions = async () => {
      isFiltering.value = true;
      await sessionStore.fetchSessionsByFilters({
        year: selectedYear.value,
        startDate: filters.startDate,
        endDate: filters.endDate
      });
      isFiltering.value = false;
    };
    
    const applyFilters = async () => {
      await loadSessions();
    };
      const clearFilters = () => {
      filters.startDate = new Date().toISOString().split('T')[0]; // Réinitialise à la date d'aujourd'hui
      filters.endDate = '';
      selectedYear.value = '';
      loadSessions();
    };
    
    const loadStudentsByYear = async () => {
      if (!newSession.year) {
        students.value = [];
        return;
      }
      
      studentLoading.value = true;
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
    
    const createNewSession = async () => {
      if (!newSession.date || !newSession.startTime || !newSession.endTime || !newSession.year || !newSession.profName || !newSession.profFirstname || !newSession.profEmail) {
        return;
      }
      
      let validationCode ='';
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
        profEmail: newSession.profEmail
      };
      
      try {
        const createdSession = await sessionStore.createSession(sessionData);
        
        if (createdSession && students.value.length > 0) {
          try {
            await sessionStore.addStudentsToSessionByNumber(createdSession.id, students.value);
          } catch (error) {
            console.error("Erreur lors de l'ajout des étudiants à la session:", error);
          }
        }
        
        newSession.date = '';
        newSession.startTime = '';
        newSession.endTime = '';
        newSession.year = '';
        newSession.profName = '';
        newSession.profFirstname = '';
        newSession.profEmail = '';
        students.value = [];
        showCreateSessionForm.value = false;
        
        showSuccessMessage.value = true;
        setTimeout(() => {
          showSuccessMessage.value = false;
        }, 3000);
        
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
      if (!timeString) return '';
      return timeString.substring(0, 5);
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
      handleSessionUpdated
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
  align-items: flex-start;
  flex-wrap: wrap;
  gap: 10px;
}

.filter-group {
  display: flex;
  flex-direction: column;
  gap: 10px;
  flex-grow: 1;
  max-width: 600px;
}

.date-filter {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  align-items: flex-end;
}

.date-input-group {
  display: flex;
  flex-direction: column;
  gap: 5px;
}

.date-input-group label {
  font-size: 0.9rem;
  color: #555;
}

.date-input {
  padding: 8px 12px;
  border-radius: 4px;
  border: 1px solid #ccc;
  font-size: 14px;
}

.filter-button {
  background-color: #3498db;
  color: white;
  border: none;
  padding: 8px 16px;
  border-radius: 4px;
  cursor: pointer;
  font-weight: 500;
  height: 37px;
}

.filter-button:hover {
  background-color: #2980b9;
}

.clear-filter-button {
  background-color: #f1f1f1;
  color: #333;
  border: 1px solid #ddd;
  padding: 8px 16px;
  border-radius: 4px;
  cursor: pointer;
  height: 37px;
}

.clear-filter-button:hover {
  background-color: #e6e6e6;
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

.session-actions {
  margin-top: 10px;
}

.view-attendance-btn {
  background-color: #3498db;
  color: white;
  padding: 8px 12px;
  border-radius: 4px;
  text-decoration: none;
  transition: background-color 0.3s;
  margin-left: 10px;
  border: none;
  cursor: pointer;
  font-weight: 500;
}

.view-attendance-btn:hover {
  background-color: #2980b9;
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


.session-form-container h2 {
  margin-bottom: 20px;
  color: #2c3e50;
  font-size: 1.4rem;
}

.form-group label {
  margin-bottom: 5px;
  font-weight: 500;
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

@media (max-width: 600px) {
  .sessions-list {
    grid-template-columns: 1fr;
    gap: 10px;
  }
  .session-card {
    font-size: 0.98em;
    padding: 0;
  }
  .session-header, .session-details {
    padding: 8px;
  }

  .form-group label{
    font-size: 0.98em;
  }

}

.view-attendance-btn {
  background-color: #3498db;
  color: white;
  padding: 8px 12px;
  border-radius: 4px;
  text-decoration: none;
  transition: background-color 0.3s;
  margin-left: 10px;
  border: none;
  cursor: pointer;
  font-weight: 500;
}
.view-attendance-btn:hover {
  background-color: #2980b9;
}
</style>

