<template>
  <div class="popup-add-student">
    <div class="popup-content">
      <div class="popup-header">
        <h2>Ajouter un étudiant - {{ year }}</h2>
        <button class="close-button" @click="$emit('close')">&times;</button>
      </div>
      <div class="popup-body">
        <form @submit.prevent="handleSubmit" class="add-student-form">
          <div class="form-group">
            <label for="name">Nom <span class="required">*</span></label>
            <input 
              type="text" 
              id="name" 
              v-model="student.name" 
              required 
              placeholder="Nom de famille"
            />
          </div>
          
          <div class="form-group">
            <label for="firstname">Prénom <span class="required">*</span></label>
            <input 
              type="text" 
              id="firstname" 
              v-model="student.firstname" 
              required 
              placeholder="Prénom"
            />
          </div>
          
          <div class="form-group">
            <label for="studentNumber">Numéro étudiant <span class="required">*</span></label>
            <input 
              type="text" 
              id="studentNumber" 
              v-model="student.studentNumber" 
              required 
              placeholder="Ex: p1234567"
            />
          </div>
          
          <div class="form-group">
            <label for="email">Email <span class="required">*</span></label>
            <input 
              type="email" 
              id="email" 
              v-model="student.email" 
              required 
              placeholder="nom.prenom@etu.univ-lyon1.fr"
            />
          </div>

          <div class="form-group">
            <label>
              <input type="checkbox" v-model="student.isDelegate" />
              Délégué
            </label>
          </div>
          
          <div class="form-actions">
            <button type="button" class="cancel-btn" @click="$emit('close')">Annuler</button>
            <button type="submit" class="submit-btn" :disabled="isSubmitting">
              {{ isSubmitting ? 'Ajout en cours...' : 'Ajouter' }}
            </button>
          </div>
        </form>
        <p v-if="errorMessage" class="error-message">{{ errorMessage }}</p>
        <p v-if="successMessage" class="success-message">{{ successMessage }}</p>
      </div>
    </div>
  </div>
</template>

<script>
import { useStudentsStore } from '../../stores/studentsStore.js';
import { ref } from 'vue';

export default {
  name: 'PopUpAddStudent',
  props: {
    year: {
      type: String,
      required: true
    }
  },
  emits: ['close', 'student-added'],
  setup(props, { emit }) {
    const studentsStore = useStudentsStore();
    const student = ref({
      name: '',
      firstname: '',
      studentNumber: '',
      email: '',
      year: props.year,
      isDelegate: false
    });
    
    const isSubmitting = ref(false);
    const errorMessage = ref('');
    const successMessage = ref('');
    
    const handleSubmit = async () => {
      isSubmitting.value = true;
      errorMessage.value = '';
      
      try {
        await studentsStore.addStudent(student.value);
        successMessage.value = 'Étudiant ajouté avec succès!';
        
        setTimeout(() => {
          emit('student-added');
          emit('close');
        }, 1000);
        
      } catch (error) {
        errorMessage.value = error.message || 'Une erreur est survenue lors de l\'ajout de l\'étudiant';
      } finally {
        isSubmitting.value = false;
      }
    };
    
    return {
      student,
      isSubmitting,
      errorMessage,
      successMessage,
      handleSubmit
    };
  }
}
</script>

<style scoped>
.popup-add-student {
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
  max-width: 500px;
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
  font-size: 1.4rem;
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
}

.add-student-form {
  display: flex;
  flex-direction: column;
  gap: 15px;
}

.form-group {
  display: flex;
  flex-direction: column;
}

.form-group label {
  font-weight: 500;
  margin-bottom: 5px;
  color: #333;
}

.required {
  color: #e74c3c;
}

.form-group input {
  padding: 10px;
  border: 1px solid #ddd;
  border-radius: 4px;
  font-size: 1rem;
}

.form-group input:focus {
  outline: none;
  border-color: #4caf50;
  box-shadow: 0 0 0 2px rgba(76, 175, 80, 0.2);
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 10px;
}

.cancel-btn, .submit-btn {
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

.submit-btn {
  background-color: #4caf50;
  color: white;
}

.submit-btn:hover:not(:disabled) {
  background-color: #45a049;
}

.submit-btn:disabled {
  background-color: #cccccc;
  cursor: not-allowed;
}

.error-message {
  color: #e74c3c;
  margin-top: 15px;
  padding: 10px;
  background-color: #fdecea;
  border-radius: 4px;
  border-left: 3px solid #e74c3c;
}

.success-message {
  color: #2ecc71;
  margin-top: 15px;
  padding: 10px;
  background-color: #e7f7ee;
  border-radius: 4px;
  border-left: 3px solid #2ecc71;
}
</style>
