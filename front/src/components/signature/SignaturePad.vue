<template>
    <div class="signature-container">
      <div v-if="!signature" class="signature-pad-container">
        <div class="signature-pad-wrapper">
          <canvas ref="signaturePad" class="signature-pad"></canvas>
        </div>
        <div class="signature-actions">
          <button class="clear-button" @click="clearSignature">Effacer</button>
          <button class="save-button" @click="saveSignature">Sauvegarder</button>
        </div>
      </div>
      <div v-else class="signature-preview">
        <img :src="signature" alt="Signature" class="signature-image" />
        <button class="reset-button" @click="resetSignature">Modifier</button>
      </div>
    </div>
  </template>
  
  <script>
  import { defineComponent, ref, onMounted, watch } from 'vue';
  import SignaturePad from 'signature_pad';
  
  export default defineComponent({
    name: 'SignaturePad',
    props: {
      initialSignature: {
        type: String,
        default: ''
      },
      studentId: {
        type: Number,
        required: true
      },
      sessionId: {
        type: Number,
        required: true
      }
    },
    emits: ['signature-saved'],
    setup(props, { emit }) {
      const signaturePad = ref(null);
      const signaturePadInstance = ref(null);
      const signature = ref(props.initialSignature || '');
  
      // Initialisation du pad de signature
      const initSignaturePad = () => {
        if (!signaturePad.value) return;
        
        signaturePadInstance.value = new SignaturePad(signaturePad.value, {
          backgroundColor: 'rgba(255, 255, 255, 0.9)',
          penColor: 'rgb(0, 0, 0)'
        });
      };
  
      // Effacer la signature
      const clearSignature = () => {
        if (signaturePadInstance.value) {
          signaturePadInstance.value.clear();
        }
      };
  
      // Sauvegarder la signature
      const saveSignature = () => {
        if (signaturePadInstance.value && !signaturePadInstance.value.isEmpty()) {
          const signatureData = signaturePadInstance.value.toDataURL();
          signature.value = signatureData;
          emit('signature-saved', {
            studentId: props.studentId,
            sessionId: props.sessionId,
            signatureData
          });
        } else {
          alert("Veuillez d'abord signer.");
        }
      };
  
      // Réinitialiser la signature pour modification
      const resetSignature = () => {
        signature.value = '';
        setTimeout(initSignaturePad, 100);
      };
  
      // Adapter la taille du canvas à son conteneur
      const resizeCanvas = () => {
        if (!signaturePad.value || !signaturePadInstance.value) return;
        
        const ratio = Math.max(window.devicePixelRatio || 1, 1);
        signaturePad.value.width = signaturePad.value.offsetWidth * ratio;
        signaturePad.value.height = signaturePad.value.offsetHeight * ratio;
        signaturePad.value.getContext("2d").scale(ratio, ratio);
        signaturePadInstance.value.clear();
      };
  
      // Initialisation au montage du composant
      onMounted(() => {
        if (!signature.value) {
          initSignaturePad();
          window.addEventListener('resize', resizeCanvas);
          resizeCanvas();
        }
      });
  
      // Surveiller les changements de signature initiale
      watch(() => props.initialSignature, (newValue) => {
        if (newValue) {
          signature.value = newValue;
        }
      });
  
      return {
        signaturePad,
        signature,
        clearSignature,
        saveSignature,
        resetSignature
      };
    }
  });
  </script>
  
  <style scoped>
  .signature-container {
    margin: 10px 0;
  }
  
  .signature-pad-container {
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
  
  .clear-button, .save-button, .reset-button {
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
  
  .save-button, .reset-button {
    background-color: #3498db;
    color: white;
  }
  
  .signature-preview {
    display: flex;
    flex-direction: column;
    gap: 10px;
    align-items: flex-end;
  }
  
  .signature-image {
    max-width: 100%;
    max-height: 100px;
    border-radius: 4px;
    border: 1px solid #ccc;
    background-color: #fff;
  }
  </style>