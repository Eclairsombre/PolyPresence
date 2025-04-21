<template>
    <div class="signature-creator-container">
      <div class="signature-pad-wrapper">
        <canvas ref="signaturePad" class="signature-pad"></canvas>
      </div>
      <div class="signature-actions">
        <button class="clear-button" @click="clearSignature">Effacer</button>
        <button class="save-button" @click="saveSignature" :disabled="isEmpty">Sauvegarder</button>
      </div>
    </div>
  </template>
  
  <script>
  import { defineComponent, ref, onMounted } from 'vue';
  import SignaturePad from 'signature_pad';
  import { useSessionStore } from '../../stores/sessionStore';
  import { useAuthStore } from '../../stores/authStore';
  
  export default defineComponent({
    name: 'SignatureCreator',
    
    setup(props, { emit }) {
      const signaturePad = ref(null);
      const signaturePadInstance = ref(null);
      const isEmpty = ref(true);
      const sessionStore = useSessionStore();
      const authStore = useAuthStore();
      
      const initSignaturePad = () => {
        if (!signaturePad.value) return;
        
        signaturePadInstance.value = new SignaturePad(signaturePad.value, {
          backgroundColor: 'rgba(255, 255, 255, 0.9)',
          penColor: 'rgb(0, 0, 0)'
        });
        
        signaturePadInstance.value.addEventListener('endStroke', () => {
          isEmpty.value = signaturePadInstance.value.isEmpty();
        });
      };
  
      const clearSignature = () => {
        if (signaturePadInstance.value) {
          signaturePadInstance.value.clear();
          isEmpty.value = true;
        }
      };
  
      const saveSignature = async () => {
        if (!signaturePadInstance.value || signaturePadInstance.value.isEmpty()) {
          alert("Veuillez d'abord signer.");
          return;
        }
  
        try {
          const signatureData = signaturePadInstance.value.toDataURL();
          const studentNumber = authStore.user.studentId;
          
          const response = await sessionStore.saveSignature(studentNumber, signatureData);
          console.log("Réponse de la sauvegarde de la signature:", response);
          
          emit('signatureSaved'); 
          
        } catch (err) {
          console.error("Erreur lors de la sauvegarde de la signature:", err);
          alert("Une erreur est survenue lors de la sauvegarde de votre signature.");
        }
      };
  
      const resizeCanvas = () => {
        if (!signaturePad.value || !signaturePadInstance.value) return;
        
        const ratio = Math.max(window.devicePixelRatio || 1, 1);
        signaturePad.value.width = signaturePad.value.offsetWidth * ratio;
        signaturePad.value.height = signaturePad.value.offsetHeight * ratio;
        signaturePad.value.getContext("2d").scale(ratio, ratio);
        
        const data = signaturePadInstance.value.toData();
        signaturePadInstance.value.clear();
        if (data && data.length) {
          signaturePadInstance.value.fromData(data);
          isEmpty.value = false;
        } else {
          isEmpty.value = true;
        }
      };
  
      onMounted(() => {
        initSignaturePad();
        window.addEventListener('resize', resizeCanvas);
        resizeCanvas();
        
        return () => {
          window.removeEventListener('resize', resizeCanvas);
        };
      });
  
      return {
        signaturePad,
        clearSignature,
        saveSignature,
        isEmpty
      };
    }
  });
  </script>
  
  <style scoped>
  .signature-creator-container {
    margin: 10px 0;
    display: flex;
    flex-direction: column;
    gap: 10px;
  }
  
  .signature-pad-wrapper {
    border: 1px solid #ccc;
    border-radius: 4px;
    background-color: #fff;
    position: relative;
  }
  
  .signature-pad {
    width: 100%;
    height: 100px;
    touch-action: none;
  }
  
  .signature-actions {
    display: flex;
    gap: 10px;
    justify-content: flex-end;
  }
  
  .clear-button, .save-button {
    padding: 6px 12px;
    border-radius: 4px;
    border: none;
    cursor: pointer;
    font-size: 0.9rem;
  }
  
  .clear-button {
    background-color: #f8f9fa;
    color: #495057;
    border: 1px solid #ced4da;
  }
  
  .save-button {
    background-color: #3498db;
    color: white;
  }
  
  .save-button:disabled {
    background-color: #a0cbeb;
    cursor: not-allowed;
  }
  </style>