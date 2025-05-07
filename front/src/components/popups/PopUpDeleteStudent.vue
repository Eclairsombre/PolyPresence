<template>
  <div class="popup-delete-student">
    <div class="popup-content">
      <div class="popup-header">
        <h2>Supprimer l'étudiant</h2>
        <button class="close-button" @click="$emit('close')">&times;</button>
      </div>
      <div class="popup-body">
        <p>Voulez-vous vraiment supprimer cet étudiant&nbsp;?</p>
        <div class="form-actions">
          <button class="cancel-btn" @click="$emit('close')">Annuler</button>
          <button class="delete-btn" @click="$emit('confirm')">Supprimer</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
</script>

<style scoped>
.popup-delete-student {
  position: fixed;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  background: rgba(0, 0, 0, 0.6);
  display: flex;
  justify-content: center;
  align-items: center;
  z-index: 1000;
  animation: fadeIn 0.3s ease-in-out;
}

@keyframes fadeIn {
  from { opacity: 0; }
  to { opacity: 1; }
}

.popup-content {
  background: white;
  width: 90%;
  max-width: 400px;
  border-radius: 8px;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.2);
  overflow: hidden;
  animation: slideIn 0.3s ease-in-out;
}

@keyframes slideIn {
  from { transform: translateY(-30px); opacity: 0; }
  to { transform: translateY(0); opacity: 1; }
}

.popup-header {
  background: #2c3e50;
  color: white;
  padding: 15px 20px;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.popup-header h2 {
  margin: 0;
  font-size: 1.2rem;
  font-weight: 500;
}

.close-button {
  background: none;
  border: none;
  color: white;
  font-size: 1.8rem;
  cursor: pointer;
  padding: 0;
  line-height: 1;
}

.popup-body {
  padding: 20px;
  text-align: center;
}

.form-actions {
  display: flex;
  justify-content: center;
  gap: 10px;
  margin-top: 20px;
}

.cancel-btn, .delete-btn {
  padding: 10px 20px;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-weight: 500;
  transition: background-color 0.3s;
}

.cancel-btn {
  background-color: #f5f5f5;
  color: #333;
}

.cancel-btn:hover {
  background-color: #e0e0e0;
}

.delete-btn {
  background-color: #e74c3c;
  color: white;
}

.delete-btn:hover {
  background-color: #c0392b;
}

@media (max-width: 600px) {
  .popup-content {
    padding: 8px 2vw;
    max-width: 98vw;
  }
  .popup-header h2 {
    font-size: 1.1rem;
  }
  .cancel-btn, .delete-btn {
    width: 100%;
    padding: 8px 0;
    font-size: 1em;
  }
}
</style>
