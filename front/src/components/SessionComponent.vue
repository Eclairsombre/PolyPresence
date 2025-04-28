<template>
  <div>
    <h2>Gestion des Sessions</h2>
    <div>
      <label for="sessionId">ID de la Session:</label>
      <input type="text" v-model="sessionId" />
      <button @click="fetchSessionAttendances(sessionId)">Charger les Présences</button>
    </div>
    <div v-if="attendances.length">
      <h3>Présences</h3>
      <ul>
        <li v-for="attendance in attendances" :key="attendance.id">
          {{ attendance.firstname }} {{ attendance.name }} - Statut: {{
          attendance.status }}
        </li>
      </ul>
    </div>
    <div>
      <label for="studentNumber">Numéro d'Étudiant:</label>
      <input type="text" v-model="studentNumber" />
      <button @click="addUserToSession(sessionId, studentNumber)">Ajouter à la Session</button>
    </div>
  </div>
</template>

<script>
import axios from "axios";
const API_URL = "http://localhost:5000/api";

export default {
  data() {
    return {
      sessionId: "",
      studentNumber: "",
      attendances: [],
    };
  },
  methods: {
    async fetchSessionAttendances(sessionId) {
      try {
        const response = await axios.get(
          `${API_URL}/Session/${sessionId}/attendances`
        );
        this.attendances = response.data.map((attendance) => ({
          ...attendance.item1,
          status: attendance.item2,
        }));
      } catch (error) {
        console.error("Erreur lors de la récupération des présences:", error);
      }
    },
    async addUserToSession(sessionId, studentNumber) {
      try {
        const response = await axios.post(
          `${API_URL}/Session/${sessionId}/student/${studentNumber}`
        );
        console.log(response.data.message);
      } catch (error) {
        console.error("Erreur lors de l'ajout de l'utilisateur à la session:", error);
      }
    },
  },
};
</script>

<style scoped>
/* Ajoutez ici vos styles */
</style>