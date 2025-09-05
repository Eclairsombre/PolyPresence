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
import { ref, defineEmits,onMounted,onUnmounted } from 'vue';
import { useSessionStore } from '../../stores/sessionStore';
import { useAuthStore } from '../../stores/authStore';
import { useStudentsStore } from '../../stores/studentsStore';

const props = defineProps({
    hasSignature: {
        type: Boolean,
        required: true,
    },
});


const handleKeyPress = (event) => {
  if (event.key === 'Enter') {
    validatePresence();
  }
};

onMounted(() => {
  window.addEventListener('keypress', handleKeyPress);
});

onUnmounted(() => {
  window.removeEventListener('keypress', handleKeyPress);
});

const emit = defineEmits(['presenceValidated']);
const sessionStore = useSessionStore();
const authStore = useAuthStore();
const studentsStore = useStudentsStore();
const validationCode = ref('');

const validatePresence = async () => {

    if (!authStore.user || !authStore.user.studentId) {
        alert("Veuillez vous connecter pour accéder à cette fonctionnalité.");
        return;
    }

    if(!props.hasSignature) {
        alert("Vous devez d'abord définir votre signature.");
        return;
    }

    if (validationCode.value === '') {
        alert("Veuillez saisir le code de validation.");
        return;
    }

    try {
        await sessionStore.validatePresence(authStore.user.studentId, sessionStore.currentSession.id, validationCode.value);
        emit('presenceValidated'); 
    } catch (error) {
        console.error("Erreur lors de la validation de la présence:", error);
        if (error.response && error.response.data && error.response.data.message) {
            alert(error.response.data.message);
        } else {
            alert("Une erreur s'est produite lors de la validation de la présence.");
        }
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

@media (max-width: 600px) {
  .validate-presence-container {
    flex-direction: column;
    gap: 8px;
    max-width: 98vw;
    padding: 0 2vw;
  }
  .validation-input {
    font-size: 0.98em;
    padding: 8px;
  }
  .validation-button {
    width: 100%;
    padding: 8px 0;
    font-size: 1em;
  }
}
</style>