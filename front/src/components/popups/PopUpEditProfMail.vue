<template>
  <div class="popup-overlay">
    <div class="popup-content">
      <h2>Modifier l'email du professeur</h2>
      <div class="popup-warning">
        <span class="warning-icon">⚠️</span>
        <span>
          N'utilisez cette option que si l'email du professeur était mal renseigné.<br>
          Un mail de signature sera envoyé à la nouvelle adresse.
        </span>
      </div>
      <label>Nouvel email du professeur</label>
      <input v-model="inputValue" type="email" required />
      <div class="popup-actions">
        <button @click="$emit('close')">Annuler</button>
        <button @click="save">Enregistrer</button>
      </div>
      <div v-if="error" class="popup-error">{{ error }}</div>
    </div>
  </div>
</template>

<script setup>
import { ref, watch } from 'vue';
const props = defineProps({
  value: String
});
const inputValue = ref(props.value || '');
const error = ref('');
watch(() => props.value, (val) => { inputValue.value = val; });
const emit = defineEmits(['close', 'save']);
const save = () => {
  if (!inputValue.value) {
    error.value = "L'email ne peut pas être vide.";
    return;
  }
  if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(inputValue.value)) {
    error.value = "Format d'email invalide.";
    return;
  }
  error.value = '';
  emit('save', inputValue.value);
};
</script>

<style scoped>
.popup-overlay {
  position: fixed;
  top: 0; left: 0; right: 0; bottom: 0;
  background: rgba(0,0,0,0.35);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 2000;
}
.popup-content {
  background: #fff;
  border-radius: 10px;
  padding: 32px 24px;
  min-width: 340px;
  box-shadow: 0 4px 24px rgba(0,0,0,0.18);
  display: flex;
  flex-direction: column;
  gap: 18px;
  position: relative;
}
.popup-content h2 {
  margin: 0 0 10px 0;
  font-size: 1.25rem;
  color: #2c3e50;
  text-align: center;
}
.popup-warning {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  background: #fff3cd;
  color: #856404;
  border-radius: 6px;
  padding: 12px 14px;
  font-size: 1rem;
  margin-bottom: 8px;
}
.warning-icon {
  font-size: 1.6rem;
  margin-top: 2px;
}
.popup-content label {
  font-weight: 500;
  color: #555;
  margin-bottom: 4px;
}
.popup-actions {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}
input[type="email"] {
  padding: 9px;
  border-radius: 4px;
  border: 1px solid #ccc;
  font-size: 16px;
  margin-top: 2px;
}
.popup-error {
  color: #e74c3c;
  margin-top: 8px;
  text-align: center;
  font-size: 0.98rem;
}
.popup-content button {
  padding: 8px 18px;
  border-radius: 4px;
  border: none;
  font-size: 1rem;
  cursor: pointer;
  transition: background 0.2s, color 0.2s;
}
.popup-content button:last-child {
  background: #27ae60;
  color: #fff;
}
.popup-content button:first-child {
  background: #e74c3c;
  color: #fff;
}
.popup-content button:last-child:hover {
  background: #219150;
}
.popup-content button:first-child:hover {
  background: #c0392b;
}
@media (max-width: 600px) {
  .popup-content {
    padding: 10px 2vw;
    min-width: unset;
    max-width: 98vw;
  }
  .popup-content h2 {
    font-size: 1.1rem;
  }
  .popup-actions button {
    width: 100%;
    padding: 8px 0;
    font-size: 1em;
  }
  input[type="email"] {
    font-size: 0.98em;
    padding: 8px;
  }
}
</style>
