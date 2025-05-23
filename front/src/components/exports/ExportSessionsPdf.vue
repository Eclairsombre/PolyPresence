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
import JSZip from 'jszip';
import { saveAs } from 'file-saver';
import {useMailPreferencesStore} from "../../stores/mailPreferencesStore.js";

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

const mailStore = useMailPreferencesStore();
const exporting = ref(false);
const exportMessage = ref('');
const exportError = ref(false);

const createPdfFileName = (session) => {
  const date = new Date(session.date);
  const month = (date.getMonth() + 1).toString().padStart(2, '0');
  const day = date.getDate().toString().padStart(2, '0');
  
  const folderStructure = `${session.year}/${month}/${day}`;
  const filename = `session_${session.year}_${session.date.split("T")[0]}_${session.startTime.replace(/:/g, "-")}.pdf`;
  
  return {
    folderPath: folderStructure,
    fileName: filename,
    fullPath: `${folderStructure}/${filename}`
  };
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
      
      const pdfBlob = await mailStore.getPdfBlob(session);
      
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

</style>

