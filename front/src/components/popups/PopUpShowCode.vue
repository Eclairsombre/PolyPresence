<template>
  <div class="popup-show-code">
    <div class="popup-content">
      <h3>Attention</h3>
      <p>
        Ne défloutez le code de session que s’il y a un problème avec l’envoi du mail, un problème avec le professeur, ou si la session est du travail personnel.
      </p>
      <div class="actions">
        <button @click="$emit('confirm')" class="confirm-btn">Déflouter</button>
        <button @click="$emit('close')" class="cancel-btn">Annuler</button>
      </div>
    </div>
  </div>
</template>

<script setup>
</script>

<style scoped>
.popup-show-code {
  position: fixed;
  top: 0;
  left: 0;
  width: 100vw;
  height: 100vh;
  background: rgba(0,0,0,0.4);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 2000;
}
.popup-content {
  background: #fff;
  border-radius: 8px;
  padding: 30px 24px;
  box-shadow: 0 4px 24px rgba(0,0,0,0.18);
  max-width: 400px;
  text-align: center;
}
.actions {
  margin-top: 24px;
  display: flex;
  justify-content: center;
  gap: 16px;
}
.confirm-btn {
  background: #3498db;
  color: #fff;
  border: none;
  padding: 8px 18px;
  border-radius: 4px;
  cursor: pointer;
}
.cancel-btn {
  background: #e74c3c;
  color: #fff;
  border: none;
  padding: 8px 18px;
  border-radius: 4px;
  cursor: pointer;
}
</style>
