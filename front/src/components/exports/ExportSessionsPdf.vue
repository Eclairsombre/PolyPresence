<template>
  <div class="export-pdf-container">
    <button 
      @click="exportSessionsToPDF" 
      class="export-button"
      :disabled="exporting || !sessions || sessions.length === 0">
      <span v-if="exporting">
        <div class="spinner"></div>
        Export en cours...
      </span>
      <span v-else>
        Exporter les sessions (PDF)
      </span>
    </button>
    <div v-if="exportMessage" :class="['export-message', exportError ? 'error' : 'success']">
      {{ exportMessage }}
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import html2pdf from 'html2pdf.js';
import { useSessionStore } from '../../stores/sessionStore';
import axios from 'axios';
import JSZip from 'jszip';
import { saveAs } from 'file-saver';

const props = defineProps({
  sessions: {
    type: Array,
    required: true
  },
  selectedYear: {
    type: String,
    default: ''
  }
});

const sessionStore = useSessionStore();
const exporting = ref(false);
const exportMessage = ref('');
const exportError = ref(false);

const createPdfFileName = (session) => {
  const date = new Date(session.date);
  const year = date.getFullYear();
  const month = (date.getMonth() + 1).toString().padStart(2, '0');
  const day = date.getDate().toString().padStart(2, '0');
  
  const folderStructure = `${session.year}/${month}/${day}`;
  const fileName = `session_${session.id}_${year}_${month}_${day}.pdf`;
  
  return {
    folderPath: folderStructure,
    fileName: fileName,
    fullPath: `${folderStructure}/${fileName}`
  };
};
const API_URL = import.meta.env.VITE_API_URL;

const generateSessionHtml = async (session) => {
  try {
    const response = await axios.get(`${API_URL}/Session/${session.id}/attendances`);
    const attendances = response.data.$values;
    
    const formatDate = (dateString) => {
      const options = { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' };
      return new Date(dateString).toLocaleDateString('fr-FR', options);
    };
    
    const formatTime = (timeString) => {
      return timeString.substring(0, 5);
    };

    let tableRows = '';
    if (attendances && attendances.length > 0) {
      attendances.forEach((attendance, index) => {
        const student = attendance.item1;
        const status = attendance.item2 === 0 ? 'Présent' : 'Absent';
        const statusClass = status === 'Présent' ? 'status-present' : 'status-absent';
        
        tableRows += `
          <tr>
            <td>${index + 1}</td>
            <td>${student.name}</td>
            <td>${student.firstname}</td>
            <td class="${statusClass}">${status}</td>
            <td>${student.signature && status === 'Présent' ? 
              `<img src="${student.signature}" alt="Signature" style="max-height: 60px;">` : 
              ''}
            </td>
          </tr>
        `;
      });
    }

    const html = `
      <div style="font-family: Arial, sans-serif; padding: 20px;">
        <h1 style="text-align: center; color: #2c3e50;">Liste de présence</h1>
        <div style="margin-bottom: 20px; padding: 15px; background-color: #f8f9fa; border-radius: 8px;">
          <h2 style="margin: 0 0 10px 0;">${formatDate(session.date)} - ${session.year}</h2>
          <p v-if="session.name"><strong>Nom de la session :</strong> ${session.name}</p>
          <p>Horaires: ${formatTime(session.startTime)} - ${formatTime(session.endTime)}</p>
          <div style='margin-top:10px;'><strong>Professeur :</strong> ${session.profFirstname || ''} ${session.profName || ''} (${session.profEmail || ''})</div>
          <div style='margin-top:5px;'><strong>Signature :</strong> ${session.profSignature ? `<img src='${session.profSignature}' alt='Signature du professeur' style='max-height:60px;vertical-align:middle;margin-left:10px;'/>` : '<em>Non signée</em>'}</div>
        </div>
        <table style="width: 100%; border-collapse: collapse;">
          <thead>
            <tr style="background-color: #f1f1f1;">
              <th style="padding: 12px 15px; text-align: left; border-bottom: 1px solid #e0e0e0;">N°</th>
              <th style="padding: 12px 15px; text-align: left; border-bottom: 1px solid #e0e0e0;">Nom</th>
              <th style="padding: 12px 15px; text-align: left; border-bottom: 1px solid #e0e0e0;">Prénom</th>
              <th style="padding: 12px 15px; text-align: left; border-bottom: 1px solid #e0e0e0;">Présent/Absent</th>
              <th style="padding: 12px 15px; text-align: left; border-bottom: 1px solid #e0e0e0;">Signature</th>
            </tr>
          </thead>
          <tbody>
            ${tableRows}
          </tbody>
        </table>
      </div>
    `;
    
    return html;
  } catch (error) {
    console.error(`Erreur lors de la génération du HTML pour la session ${session.id}:`, error);
    throw error;
  }
};

const exportSessionsToPDF = async () => {
  if (exporting.value) return;
  if (!props.sessions || props.sessions.length === 0) {
    exportMessage.value = "Aucune session à exporter";
    exportError.value = true;
    setTimeout(() => {
      exportMessage.value = "";
      exportError.value = false;
    }, 3000);
    return;
  }
  
  exporting.value = true;
  exportMessage.value = "Préparation de l'export...";
  exportError.value = false;
  
  try {
    const zip = new JSZip();
    
    for (let i = 0; i < props.sessions.length; i++) {
      const session = props.sessions[i];
      
      exportMessage.value = `Export en cours... (${i+1}/${props.sessions.length})`;
      
      const pdfBlob = await generateSessionPDF(session);
      
      const { folderPath, fileName } = createPdfFileName(session);
      zip.file(`${folderPath}/${fileName}`, pdfBlob);
    }
    
    exportMessage.value = "Finalisation de l'archive...";
    const zipContent = await zip.generateAsync({ type: 'blob' });
    
    const zipFileName = `sessions_${props.selectedYear || 'all'}.zip`;
    
    saveAs(zipContent, zipFileName);
    
    exportMessage.value = props.sessions.length === 1 
      ? "Session exportée avec succès!" 
      : `${props.sessions.length} sessions exportées avec succès!`;
      
  } catch (error) {
    console.error("Erreur lors de l'export des sessions:", error);
    exportMessage.value = "Une erreur est survenue lors de l'export des sessions.";
    exportError.value = true;
  } finally {
    exporting.value = false;
    
    setTimeout(() => {
      exportMessage.value = "";
    }, 5000);
  }
};

const generateSessionPDF = async (session) => {
  try {
    const html = await generateSessionHtml(session);
    
    const opt = {
      margin: 10,
      filename: 'temp.pdf',
      image: { type: 'jpeg', quality: 0.98 },
      html2canvas: { scale: 2, useCORS: true, logging: false },
      jsPDF: { unit: 'mm', format: 'a4', orientation: 'portrait' }
    };
    
    const pdf = await html2pdf().from(html).set(opt).outputPdf('blob');
    
    return pdf;
  } catch (error) {
    console.error(`Erreur lors de la génération du PDF pour la session ${session.id}:`, error);
    throw error;
  }
};

onMounted(() => {
  exportMessage.value = "";
  exportError.value = false;
});
</script>

<style scoped>
.export-pdf-container {
  margin: 15px 0;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.export-button {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  background-color: #34495e;
  color: white;
  border: none;
  padding: 10px 16px;
  border-radius: 4px;
  cursor: pointer;
  font-weight: 500;
  transition: background-color 0.3s;
}

.export-button:hover:not(:disabled) {
  background-color: #2c3e50;
}

.export-button:disabled {
  background-color: #95a5a6;
  cursor: not-allowed;
}

.spinner {
  display: inline-block;
  width: 16px;
  height: 16px;
  border: 2px solid rgba(255, 255, 255, 0.3);
  border-radius: 50%;
  border-top-color: white;
  animation: spin 1s infinite linear;
  margin-right: 8px;
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

.export-message {
  padding: 10px;
  border-radius: 4px;
  font-size: 0.9rem;
  text-align: center;
}

.export-message.success {
  background-color: #d4edda;
  color: #155724;
  border: 1px solid #c3e6cb;
}

.export-message.error {
  background-color: #f8d7da;
  color: #721c24;
  border: 1px solid #f5c6cb;
}
</style>
