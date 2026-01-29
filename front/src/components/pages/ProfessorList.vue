<template>
  <div class="professor-list">
    <h1>Liste des professeurs</h1>
    <ul v-if="professors.length">
      <li v-for="prof in professors" :key="prof.id">
        <strong>{{ prof.firstname }} {{ prof.name }}</strong>
        <span v-if="!prof.editing"> ({{ prof.email }}) </span>
        <template v-else>
          <input v-model="prof.newEmail" type="email" style="margin-left:8px;" />
        </template>
        <button v-if="!prof.editing" @click="enableEdit(prof)">Modifier</button>
        <button v-else @click="saveEmail(prof)">Enregistrer</button>
        <button v-if="prof.editing" @click="cancelEdit(prof)">Annuler</button>
      </li>
    </ul>
    <div v-else>Aucun professeur trouvé.</div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue';
import { useProfessorStore } from '../../stores/professorStore';

const professorStore = useProfessorStore();
const professors = ref([]);

onMounted(async () => {
  await professorStore.fetchProfessors();
  professors.value = professorStore.professors;
  professorStore.professors.forEach(p => {
    p.editing = false;
    p.newEmail = p.email;
  });
});

function enableEdit(prof) {
  prof.editing = true;
  prof.newEmail = prof.email;
}

function cancelEdit(prof) {
  prof.editing = false;
  prof.newEmail = prof.email;
}

async function saveEmail(prof) {
  if (!prof.newEmail || prof.newEmail === prof.email) {
    prof.editing = false;
    return;
  }
  const ok = await professorStore.updateProfessorEmail(prof.id, prof.newEmail);
  if (ok) {
    prof.email = prof.newEmail;
    prof.editing = false;
  } else {
    alert("Erreur lors de la mise à jour de l'email");
  }
}
</script>

<style scoped>
.professor-list {
  max-width: 600px;
  margin: 2rem auto;
  background: #fff;
  border-radius: 8px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.08);
  padding: 2rem;
}
h1 {
  margin-bottom: 1.5rem;
}
ul {
  list-style: none;
  padding: 0;
}
li {
  padding: 0.5rem 0;
  border-bottom: 1px solid #eee;
  display: flex;
  align-items: center;
  gap: 8px;
}
li:last-child {
  border-bottom: none;
}
input[type="email"] {
  padding: 2px 6px;
  border-radius: 4px;
  border: 1px solid #ccc;
}
button {
  margin-left: 8px;
  padding: 2px 8px;
  border-radius: 4px;
  border: none;
  background: #1976d2;
  color: #fff;
  cursor: pointer;
}
button:hover {
  background: #125ea2;
}
</style>
