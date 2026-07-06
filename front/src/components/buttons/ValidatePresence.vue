<template>
  <div class="validate-container">
    <h3 class="validate-title">Valider ma présence</h3>
    <p class="validate-hint">Entrez le code communiqué par votre professeur</p>
    <div class="validate-row">
      <input
        type="text"
        v-model="validationCode"
        placeholder="Code de validation"
        aria-label="Code de validation"
        class="validate-input"
        inputmode="numeric"
        autocomplete="off"
        :disabled="isSubmitting"
      />
      <button
        @click="validatePresence"
        class="validate-btn"
        :disabled="isSubmitting"
      >
        {{ isSubmitting ? "Validation…" : "Valider" }}
      </button>
    </div>
  </div>
</template>

<script setup>
import { ref, defineEmits, onMounted, onUnmounted } from "vue";
import { useSessionStore } from "../../stores/sessionStore";
import { useAuthStore } from "../../stores/authStore";
import { useStudentsStore } from "../../stores/studentsStore";
import { useToastStore } from "../../stores/toastStore";

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
const toast = useToastStore();
const validationCode = ref("");
const isSubmitting = ref(false);

const validatePresence = async () => {
  if (isSubmitting.value) return;

  if (!authStore.user || !authStore.user.studentId) {
    toast.error("Veuillez vous connecter pour accéder à cette fonctionnalité.");
    return;
  }

  if (!props.hasSignature) {
    toast.error("Vous devez d'abord définir votre signature (menu « Ma signature »).");
    return;
  }

  if (validationCode.value.trim() === "") {
    toast.error("Veuillez saisir le code de validation.");
    return;
  }

  isSubmitting.value = true;
  try {
    await sessionStore.validatePresence(
      authStore.user.studentId,
      sessionStore.currentSession.id,
      validationCode.value,
    );
    toast.success("Présence validée avec succès.");
    emit("presenceValidated");
  } catch (error) {
    console.debug("Erreur lors de la validation de la présence:", error);
    toast.error(
      error.response?.data?.message ||
        "Une erreur s'est produite lors de la validation de la présence.",
    );
  } finally {
    isSubmitting.value = false;
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

.validate-btn:hover:not(:disabled) {
  background: #219150;
  box-shadow: 0 4px 12px rgba(39, 174, 96, 0.25);
  transform: translateY(-1px);
}

.validate-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
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
