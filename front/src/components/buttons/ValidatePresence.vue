<template>
  <div class="validate-container">
    <h3 class="validate-title">Valider ma présence</h3>
    <p class="validate-hint">Entrez le code communiqué par votre professeur</p>
    <div class="validate-row">
      <input
        type="text"
        v-model="validationCode"
        placeholder="Code de validation"
        class="validate-input"
      />
      <button @click="validatePresence" class="validate-btn">Valider</button>
    </div>
  </div>
</template>

<script setup>
import { ref, defineEmits, onMounted, onUnmounted } from "vue";
import { useSessionStore } from "../../stores/sessionStore";
import { useAuthStore } from "../../stores/authStore";
import { useStudentsStore } from "../../stores/studentsStore";

const props = defineProps({
  hasSignature: {
    type: Boolean,
    required: true,
  },
});

const handleKeyPress = (event) => {
  if (event.key === "Enter") {
    validatePresence();
  }
};

onMounted(() => {
  window.addEventListener("keypress", handleKeyPress);
});

onUnmounted(() => {
  window.removeEventListener("keypress", handleKeyPress);
});

const emit = defineEmits(["presenceValidated"]);
const sessionStore = useSessionStore();
const authStore = useAuthStore();
const studentsStore = useStudentsStore();
const validationCode = ref("");

const validatePresence = async () => {
  if (!authStore.user || !authStore.user.studentId) {
    alert("Veuillez vous connecter pour accéder à cette fonctionnalité.");
    return;
  }

  if (!props.hasSignature) {
    alert("Vous devez d'abord définir votre signature.");
    return;
  }

  if (validationCode.value === "") {
    alert("Veuillez saisir le code de validation.");
    return;
  }

  try {
    await sessionStore.validatePresence(
      authStore.user.studentId,
      sessionStore.currentSession.id,
      validationCode.value,
    );
    emit("presenceValidated");
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
.validate-container {
  display: flex;
  flex-direction: column;
  gap: 10px;
  max-width: 480px;
  margin: 0 auto;
}

.validate-title {
  font-size: 1rem;
  font-weight: 700;
  color: #1a1a2e;
  margin: 0;
}

.validate-hint {
  font-size: 0.85rem;
  color: #6c757d;
  margin: 0;
}

.validate-row {
  display: flex;
  gap: 10px;
  align-items: stretch;
}

.validate-input {
  flex: 1;
  padding: 11px 14px;
  border: 1px solid #d1d5db;
  border-radius: 10px;
  font-size: 1rem;
  color: #1a1a2e;
  outline: none;
  transition:
    border-color 0.2s,
    box-shadow 0.2s;
  font-family: "SF Mono", "Fira Code", monospace;
  letter-spacing: 1px;
}

.validate-input:focus {
  border-color: #27ae60;
  box-shadow: 0 0 0 3px rgba(39, 174, 96, 0.12);
}

.validate-btn {
  padding: 11px 24px;
  background: #27ae60;
  color: #fff;
  border: none;
  border-radius: 10px;
  font-size: 0.95rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
  white-space: nowrap;
}

.validate-btn:hover {
  background: #219150;
  box-shadow: 0 4px 12px rgba(39, 174, 96, 0.25);
  transform: translateY(-1px);
}

@media (max-width: 480px) {
  .validate-row {
    flex-direction: column;
  }
  .validate-btn {
    width: 100%;
  }
}
</style>
