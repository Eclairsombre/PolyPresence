<template>
  <div class="prof-signature-page">
    <div v-if="session" class="session-infos">
        <h2>Informations de la session</h2>
        <ul>
            <li><strong>Année :</strong> {{ session.year || session.Year }}</li>
            <li><strong>Date :</strong> {{ session.date ? new Date(session.date).toLocaleDateString() : (session.Date ? new Date(session.Date).toLocaleDateString() : '') }}</li>
            <li><strong>Heure de début :</strong> {{ session.startTime || session.StartTime }}</li>
            <li><strong>Heure de fin :</strong> {{ session.endTime || session.EndTime }}</li>
            <li><strong>Code de validation :</strong> {{ validationCode }}</li>
        </ul>
    </div>
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
import { useProfSignatureStore } from '../../stores/profSignatureStore';

const route = useRoute();
const token = route.params.token;
const profName = ref('');
const profFirstname = ref('');
const loading = ref(true);
const error = ref('');
const success = ref(false);
const signaturePad = ref(null);
const validationCode = ref('');
const session = ref(null);
const API_URL = import.meta.env.VITE_API_URL;
const profSignatureStore = useProfSignatureStore();

onMounted(async () => {
  const data = await profSignatureStore.fetchSessionByProfSignatureToken(token);
  if (data) {
    session.value = data;
    validationCode.value = data.validationCode || data.ValidationCode || data.validation_code || '';
    loading.value = false;
  } else {
    error.value = profSignatureStore.error;
    loading.value = false;
  }
});

const clearSignature = () => {
  signaturePad.value && signaturePad.value.clear();
};

const submitSignature = async () => {
  const signatureData = signaturePad.value.getSignature();
  if (!signatureData || signatureData.length < 30) {
    error.value = "Merci de signer dans la zone prévue.";
    return;
  }
  const payload = {
    Signature: signatureData,
    Name: profName.value,
    Firstname: profFirstname.value
  };
  const result = await profSignatureStore.saveProfSignature(token, payload);
  if (result) {
    success.value = true;
    error.value = '';
  } else {
    error.value = profSignatureStore.error;
  }
};
</script>

<style scoped>
.session-infos {
  background: #f6f8fa;
  border: 1px solid #e0e4ea;
  border-radius: 8px;
  padding: 18px 16px 10px 16px;
  margin-bottom: 24px;
  box-shadow: 0 1px 4px rgba(52,152,219,0.07);
}
.session-infos h2 {
  margin-top: 0;
  font-size: 1.15em;
  color: #2c3e50;
  margin-bottom: 10px;
}
.session-infos ul {
  list-style: none;
  padding: 0;
  margin: 0;
}
.session-infos li {
  margin-bottom: 6px;
  font-size: 1em;
  color: #34495e;
}
.session-infos strong {
  color: #2980b9;
}
.prof-signature-page {
  max-width: 440px;
  margin: 40px auto;
  background: #fff;
  border-radius: 12px;
  box-shadow: 0 4px 24px rgba(52,152,219,0.10);
  padding: 36px 24px 28px 24px;
}
.prof-signature-page h1 {
  text-align: center;
  margin-bottom: 24px;
  color: #2980b9;
  font-size: 1.5em;
}
.form-group {
  margin-bottom: 20px;
  display: flex;
  flex-direction: column;
}
input[type="text"] {
  padding: 10px;
  border-radius: 5px;
  border: 1px solid #bfc9d1;
  font-size: 16px;
  background: #fafdff;
  transition: border 0.2s;
}
input[type="text"]:focus {
  border: 1.5px solid #3498db;
  outline: none;
}
.clear-btn {
  margin-top: 8px;
  background: #f2f6fa;
  border: none;
  border-radius: 4px;
  padding: 7px 14px;
  cursor: pointer;
  color: #555;
  transition: background 0.2s;
}
.clear-btn:hover {
  background: #e1eaf4;
}
.submit-btn {
  width: 100%;
  background: linear-gradient(90deg, #3498db 60%, #2980b9 100%);
  color: #fff;
  border: none;
  border-radius: 5px;
  padding: 13px;
  font-size: 17px;
  cursor: pointer;
  margin-top: 12px;
  font-weight: 600;
  box-shadow: 0 2px 8px rgba(52,152,219,0.08);
  transition: background 0.2s;
}
.submit-btn:hover {
  background: linear-gradient(90deg, #2980b9 60%, #3498db 100%);
}
.success {
  color: #27ae60;
  text-align: center;
  margin-top: 22px;
  font-weight: 600;
  font-size: 1.1em;
}
.error {
  color: #e74c3c;
  text-align: center;
  margin-top: 22px;
  font-weight: 600;
  font-size: 1.1em;
}
.loading {
  text-align: center;
  color: #888;
  font-size: 1.1em;
}
</style>
