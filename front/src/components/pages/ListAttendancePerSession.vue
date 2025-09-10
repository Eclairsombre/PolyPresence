<template>
  <div class="attendance-page">
    <div v-if="loading" class="loading-state">Chargement des données...</div>
    
    <div v-else-if="error" class="error-state">
      <p>{{ error }}</p>
      <button @click="loadSessionData" class="retry-button">Réessayer</button>
    </div>
    
    <div v-else class="attendance-container">
      <div class="header-section">
        <h1>Liste de présence</h1>
        <div class="school-info">
          <p>Etablissement de formation : UCBL1 - EPUL</p>
          <p>Diplôme : Ingénieur de l'EPUL - spécialité Informatique - apprentissage</p>
        </div>
        <div class="session-info">
          <h2>{{ formatDate(session?.date) }} - {{ session?.year }}</h2>
          <p>{{ formatTime(session?.startTime) }} - {{ formatTime(session?.endTime) }}</p>
        </div>
        <div class="prof-info" v-if="session">
          <p v-if="session.name" class="session-name"><strong>Nom de la session :</strong> {{ session.name }}</p>
          <p v-if="session.room" class="session-room"><strong>Salle :</strong> {{ session.room }}</p>
          
          <div class="professors-section">
            <h3>Encadrement pédagogique</h3>
            
            <div class="professor-card" v-if="(session.profFirstname && session.profFirstname.trim() !== '') || (session.profName && session.profName.trim() !== '')">
              <div class="professor-details">
                <span class="professor-name">{{ session.profFirstname }} {{ session.profName }}</span>
                <span v-if="session.profEmail" class="professor-email">({{ session.profEmail }})</span>
              </div>
              <div class="professor-signature" v-if="session.profSignature">
                <span class="signature-label">Signature :</span>
                <img :src="session.profSignature" alt="Signature du professeur 1" class="signature-image" />
              </div>
              <div class="professor-signature" v-else>
                <span class="signature-label">Signature : <em>Non signée</em></span>
              </div>
            </div>
            
            <div class="professor-card" v-if="(session.profFirstname2 && session.profFirstname2.trim() !== '') || (session.profName2 && session.profName2.trim() !== '')">
              <div class="professor-details">
                <span class="professor-name">{{ session.profFirstname2 }} {{ session.profName2 }}</span>
                <span v-if="session.profEmail2" class="professor-email">({{ session.profEmail2 }})</span>
              </div>
              <div class="professor-signature" v-if="session.profSignature2">
                <span class="signature-label">Signature :</span>
                <img :src="session.profSignature2" alt="Signature du professeur 2" class="signature-image" />
              </div>
              <div class="professor-signature" v-else>
                <span class="signature-label">Signature : <em>Non signée</em></span>
              </div>
            </div>
          </div>
        </div>
        <div class="actions">
          <button class="back-button" @click="goBack">Retour aux sessions</button>
          <button class="export-button" @click="exportToPDF" :disabled="exporting">
            {{ exporting ? 'Génération PDF...' : 'Exporter en PDF' }}
          </button>
        </div>
        
      </div>
      
      <div class="attendance-table-wrapper">
        <table class="attendance-table">
          <thead>
            <tr>
              <th class="number-column">N°</th>
              <th class="name-column">Nom</th>
              <th class="firstname-column">Prénom</th>
              <th class="status-column">Présent/Absent</th>
              <th class="signature-column">Signature</th>
              <th class="comment-column">Retard/Commentaire</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(student, index) in students" :key="student.id">
              <td>{{ index + 1 }}</td>
              <td>{{ student.name }}</td>
              <td>{{ student.firstname }}</td>
              <td class="status-cell">
                <span :class="{'status-present': student.status === 'Present', 'status-absent': student.status === 'Absent'}">
                  {{ student.status === 'Present' ? 'P' : 'A' }}
                </span>
              </td>
              <td class="signature-cell">
                <div v-if="student.status === 'Present'">
                  <SignatureDisplay 
                    :signatureData="student.signature" 
                    :inAttendanceList="true"
                  />
                </div>
              </td>
              <td class="comment-cell">
                <div class="comment-content" :title="student.comment">
                  {{student.comment}}
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>

<script>
import {defineComponent, onMounted, ref} from 'vue';
import {useRoute, useRouter} from 'vue-router';
import {useSessionStore} from '../../stores/sessionStore';
import {useStudentsStore} from '../../stores/studentsStore';
import SignatureDisplay from '../signature/SignatureDisplay.vue';
import {useMailPreferencesStore} from "../../stores/mailPreferencesStore.js";


export default defineComponent({
  name: 'ListAttendancePerSession',
  components: {
    SignatureDisplay
  },
  
  setup() {
    const route = useRoute();
    const router = useRouter();
    const sessionStore = useSessionStore();
    const studentsStore = useStudentsStore();
    const mailStore = useMailPreferencesStore()
    
    const session = ref(null);
    const students = ref([]);
    const loading = ref(true);
    const error = ref(null);
    const exporting = ref(false);
    
    const loadSessionData = async () => {
      loading.value = true;
      error.value = null;
      try {
        const sessionId = route.params.id;
        session.value = await sessionStore.fetchSessionById(sessionId);
        const attendances = await sessionStore.getSessionAttendances(sessionId);
        students.value = attendances.map((student) => ({
          id: student.item1.id,
          name: student.item1.name,
          firstname: student.item1.firstname,
          status: student.item2 === 0 ? 'Present' : 'Absent',
          signature: student.item1.signature || '',
          comment: student.item1.comment || ''
        })).sort((a,b) => a.name.localeCompare(b.name));


      } catch (err) {
        console.error("Erreur lors du chargement des données:", err);
        error.value = "Impossible de charger les données de présence.";
      } finally {
        loading.value = false;
      }
    };
    
    const handleSignatureSaved = async ({ studentId, sessionId, signatureData }) => {
      try {
        const studentData = await studentsStore.getStudentById(studentId);
        if (!studentData) {
          console.error("Impossible de trouver l'étudiant");
          return;
        }

        await sessionStore.saveSignature(studentData.studentNumber, sessionId, signatureData);
        
        const studentIndex = students.value.findIndex(s => s.id === studentId);
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
        path: '/sessions',
        query: {
          year,
          startDate,
          endDate
        }
      });
    };
    
    const formatDate = (dateString) => {
      if (!dateString) return '';
      const options = { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' };
      return new Date(dateString).toLocaleDateString('fr-FR', options);
    };
    
    const formatTime = (timeString) => {
      if (!timeString) return '';
      return timeString.substring(0, 5);
    };

    const exportToPDF = async () => {
      if (!session.value) return;
      exporting.value = true;

      try {
          await mailStore.getSessionPdf(session)
      } finally {
        exporting.value = false;
      }
    };

    onMounted(() => {
      loadSessionData();
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
      route
    };
  }
});
</script>

<style scoped>
.attendance-page {
  width: 100%;
  max-width: 1200px;
  margin: 0 auto;
  padding: 20px;
}

.signature-cell {
  vertical-align: top;
  padding: 10px;
}

.loading-state, .error-state {
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

.header-section {
  margin-bottom: 30px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.school-info {
  background-color: #eaf6fb;
  border-left: 4px solid #3498db;
  padding: 12px 18px;
  border-radius: 6px;
  margin-bottom: 10px;
  font-size: 1.08em;
  color: #2c3e50;
  box-shadow: 0 2px 6px rgba(52, 152, 219, 0.07);
}
.school-info p {
  margin: 0 0 4px 0;
  font-weight: 500;
}

.session-info {
  background-color: #f8f9fa;
  padding: 15px;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.05);
}

.session-info h2 {
  margin: 0 0 10px 0;
  color: #2c3e50;
}

.prof-info {
  margin-top: 20px;
}

.session-name, .session-room {
  margin: 10px 0;
  font-size: 1.1em;
  color: #2c3e50;
}

.professors-section {
  margin-top: 20px;
}

.professors-section h3 {
  margin: 0 0 15px 0;
  color: #2c3e50;
  font-size: 1.2em;
  border-bottom: 2px solid #3498db;
  padding-bottom: 8px;
}

.professor-card {
  background-color: #f8f9fa;
  border: 1px solid #e9ecef;
  border-radius: 8px;
  padding: 15px;
  margin-bottom: 15px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.05);
}

.professor-header {
  margin-bottom: 10px;
  color: #2c3e50;
  font-size: 1.05em;
}

.professor-details {
  margin-bottom: 10px;
}

.professor-name {
  font-weight: 600;
  color: #2c3e50;
  margin-right: 10px;
}

.professor-email {
  color: #6c757d;
  font-style: italic;
}

.professor-signature {
  display: flex;
  align-items: center;
  gap: 10px;
}

.signature-label {
  font-weight: 500;
  color: #495057;
}

.signature-image {
  max-height: 50px;
  border: 1px solid #dee2e6;
  border-radius: 4px;
  background-color: white;
  padding: 2px;
}

.actions {
  display: flex;
  justify-content: flex-start;
  margin-top: 15px;
  gap: 10px;
}

.back-button {
  background-color: #3498db;
  color: white;
  border: none;
  padding: 10px 20px;
  border-radius: 4px;
  cursor: pointer;
  transition: background-color 0.3s;
  font-weight: 500;
}

.back-button:hover {
  background-color: #2980b9;
}

.export-button {
  background-color: #34495e;
  color: white;
  border: none;
  padding: 10px 20px;
  border-radius: 4px;
  cursor: pointer;
  font-weight: 500;
  transition: background-color 0.3s;
  display: flex;
  align-items: center;
  gap: 8px;
}

.export-button:disabled {
  background-color: #95a5a6;
  cursor: not-allowed;
}

.export-button:hover:not(:disabled) {
  background-color: #2c3e50;
}

.attendance-table-wrapper {
  overflow-x: auto;
  box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
  border-radius: 8px;
}

.attendance-table {
  width: 100%;
  border-collapse: collapse;
  background-color: white;
}

.attendance-table th,
.attendance-table td {
  padding: 12px 15px;
  text-align: left;
  border-bottom: 1px solid #e0e0e0;
}

.attendance-table th {
  background-color: #f1f1f1;
  font-weight: 600;
  color: #333;
}

.attendance-table tr:last-child td {
  border-bottom: none;
}

.attendance-table tr:hover {
  background-color: #f9f9f9;
}

.number-column {
  width: 50px;
}

.name-column, .firstname-column {
  width: 25%;
}

.status-column {
  width: 150px;
}

.signature-column {
  width: 20%;
}

.comment-column {
  width: 20%;
  background-color: #f7fbfd;
}

.comment-cell {
  background-color: #f7fbfd;
  vertical-align: top;
  padding: 10px;
}

.comment-content {
  font-style: italic;
  color: #576574;
  background-color: #fff;
  border-left: 3px solid #3498db;
  padding: 8px;
  border-radius: 3px;
  max-height: 100px;
  overflow-y: auto;
  font-size: 0.95em;
  line-height: 1.4;
  word-break: break-word;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
  transition: background-color 0.2s;
}

.comment-content:hover {
  background-color: #f8f9fa;
}

.comment-content:empty {
  display: none;
  background-color: transparent;
  box-shadow: none;
  border-left: none;
  padding: 0;
}

.status-cell {
  text-align: center;
}

.status-present {
  color: #27ae60;
  font-weight: bold;
}

.status-absent {
  color: #e74c3c;
  font-weight: bold;
}


@media (max-width: 768px) {
  .attendance-table {
    min-width: 600px;
  }
  
  .comment-content {
    font-size: 0.9em;
    max-height: 60px;
  }
}

@media (max-width: 600px) {
  .attendance-page {
    padding: 4px;
  }
  .header-section {
    gap: 4px;
  }
  .attendance-table-wrapper {
    box-shadow: none;
    border-radius: 0;
  }
  .attendance-table {
    min-width: 400px;
    font-size: 0.95em;
  }
  .attendance-table th, .attendance-table td {
    padding: 6px 4px;
  }
  .back-button, .export-button {
    padding: 8px 8px;
    font-size: 0.95em;
    width: 100%;
  }
  .actions {
    flex-direction: column;
    gap: 6px;
  }
  
  .comment-column, .comment-cell {
    min-width: 100px;
  }
  
  .comment-content {
    padding: 4px;
    font-size: 0.85em;
    max-height: 50px;
  }
}
</style>

