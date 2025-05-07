<template>
  <div class="signature-page">
    <div class="signature-header">
      <h1>Ma signature</h1>
    </div>
    
    <div class="signature-container">
      <div v-if="loading" class="loading-state">
        <div class="spinner"></div>
        <p>Chargement...</p>
      </div>
      
      <div v-else-if="error" class="error-state">
        <p>{{ error }}</p>
        <button @click="loadSignature" class="retry-button">Réessayer</button>
      </div>
      
     <div v-else class="signature-content">
        <div class="current-signature">
          <h2>Ma signature actuelle</h2>
          <SignatureDisplay 
            :signatureData="signatureData" 
            :showEditButton="false"
          />
        </div>
        
        <div class="signature-editor">
          <h2>{{ signatureData ? 'Modifier ma signature' : 'Créer ma signature' }}</h2>
          <SignatureCreator @signatureSaved="onSignatureSaved" />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { useSessionStore } from '../../stores/sessionStore';
import { useAuthStore } from '../../stores/authStore';
import SignatureDisplay from '../signature/SignatureDisplay.vue';
import SignatureCreator from '../signature/SignatureCreator.vue';

const router = useRouter();
const sessionStore = useSessionStore();
const authStore = useAuthStore();

const signatureData = ref('');
const loading = ref(true);
const error = ref(null);

const loadSignature = async () => {
  loading.value = true;
  error.value = null;
  
  try {
    if (!authStore.user || !authStore.user.studentId) {
      error.value = "Veuillez vous connecter pour accéder à cette page.";
      return;
    }
    
    const studentNumber = authStore.user.studentId;
    const response = await sessionStore.getSignature(studentNumber);
    
    if (response && response.signature) {
      signatureData.value = response.signature;
    }
  } catch (err) {
    console.error("Erreur lors du chargement de la signature:", err);
    error.value = "Impossible de charger votre signature. Veuillez réessayer.";
  } finally {
    loading.value = false;
  }
};

const onSignatureSaved = async () => {
  await loadSignature();
};


onMounted(loadSignature);
</script>

<style scoped>
.signature-page {
  max-width: 800px;
  margin: 0 auto;
  padding: 20px;
}

.signature-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 30px;
}

.signature-header h1 {
  margin: 0;
  color: #2c3e50;
}

.back-button {
  padding: 8px 16px;
  background-color: #f8f9fa;
  border: 1px solid #dee2e6;
  border-radius: 4px;
  color: #212529;
  cursor: pointer;
  transition: all 0.2s;
}

.back-button:hover {
  background-color: #e9ecef;
}

.signature-container {
  background-color: white;
  border-radius: 8px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
  padding: 20px;
}

.loading-state, .error-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 30px;
  text-align: center;
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
  color: #721c24;
}

.retry-button {
  margin-top: 15px;
  padding: 6px 12px;
  background-color: #3498db;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
}

.signature-content {
  display: flex;
  flex-direction: column;
  gap: 30px;
}

.current-signature, .signature-editor {
  padding: 20px;
  border: 1px solid #e9ecef;
  border-radius: 8px;
}

.current-signature h2, .signature-editor h2 {
  margin-top: 0;
  margin-bottom: 15px;
  font-size: 1.2rem;
  color: #495057;
}

@media (max-width: 768px) {
  .signature-page {
    padding: 10px;
  }
  
  .signature-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 15px;
  }
}

@media (max-width: 600px) {
  .signature-container {
    padding: 8px 2vw;
  }
  .signature-content {
    gap: 12px;
  }
  .current-signature, .signature-editor {
    padding: 8px 2vw;
  }
}
</style>
