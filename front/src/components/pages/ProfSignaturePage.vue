<template>
  <div class="prof-signature-page">
    <h1>Signature du professeur</h1>
    <div v-if="loading" class="loading">Chargement...</div>
    <div v-else-if="error" class="error">{{ error }}</div>
    <div v-else>
      <form @submit.prevent="submitSignature" class="prof-form">
        <div class="form-group">
          <label>Nom :</label>
          <input v-model="profName" type="text" required />
        </div>
        <div class="form-group">
          <label>Prénom :</label>
          <input v-model="profFirstname" type="text" required />
        </div>
        <div class="form-group">
          <label>Signature :</label>
          <SignatureCreator v-bind:hideSaveButton="true" ref="signaturePad" />
          <button type="button" @click="clearSignature" class="clear-btn">Effacer la signature</button>
        </div>
        <button type="submit" class="submit-btn">Valider</button>
      </form>
      <div v-if="success" class="success">Signature enregistrée avec succès !</div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import { useRoute } from 'vue-router';
import SignatureCreator from '../signature/SignatureCreator.vue';
import axios from 'axios';

const route = useRoute();
const token = route.params.token;
const profName = ref('');
const profFirstname = ref('');
const loading = ref(true);
const error = ref('');
const success = ref(false);
const signaturePad = ref(null);

onMounted(async () => {
  try {
    // Vérifie que le token est valide
    await axios.get(`/api/Session/prof-signature/${token}`);
    loading.value = false;
  } catch (e) {
    error.value = "Lien invalide ou expiré.";
    loading.value = false;
  }
});

const clearSignature = () => {
  signaturePad.value && signaturePad.value.clear();
};
const API_URL = import.meta.env.VITE_API_URL;

const submitSignature = async () => {
  const signatureData = signaturePad.value.getSignature();
  if (!signatureData || signatureData.length < 30) {
    error.value = "Merci de signer dans la zone prévue.";
    return;
  }
  try {
    await axios.post(`${API_URL}/Session/prof-signature/${token}`, {
      Signature: signatureData,
      Name: profName.value,
      Firstname: profFirstname.value
    });
    success.value = true;
    error.value = '';
  } catch (e) {
    error.value = "Erreur lors de l'enregistrement. Veuillez réessayer.";
  }
};
</script>

<style scoped>
.prof-signature-page {
  max-width: 400px;
  margin: 40px auto;
  background: #fff;
  border-radius: 8px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.08);
  padding: 30px 20px;
}
.prof-signature-page h1 {
  text-align: center;
  margin-bottom: 20px;
}
.form-group {
  margin-bottom: 18px;
  display: flex;
  flex-direction: column;
}
input[type="text"] {
  padding: 8px;
  border-radius: 4px;
  border: 1px solid #ccc;
  font-size: 16px;
}
.clear-btn {
  margin-top: 8px;
  background: #eee;
  border: none;
  border-radius: 4px;
  padding: 6px 12px;
  cursor: pointer;
}
.submit-btn {
  width: 100%;
  background: #3498db;
  color: #fff;
  border: none;
  border-radius: 4px;
  padding: 12px;
  font-size: 16px;
  cursor: pointer;
  margin-top: 10px;
}
.success {
  color: #27ae60;
  text-align: center;
  margin-top: 20px;
}
.error {
  color: #e74c3c;
  text-align: center;
  margin-top: 20px;
}
.loading {
  text-align: center;
  color: #888;
}
</style>
