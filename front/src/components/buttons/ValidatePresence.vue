<template>
    <div class="validate-presence-container">
        <input 
            type="text" 
            v-model="validationCode" 
            placeholder="Code de validation" 
            class="validation-input"
        />
        <button @click="validatePresence" class="validation-button">
            Valider la présence
        </button>
    </div>
</template>

<script setup>
import { ref, defineEmits } from 'vue';
import { useSessionStore } from '../../stores/sessionStore';
import { useAuthStore } from '../../stores/authStore';
import { useStudentsStore } from '../../stores/studentsStore';

const emit = defineEmits(['presenceValidated']);
const sessionStore = useSessionStore();
const authStore = useAuthStore();
const studentsStore = useStudentsStore();
const validationCode = ref('');

const validatePresence = async () => {

    // Vérifier si l'utilisateur est connecté
    if (!authStore.user || !authStore.user.studentId) {
        alert("Veuillez vous connecter pour accéder à cette fonctionnalité.");
        return;
    }

    // Vérifier si le code de validation est valide
    if (validationCode.value !== sessionStore.currentSession.validationCode) {
        alert("Le code de validation est incorrect.");
        return;
    }

    // Valider la présence de l'étudiant
    try {
        await sessionStore.validatePresence(authStore.user.studentId, sessionStore.currentSession.id);
        alert("Présence validée avec succès !");
        emit('presenceValidated'); // Émettre l'événement pour indiquer que la présence a été validée
    } catch (error) {
        console.error("Erreur lors de la validation de la présence:", error);
        alert("Une erreur s'est produite lors de la validation de la présence.");
    }
};
</script>

<style scoped>
.validate-presence-container {
    display: flex;
    flex-direction: column;
    gap: 15px;
    max-width: 400px;
    margin: 20px auto;
}

.validation-input {
    padding: 12px 15px;
    border: 1px solid #ccc;
    border-radius: 4px;
    font-size: 16px;
    transition: border-color 0.3s;
}

.validation-input:focus {
    border-color: #4CAF50;
    outline: none;
    box-shadow: 0 0 5px rgba(76, 175, 80, 0.3);
}

.validation-button {
    background-color: #4CAF50;
    color: white;
    border: none;
    padding: 12px 20px;
    text-align: center;
    text-decoration: none;
    font-size: 16px;
    border-radius: 4px;
    cursor: pointer;
    transition: background-color 0.3s;
}

.validation-button:hover {
    background-color: #45a049;
}

@media (min-width: 768px) {
    .validate-presence-container {
        flex-direction: row;
    }
    
    .validation-input {
        flex: 1;
    }
}
</style>