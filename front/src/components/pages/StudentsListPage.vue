<template>
    <div class="students-list-page">
        <div class="header-section">
            <h1>Liste des étudiants</h1>
            <router-link to="/" class="back-link">Retour à l'accueil</router-link>
        </div>
        <div class="actions-bar">
            <div class="filter-buttons">
                <button 
                :class="{ active: yearFilter === '3A' }" 
                @click="filterYear('3A')"
                >3A</button>
                <button 
                    :class="{ active: yearFilter === '4A' }" 
                    @click="filterYear('4A')"
                >4A</button>
                <button 
                    :class="{ active: yearFilter === '5A' }" 
                    @click="filterYear('5A')"
                >5A</button>
                <button @click="openImportPopup">Importer</button>
            </div>
            <AddStudentButton @click="openAddPopup" />
        </div>
        <table>
            <thead>
                <tr>
                    <th>Nom</th>
                    <th>Prénom</th>
                    <th>Numéro étudiant</th>
                    <th>Email</th>
                    <th>Année</th>
                </tr>
            </thead>
            <tbody>
                <tr v-for="student in students" :key="student.id">
                    <td>{{ student.name }}</td>
                    <td>{{ student.firstname }}</td>
                    <td>{{ student.studentNumber }}</td>
                    <td>{{ student.email }}</td>
                    <td>{{ student.year }}</td>
                </tr>
            </tbody>
        </table>
    </div>
    <PopUpImportStudent 
      v-if="showImportPopup" 
      :year="yearFilter" 
      :students="students"
      @close="closeImportPopup" 
    />
    <PopUpAddStudent
      v-if="showAddPopup"
      :year="yearFilter"
      @close="closeAddPopup"
      @student-added="refreshStudents"
    />
</template>

<script setup>
import { useStudentsStore } from '../../stores/studentsStore.ts';
import { onMounted, ref } from 'vue';
import PopUpImportStudent from '../popups/PopUpImportStudent.vue';
import PopUpAddStudent from '../popups/PopUpAddStudent.vue';
import AddStudentButton from '../buttons/AddStudentButton.vue';

const yearFilter = ref('3A');
const studentsStore = useStudentsStore();
const showImportPopup = ref(false);
const showAddPopup = ref(false);

const students = ref([]);

onMounted(async () => {
    await refreshStudents();
});

const refreshStudents = async () => {
    students.value = await studentsStore.fetchStudents(yearFilter.value);
};

const filterYear = async (year) => {
    yearFilter.value = year;
    await refreshStudents();
};

const openImportPopup = () => {
  showImportPopup.value = true;
};

const closeImportPopup = async () => {
  showImportPopup.value = false;
  await refreshStudents();
};

const openAddPopup = () => {
  showAddPopup.value = true;
};

const closeAddPopup = () => {
  showAddPopup.value = false;
};
</script>

<style scoped>
.students-list-page {
    max-width: 1200px;
    margin: 0 auto;
    padding: 20px;
}

.header-section {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 20px;
}

.back-link {
    background-color: #2c3e50;
    color: white;
    padding: 8px 16px;
    border-radius: 4px;
    text-decoration: none;
    font-size: 14px;
}

.actions-bar {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 20px;
}

.filter-buttons {
    display: flex;
    gap: 10px;
}

.filter-buttons button {
    padding: 8px 16px;
    background-color: #f0f0f0;
    border: 1px solid #ddd;
    border-radius: 4px;
    cursor: pointer;
    transition: background-color 0.2s;
}

.filter-buttons button:hover {
    background-color: #e0e0e0;
}

.filter-buttons button.active {
    background-color: #2c3e50;
    color: white;
    border-color: #2c3e50;
}

table {
    width: 100%;
    border-collapse: collapse;
}

th, td {
    padding: 12px;
    text-align: left;
    border-bottom: 1px solid #ddd;
}

th {
    background-color: #f5f5f5;
    font-weight: bold;
}

tr:hover {
    background-color: #f9f9f9;
}
</style>