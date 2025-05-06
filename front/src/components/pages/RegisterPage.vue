<template>
  <div class="register-page">
    <div class="register-container">
      <h1>Créer un compte</h1>
      <div class="register-form-select">
        <label for="year-select">Choisissez votre année :</label>
        <select v-model="selectedYear" id="year-select" class="register-input" style="max-width:200px;margin-bottom:20px;">
          <option value="3A">3A</option>
          <option value="4A">4A</option>
          <option value="5A">5A</option>
        </select>
        <label for="student-select">Sélectionnez votre nom :</label>
        <select v-model="selectedStudentNumber" id="student-select" class="register-input" style="max-width:300px;margin-bottom:20px;">
          <option value="" disabled>Choisir un étudiant</option>
          <option v-for="student in studentsByYear[selectedYear]" :key="student.studentNumber" :value="student.studentNumber">
            {{ student.name }} {{ student.firstname }}
          </option>
        </select>
        <button class="register-btn" :disabled="!selectedStudentNumber || sending" @click="sendRegisterMail">
          {{ sending ? 'Envoi en cours...' : 'Recevoir un lien de création de mot de passe' }}
        </button>
        <div v-if="errorMessage" class="register-error">{{ errorMessage }}</div>
        <div v-if="successMessage" class="register-success">{{ successMessage }}</div>
      </div>
      <button @click="goToLogin" class="redirect-btn">Déjà un compte ? Se connecter</button>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import axios from 'axios';
import { useRouter } from 'vue-router';
import { useStudentsStore } from '../../stores/studentsStore.js';

const studentsByYear = ref({ '3A': [], '4A': [], '5A': [] });
const studentsStore = useStudentsStore();
const selectedYear = ref('3A');
const selectedStudentNumber = ref('');
const errorMessage = ref('');
const successMessage = ref('');
const sending = ref(false);
const router = useRouter();
const API_URL = import.meta.env.VITE_API_URL;

const fetchAllStudents = async () => {
  for (const year of ['3A', '4A', '5A']) {
    const students = await studentsStore.fetchStudents(year);
    studentsByYear.value[year] = students.sort((a, b) => a.name.localeCompare(b.name));
  }
};

const goToLogin = () => {
  router.push('/login');
};

onMounted(async () => {
  await fetchAllStudents();
});

const sendRegisterMail = async () => {
  errorMessage.value = '';
  successMessage.value = '';
  sending.value = true;
  try {
    await axios.post(`${API_URL}/User/send-register-link`, {
      studentNumber: selectedStudentNumber.value,
    });
    successMessage.value = 'Un mail vous a été envoyé avec un lien pour créer votre mot de passe.';
  } catch (error) {
    if (error?.response?.data?.message?.includes('mot de passe existe déjà')) {
      errorMessage.value = 'Vous avez déjà un compte. Veuillez utiliser la page de connexion.';
    } else if (error?.response?.data?.message?.includes('déjà été envoyé')) {
      errorMessage.value = "Un mail a déjà été envoyé récemment. Merci de vérifier votre boîte mail ou de patienter avant une nouvelle demande.";
    } else {
      errorMessage.value = error?.response?.data?.message || 'Erreur lors de l’envoi du mail.';
    }
  } finally {
    sending.value = false;
  }
};
</script>

<style scoped>
.register-page {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 80vh;
  background: #f5f7fa;
}
.register-container {
  background: #fff;
  padding: 40px 30px;
  border-radius: 12px;
  box-shadow: 0 4px 20px rgba(0,0,0,0.08);
  text-align: center;
}
.register-btn {
  background: #2c3e50;
  color: #fff;
  border: none;
  padding: 14px 32px;
  border-radius: 30px;
  font-size: 1.1rem;
  font-weight: 500;
  cursor: pointer;
  margin-top: 20px;
  transition: background 0.2s;
}
.register-btn:hover {
  background: #1a2533;
}
.register-input {
  display: block;
  width: 100%;
  margin: 12px 0;
  padding: 12px;
  border: 1px solid #d1d5db;
  border-radius: 8px;
  font-size: 1rem;
  box-sizing: border-box;
}
.register-error {
  color: #c0392b;
  margin-top: 18px;
  font-weight: 500;
}
.register-success {
  color: #27ae60;
  margin-top: 18px;
  font-weight: 500;
}
.back-login-link {
  display: block;
  margin-top: 18px;
  color: #3498db;
  text-decoration: underline;
  font-size: 1rem;
}
.students-list-register {
  margin-top: 40px;
  text-align: left;
}
.students-year-section {
  margin-bottom: 24px;
}
.students-year-section h3 {
  margin-bottom: 8px;
}
.students-year-section ul {
  margin: 0;
  padding-left: 20px;
}
.register-form-select {
  margin-bottom: 32px;
  text-align: left;
}
.redirect-btn {
  background: transparent;
  color: #2c3e50;
  border: 1px solid #2c3e50;
  padding: 10px 28px;
  border-radius: 30px;
  font-size: 1rem;
  font-weight: 500;
  cursor: pointer;
  margin-top: 14px;
  margin-bottom: 0;
  transition: background 0.2s, color 0.2s;
  display: block;
  width: 100%;
}
.redirect-btn:hover {
  background: #2c3e50;
  color: #fff;
}
</style>
