<!-- filepath: c:\Users\alext\OneDrive\Bureau\PolytechPresence\front\src\components\UserList.vue -->
<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { userService } from './api/api';

const users = ref([]);
const loading = ref(true);
const error = ref('');
const newUser = ref({ name: '', surname: '', email: '', studentNumber: '' });

onMounted(async () => {
  try {
    users.value = await userService.getAll();
    loading.value = false;
  } catch (err: any) {
    error.value = 'Erreur lors du chargement des utilisateurs: ' + err.message;
    loading.value = false;
  }
});

const deleteUser = async (id: number) => {
  try {
    await userService.delete(id);
    // Filtrer l'utilisateur supprimé de la liste
    users.value = users.value.filter(user => user.id !== id);
  } catch (err: any) {
    error.value = 'Erreur lors de la suppression: ' + err.message;
  }
};

const addUser = async () => {
  try {
    // Envoyer l'objet newUser directement
    const createdUser = await userService.create(newUser.value);
    users.value.push(createdUser);
    // Réinitialiser le formulaire
    newUser.value = { name: '', surname: '', email: '', studentNumber: '' };
  } catch (err: any) {
    error.value = 'Erreur lors de l\'ajout de l\'utilisateur: ' + err.message;
  }
};
</script>

<template>
  <div>
    <h1>Liste des utilisateurs</h1>
    
    <div v-if="loading">Chargement...</div>
    <div v-if="error" class="error">{{ error }}</div>
    
    <ul v-if="!loading && !error && users.length > 0">
      <li v-for="user in users" :key="user.id">
        {{ user.name }} ({{ user.email }})
        <button @click="deleteUser(user.id)">Supprimer</button>
      </li>
    </ul>
    <form @submit.prevent="addUser()">
        <input type="text" v-model="newUser.name" placeholder="Nom" required />
        <input type="text" v-model="newUser.surname" placeholder="Prénom" required />
        <input type="email" v-model="newUser.email" placeholder="Email" required />
        <input type="text" v-model="newUser.studentNumber" placeholder="Numéro étudiant" required />
        <button type="submit">Ajouter</button>
    </form>
    
    
    <div v-if="!loading && !error && users.length === 0">
      Aucun utilisateur trouvé.
    </div>
  </div>
</template>

<style scoped>
.error {
  color: red;
}
</style>