<template>
    <div class="signature-display-container">
      <img 
        v-if="signatureData && signatureData != ' '" 
        :src="signatureData" 
        alt="Signature de l'étudiant" 
        :class="['signature-image', { 'signature-image-small': inAttendanceList }]" 
      />
      <div v-else :class="['no-signature-placeholder', { 'no-signature-placeholder-small': inAttendanceList }]">
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
      },
      inAttendanceList: {
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
    gap: 10px;
    align-items: center;
  }
  
  .signature-image {
    max-width: 100%;
    max-height: 180px;
    border: 1px solid #e0e4ea;
    border-radius: 10px;
    background-color: #fff;
  }
  
  .no-signature-placeholder {
    width: 100%;
    height: 100px;
    display: flex;
    align-items: center;
    justify-content: center;
    background: #f8f9fb;
    border: 2px dashed #d1d5db;
    color: #6c757d;
    font-style: italic;
    font-size: 0.9rem;
    border-radius: 10px;
  }
  
  .signature-actions {
    display: flex;
    justify-content: flex-end;
    width: 100%;
  }
  
  .edit-button {
    padding: 8px 16px;
    border: none;
    background: #3498db;
    color: white;
    border-radius: 8px;
    cursor: pointer;
    font-size: 0.88rem;
    font-weight: 600;
    display: flex;
    align-items: center;
    gap: 5px;
    text-decoration: none;
    transition: all 0.2s;
  }
  
  .edit-button:hover {
    background: #2980b9;
    box-shadow: 0 2px 8px rgba(52, 152, 219, 0.25);
  }

  .signature-image-small {
    max-height: 80px !important;
    border: 1px solid #e0e4ea;
  }

  .no-signature-placeholder-small {
    height: 50px;
    font-size: 0.8rem;
  }
  </style>

  .no-signature-placeholder-small {
    height: 50px !important; /* Taille réduite pour les placeholders dans les listes */
    font-size: 0.9em;
  }

  @media (max-width: 600px) {
    .signature-image {
      max-height: 120px;
    }
    .no-signature-placeholder {
      height: 80px;
      font-size: 0.95em;
    }
    .signature-image-small {
      max-height: 60px !important; /* Encore plus petit sur mobile */
    }
    .no-signature-placeholder-small {
      height: 40px !important; /* Encore plus petit sur mobile */
      font-size: 0.85em;
    }
    .edit-button {
      width: 100%;
      padding: 8px 0;
      font-size: 1em;
    }
  }
  </style>