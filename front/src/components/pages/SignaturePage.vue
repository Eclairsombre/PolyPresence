<template>
  <div class="signature-page">
    <div class="page-header">
      <h1>Ma signature</h1>
      <p class="page-subtitle">
        Créez ou modifiez votre signature pour l'émargement
      </p>
    </div>

    <div v-if="loading" class="state-card">
      <div class="spinner"></div>
      <p>Chargement...</p>
    </div>

    <div v-else-if="error" class="state-card state-error">
      <p>{{ error }}</p>
      <button
        @click="loadSignature"
        class="btn btn-outline"
        style="margin-top: 8px"
      >
        Réessayer
      </button>
    </div>

    <div v-else class="signature-grid">
      <section class="section">
        <div class="section-header">
          <h2>Signature actuelle</h2>
        </div>
        <div class="section-body signature-preview">
          <SignatureDisplay
            :signatureData="signatureData"
            :showEditButton="false"
          />
        </div>
      </section>

      <section class="section">
        <div class="section-header">
          <h2>
            {{ signatureData ? "Modifier ma signature" : "Créer ma signature" }}
          </h2>
        </div>
        <div class="section-body">
          <SignatureCreator @signatureSaved="onSignatureSaved" />
        </div>
      </section>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from "vue";
import { useRouter } from "vue-router";
import { useSessionStore } from "../../stores/sessionStore";
import { useAuthStore } from "../../stores/authStore";
import SignatureDisplay from "../signature/SignatureDisplay.vue";
import SignatureCreator from "../signature/SignatureCreator.vue";

const router = useRouter();
const sessionStore = useSessionStore();
const authStore = useAuthStore();

const signatureData = ref("");
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
  width: 100%;
}

.page-header {
  margin-bottom: 24px;
}

.page-header h1 {
  font-size: 1.6rem;
  font-weight: 700;
  color: #1a1a2e;
  margin: 0 0 4px;
}

.page-subtitle {
  color: #6c757d;
  font-size: 0.92rem;
  margin: 0;
}

/* State cards */
.state-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 12px;
  padding: 40px 24px;
  border-radius: 12px;
  border: 1px solid #e0e4ea;
  background: #fff;
  text-align: center;
}

.state-card p {
  margin: 0;
  color: #6c757d;
}

.state-error {
  border-color: #fecaca;
  background: #fff5f5;
}

.state-error p {
  color: #c0392b;
}

.spinner {
  width: 28px;
  height: 28px;
  border: 3px solid #e0e4ea;
  border-top-color: #1a1a2e;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

/* Grid */
.signature-grid {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.section {
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 14px;
  overflow: hidden;
}

.section-header {
  padding: 16px 22px;
  border-bottom: 1px solid #f0f0f5;
}

.section-header h2 {
  font-size: 1.05rem;
  font-weight: 600;
  color: #1a1a2e;
  margin: 0;
}

.section-body {
  padding: 22px;
}

.signature-preview {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 120px;
}

.btn {
  padding: 9px 18px;
  border: none;
  border-radius: 8px;
  font-size: 0.9rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-outline {
  background: transparent;
  color: #1a1a2e;
  border: 1px solid #d1d5db;
}

.btn-outline:hover {
  background: #f8f9fb;
}

@media (max-width: 600px) {
  .section-body {
    padding: 14px;
  }
}
</style>
