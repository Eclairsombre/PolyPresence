<template>
  <div class="popup-overlay" @click.self="close">
    <div class="popup-content">
      <h2>Validation de la signature</h2>
      <div class="popup-message">
        <p>
          Attention : Dès que la signature sera validée, <b>les élèves ne pourront plus se mettre présents pour cette session.</b><br>
          Êtes-vous sûr de vouloir continuer ?
        </p>
      </div>
      <div class="popup-actions">
        <button class="confirm-btn" @click="confirm">Oui, valider la signature</button>
        <button class="cancel-btn" @click="close">Annuler</button>
      </div>
    </div>
  </div>
</template>

<script setup>
const emit = defineEmits(['close', 'confirm']);

function close() {
  emit('close');
}
function confirm() {
  emit('confirm');
}
</script>

<style scoped>
.popup-overlay {
  position: fixed;
  top: 0; left: 0; right: 0; bottom: 0;
  background: rgba(0,0,0,0.35);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}
.popup-content {
  background: #fff;
  border-radius: 14px;
  box-shadow: 0 4px 24px rgba(52,152,219,0.18);
  padding: 32px 28px 24px 28px;
  max-width: 420px;
  width: 100%;
  text-align: center;
}
.popup-message {
  margin-bottom: 24px;
  font-size: 1.08em;
  color: #2c3e50;
}
.popup-actions {
  display: flex;
  justify-content: center;
  gap: 18px;
}
.confirm-btn {
  background: #217dbb;
  color: #fff;
  border: none;
  border-radius: 8px;
  padding: 10px 22px;
  font-size: 1em;
  font-weight: 600;
  cursor: pointer;
  transition: background 0.2s;
}
.confirm-btn:hover {
  background: #1761a0;
}
.cancel-btn {
  background: #e74c3c;
  color: #fff;
  border: none;
  border-radius: 8px;
  padding: 10px 22px;
  font-size: 1em;
  font-weight: 600;
  cursor: pointer;
  transition: background 0.2s;
}
.cancel-btn:hover {
  background: #c0392b;
}
</style>
