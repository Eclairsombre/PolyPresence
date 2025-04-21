<template>
    <div class="signature-display-container">
      <img 
        v-if="signatureData" 
        :src="signatureData" 
        alt="Signature de l'étudiant" 
        class="signature-image" 
      />
      <div v-else class="no-signature-placeholder">
        Aucune signature
      </div>
      <div v-if="showEditButton" class="signature-actions">
        <router-link to="/signature" class="edit-button">
          <span>Modifier</span>
        </router-link>
      </div>
    </div>
  </template>
  
  <script>
  import { defineComponent } from 'vue';
  
  export default defineComponent({
    name: 'SignatureDisplay',
    props: {
      signatureData: {
        type: String,
        default: ''
      },
      showEditButton: {
        type: Boolean,
        default: false
      }
    },
  });
  </script>
  
  <style scoped>
  .signature-display-container {
    display: flex;
    flex-direction: column;
    gap: 8px;
    align-items: flex-start;
  }
  
  .signature-image {
    max-width: 100%;
    max-height: 100px;
    border: 1px solid #ccc;
    border-radius: 4px;
    background-color: #fff;
  }
  
  .no-signature-placeholder {
    width: 100%;
    height: 60px;
    display: flex;
    align-items: center;
    justify-content: center;
    background-color: #f8f9fa;
    border: 1px dashed #ced4da;
    color: #6c757d;
    font-style: italic;
    border-radius: 4px;
  }
  
  .signature-actions {
    display: flex;
    justify-content: flex-end;
    width: 100%;
  }
  
  .edit-button {
    padding: 6px 12px;
    border: none;
    background-color: #3498db;
    color: white;
    border-radius: 4px;
    cursor: pointer;
    font-size: 0.9rem;
    display: flex;
    align-items: center;
    gap: 5px;
    text-decoration: none;
  }
  
  .edit-button:hover {
    background-color: #2980b9;
  }
  </style>