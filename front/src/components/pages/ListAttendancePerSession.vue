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
        <div class="session-info">
          <h2>{{ formatDate(session?.date) }} - {{ session?.year }}</h2>
          <p>{{ formatTime(session?.startTime) }} - {{ formatTime(session?.endTime) }}</p>
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
                  />
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
import { defineComponent, ref, onMounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useSessionStore } from '../../stores/sessionStore';
import { useStudentsStore } from '../../stores/studentsStore';
import axios from 'axios';
import SignatureDisplay from '../signature/SignatureDisplay.vue';
import html2pdf from 'html2pdf.js';


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
        
        const sessionData = await sessionStore.fetchSessionById(sessionId);
        session.value = sessionData;
        
        const response = await axios.get(`http://localhost:5020/api/Session/${sessionId}/attendances`);
        console.log("Données de présence:", response.data);
        if (response.data) {
          for (const student of response.data) {
            students.value.push({
              id: student.item1.id,
              name: student.item1.name,
              firstname: student.item1.firstname,
              status: student.item2 === 0 ? 'Present' : 'Absent',
              signature: student.item1.signature || '' 
            });
          }
          students.value = students.value.sort((a, b) => a.name.localeCompare(b.name));
        }
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
      router.push('/sessions');
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
        const element = document.querySelector('.attendance-table-wrapper');
        
        if (!element) {
          throw new Error("Élément non trouvé pour l'exportation");
        }
        
        const options = {
          margin: 10,
          filename: `presence_${session.value.year}_${formatDateForFilename(session.value.date)}.pdf`,
          image: { type: 'jpeg', quality: 0.98 },
          html2canvas: { scale: 2, useCORS: true },
          jsPDF: { unit: 'mm', format: 'a4', orientation: 'portrait' }
        };
        
        await html2pdf().from(element).set(options).save();
      } catch (err) {
        console.error("Erreur lors de l'export PDF:", err);
        error.value = "Impossible d'exporter le PDF.";
      } finally {
        exporting.value = false;
      }
    };

    const formatDateForFilename = (dateString) => {
      if (!dateString) return 'unknown-date';
      const date = new Date(dateString);
      return `${date.getFullYear()}-${(date.getMonth() + 1).toString().padStart(2, '0')}-${date.getDate().toString().padStart(2, '0')}`;
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

.actions {
  display: flex;
  justify-content: flex-start;
  margin-top: 15px;
}

.back-button {
  background-color: #3498db;
  color: white;
  border: none;
  padding: 10px 20px;
  border-radius: 4px;
  cursor: pointer;
  transition: background-color 0.3s;
}

.back-button:hover {
  background-color: #2980b9;
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

.signature-placeholder {
  height: 20px;
  border-bottom: 1px solid #ccc;
}

@media (max-width: 768px) {
  .attendance-table {
    min-width: 600px;
  }
}
</style>
