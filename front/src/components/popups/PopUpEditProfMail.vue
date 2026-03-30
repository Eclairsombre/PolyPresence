<template>
  <div class="popup-overlay">
    <div class="popup-card">
      <div class="popup-header">
        <h2>Modifier l'email du professeur</h2>
      </div>
      <div class="popup-body">
        <div class="popup-warning">
          <span class="warning-icon">⚠️</span>
          <span>
            N'utilisez cette option que si l'email du professeur était mal
            renseigné.<br />
            Un mail de signature sera envoyé à la nouvelle adresse.
          </span>
        </div>
        <div class="form-field">
          <label>Nouvel email</label>
          <input
            v-model="inputValue"
            type="email"
            required
            placeholder="professeur@univ-lyon1.fr"
          />
        </div>
        <div v-if="error" class="popup-error">{{ error }}</div>
        <div class="popup-actions">
          <button class="btn btn-cancel" @click="$emit('close')">
            Annuler
          </button>
          <button class="btn btn-save" @click="save">Enregistrer</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, watch } from "vue";
const props = defineProps({
  value: String,
});
const inputValue = ref(props.value || "");
const error = ref("");
watch(
  () => props.value,
  (val) => {
    inputValue.value = val;
  },
);
const emit = defineEmits(["close", "save"]);
const save = () => {
  if (!inputValue.value) {
    error.value = "L'email ne peut pas être vide.";
    return;
  }
  if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(inputValue.value)) {
    error.value = "Format d'email invalide.";
    return;
  }
  error.value = "";
  emit("save", inputValue.value);
};
</script>

<style scoped>
.popup-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.4);
  backdrop-filter: blur(4px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 2000;
  padding: 16px;
}

.popup-card {
  background: #fff;
  border-radius: 16px;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.15);
  width: 100%;
  max-width: 420px;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.popup-header {
  padding: 24px 24px 16px;
  text-align: center;
}

.popup-header h2 {
  font-size: 1.15rem;
  font-weight: 700;
  color: #1a1a2e;
  margin: 0;
}

.popup-body {
  padding: 0 24px 24px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.popup-warning {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  background: #fffbeb;
  border: 1px solid #fde68a;
  border-radius: 10px;
  padding: 12px 14px;
  font-size: 0.88rem;
  color: #92400e;
  line-height: 1.4;
}

.warning-icon {
  font-size: 1.2rem;
  flex-shrink: 0;
  margin-top: 1px;
}

.form-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form-field label {
  font-size: 0.82rem;
  font-weight: 600;
  color: #495057;
  text-transform: uppercase;
  letter-spacing: 0.3px;
}

.form-field input[type="email"] {
  width: 100%;
  padding: 11px 14px;
  border: 1px solid #d1d5db;
  border-radius: 10px;
  font-size: 0.95rem;
  color: #1a1a2e;
  outline: none;
  box-sizing: border-box;
  transition:
    border-color 0.2s,
    box-shadow 0.2s;
}

.form-field input[type="email"]:focus {
  border-color: #3498db;
  box-shadow: 0 0 0 3px rgba(52, 152, 219, 0.12);
}

.popup-error {
  color: #c0392b;
  text-align: center;
  font-size: 0.88rem;
  font-weight: 500;
}

.popup-actions {
  display: flex;
  gap: 10px;
  justify-content: flex-end;
}

.popup-actions .btn {
  padding: 9px 18px;
  border-radius: 8px;
  font-size: 0.9rem;
  font-weight: 600;
  cursor: pointer;
  border: none;
  transition: all 0.15s;
}

.btn-cancel {
  background: #f8f9fb;
  color: #495057;
  border: 1px solid #d1d5db;
}

.btn-cancel:hover {
  background: #e8ecf1;
}

.btn-save {
  background: #27ae60;
  color: #fff;
}

.btn-save:hover {
  background: #219150;
}

@media (max-width: 480px) {
  .popup-card {
    max-width: 100%;
    border-radius: 12px;
  }
}
</style>
