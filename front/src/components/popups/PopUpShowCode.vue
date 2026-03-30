<template>
  <div class="popup-overlay">
    <div class="popup-card">
      <div class="popup-header">
        <h2>Attention</h2>
      </div>
      <div class="popup-body">
        <div class="popup-warning">
          <span class="warning-icon">⚠️</span>
          <span>
            Ne défloutez le code que s'il y a un problème avec l'envoi du mail, un problème avec le professeur, ou si la session est du travail personnel.
          </span>
        </div>
        <div class="popup-actions">
          <button class="btn btn-cancel" @click="$emit('close')">Annuler</button>
          <button class="btn btn-primary" @click="$emit('confirm')">Déflouter</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
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
  max-width: 400px;
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
}

.popup-actions {
  display: flex;
  gap: 10px;
  justify-content: center;
}

.btn {
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

.btn-primary {
  background: #3498db;
  color: #fff;
}

.btn-primary:hover {
  background: #2980b9;
}

@media (max-width: 480px) {
  .popup-card {
    max-width: 100%;
    border-radius: 12px;
  }
}
</style>