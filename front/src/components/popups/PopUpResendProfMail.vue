<template>
  <div class="popup-overlay">
    <div class="popup-content warning">
      <h2>Renvoyer le mail au professeur ?</h2>
      <div class="popup-warning">
        <span class="warning-icon">⚠️</span>
        <span>
          N'utilisez cette option que si l'email du professeur était mal renseigné.<br>
          Un nouveau mail de signature sera envoyé à l'adresse actuellement enregistrée.
        </span>
      </div>
      <div class="popup-actions">
        <button @click="$emit('close')">Annuler</button>
        <button @click="$emit('confirm')">Renvoyer le mail</button>
      </div>
    </div>
  </div>
</template>

<script setup>
</script>

<style scoped>
.popup-overlay {
  position: fixed;
  top: 0; left: 0; right: 0; bottom: 0;
  background: rgba(0,0,0,0.35);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 2100;
}
.popup-content.warning {
  background: #fffbe6;
  border-radius: 10px;
  padding: 32px 24px;
  min-width: 340px;
  box-shadow: 0 4px 24px rgba(0,0,0,0.18);
  display: flex;
  flex-direction: column;
  gap: 18px;
  border: 2px solid #f1c40f;
  position: relative;
}
.popup-content h2 {
  margin: 0 0 10px 0;
  font-size: 1.25rem;
  color: #b37b00;
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
}
.warning-icon {
  font-size: 1.6rem;
  margin-top: 2px;
}
.popup-actions {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
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
  background: #f1c40f;
  color: #fff;
}
.popup-content button:first-child {
  background: #e74c3c;
  color: #fff;
}
.popup-content button:last-child:hover {
  background: #b37b00;
}
.popup-content button:first-child:hover {
  background: #c0392b;
}

@media (max-width: 600px) {
  .popup-content.warning {
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
}
</style>
