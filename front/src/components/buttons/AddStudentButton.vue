<template>
  <button class="add-button" @click="$emit('click')">
    <span class="plus-icon">+</span>
    <span class="button-text">Ajouter un étudiant</span>
  </button>
</template>

<script>
export default {
  name: 'AddStudentButton',
  emits: ['click']
}
</script>

<style scoped>
.add-button {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 8px 16px;
  background-color: #4caf50;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  transition: background-color 0.3s ease;
  font-weight: 500;
}

.add-button:hover {
  background-color: #45a049;
}

.plus-icon {
  font-size: 1.2em;
  margin-right: 8px;
  font-weight: bold;
}

.button-text {
  display: inline-block;
}

@media (max-width: 768px) {
  .button-text {
    display: none;
  }
  
  .plus-icon {
    margin-right: 0;
  }
  
  .add-button {
    width: 40px;
    height: 40px;
    border-radius: 50%;
    padding: 0;
  }
}
</style>