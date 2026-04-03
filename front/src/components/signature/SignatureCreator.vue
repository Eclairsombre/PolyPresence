<template>
  <div class="signature-creator-container">
    <div class="signature-pad-wrapper">
      <div class="signature-pad-inner">
        <canvas ref="signaturePad" class="signature-pad"></canvas>
      </div>
    </div>
    <div class="signature-actions">
      <button class="clear-button" @click.prevent="clearSignature">
        Effacer
      </button>
      <button
        v-if="!$attrs.hideSaveButton"
        class="save-button"
        @click="saveSignature"
        :disabled="isEmpty"
      >
        Sauvegarder
      </button>
    </div>
  </div>
</template>

<script>
import { defineComponent, ref, onMounted } from "vue";
import SignaturePad from "signature_pad";
import { useSessionStore } from "../../stores/sessionStore";
import { useAuthStore } from "../../stores/authStore";

export default defineComponent({
  name: "SignatureCreator",

  setup(props, { emit }) {
    const signaturePad = ref(null);
    const signaturePadInstance = ref(null);
    const isEmpty = ref(true);
    const sessionStore = useSessionStore();
    const authStore = useAuthStore();

    const initSignaturePad = () => {
      if (!signaturePad.value) return;

      const canvas = signaturePad.value;
      const rect = canvas.getBoundingClientRect();
      const ratio = Math.max(window.devicePixelRatio || 1, 1);

      canvas.width = rect.width * ratio;
      canvas.height = rect.height * ratio;

      const ctx = canvas.getContext("2d");
      ctx.scale(ratio, ratio);

      ctx.clearRect(0, 0, canvas.width, canvas.height);

      if (signaturePadInstance.value) {
        signaturePadInstance.value.off();
      }

      signaturePadInstance.value = new SignaturePad(canvas, {
        backgroundColor: "rgba(255, 255, 255, 0)",
        penColor: "rgb(0, 0, 0)",
        dotSize: 2,
        minWidth: 1,
        maxWidth: 2.5,
        velocityFilterWeight: 0.4,
        throttle: 16,
      });

      signaturePadInstance.value.addEventListener("endStroke", () => {
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

        const response = await sessionStore.saveSignature(
          studentNumber,
          signatureData,
        );

        emit("signatureSaved");
      } catch (err) {
        console.debug("Erreur lors de la sauvegarde de la signature:", err);
        alert(
          "Une erreur est survenue lors de la sauvegarde de votre signature.",
        );
      }
    };

    const getSignature = () => {
      if (!signaturePadInstance.value || signaturePadInstance.value.isEmpty())
        return "";
      return signaturePadInstance.value.toDataURL();
    };

    const resizeCanvas = () => {
      if (!signaturePad.value || !signaturePadInstance.value) return;

      const data = signaturePadInstance.value.toData();

      const canvas = signaturePad.value;
      const ctx = canvas.getContext("2d");

      const rect = canvas.getBoundingClientRect();
      const ratio = Math.max(window.devicePixelRatio || 1, 1);

      canvas.width = rect.width * ratio;
      canvas.height = rect.height * ratio;

      ctx.scale(ratio, ratio);

      if (signaturePadInstance.value) {
        signaturePadInstance.value.off();
      }

      signaturePadInstance.value.clear();

      signaturePadInstance.value = new SignaturePad(canvas, {
        backgroundColor: "rgba(255, 255, 255, 0)",
        penColor: "rgb(0, 0, 0)",
        dotSize: 2,
        minWidth: 1,
        maxWidth: 2.5,
        velocityFilterWeight: 0.4,
        throttle: 16,
      });

      signaturePadInstance.value.addEventListener("endStroke", () => {
        isEmpty.value = signaturePadInstance.value.isEmpty();
      });

      if (data && data.length) {
        signaturePadInstance.value.fromData(data);
        isEmpty.value = false;
      } else {
        isEmpty.value = true;
      }
    };

    onMounted(() => {
      // Attendre que le DOM soit complètement chargé avant d'initialiser
      setTimeout(() => {
        initSignaturePad();
        setTimeout(resizeCanvas, 50);
      }, 100);

      const resizeObserver = new ResizeObserver(() => {
        setTimeout(resizeCanvas, 100);
      });

      if (signaturePad.value) {
        resizeObserver.observe(signaturePad.value.parentElement.parentElement);
      }

      const handleResize = () => {
        setTimeout(resizeCanvas, 200);
      };

      window.addEventListener("resize", handleResize);

      window.addEventListener("orientationchange", () => {
        setTimeout(forceCanvasReset, 300);
      });

      return () => {
        window.removeEventListener("resize", handleResize);
        window.removeEventListener("orientationchange", forceCanvasReset);
        if (resizeObserver && signaturePad.value) {
          resizeObserver.unobserve(
            signaturePad.value.parentElement.parentElement,
          );
          resizeObserver.disconnect();
        }
        if (signaturePadInstance.value) {
          signaturePadInstance.value.off();
        }
      };
    });

    const forceCanvasReset = () => {
      if (signaturePadInstance.value) {
        signaturePadInstance.value.off();
        signaturePadInstance.value = null;
      }

      setTimeout(() => {
        initSignaturePad();
        resizeCanvas();
      }, 50);
    };

    return {
      signaturePad,
      clearSignature,
      saveSignature,
      isEmpty,
      getSignature,
      forceCanvasReset,
    };
  },
});
</script>

<style scoped>
.signature-creator-container {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.signature-pad-wrapper {
  border: 1px solid #d1d5db;
  border-radius: 10px;
  background: #fafbfc;
  position: relative;
  width: 100%;
  height: 200px;
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: visible;
  z-index: 1;
  transition: border-color 0.2s;
}

.signature-pad-wrapper:hover {
  border-color: #adb5bd;
}

.signature-pad-inner {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  touch-action: none;
  z-index: 5;
}

.signature-pad {
  position: absolute;
  top: 0;
  left: 0;
  width: 100% !important;
  height: 100% !important;
  touch-action: none;
  box-sizing: border-box;
  z-index: 10;
  background: transparent;
  cursor: crosshair;
  -webkit-user-select: none;
  -moz-user-select: none;
  -ms-user-select: none;
  user-select: none;
}

.signature-actions {
  display: flex;
  gap: 10px;
  justify-content: flex-end;
}

.clear-button,
.save-button {
  padding: 8px 16px;
  border-radius: 8px;
  border: none;
  cursor: pointer;
  font-size: 0.88rem;
  font-weight: 600;
  transition: all 0.2s;
}

.clear-button {
  background: #f8f9fb;
  color: #495057;
  border: 1px solid #d1d5db;
}

.clear-button:hover {
  background: #e8ecf1;
  border-color: #adb5bd;
}

.save-button {
  background: #3498db;
  color: white;
}

.save-button:hover:not(:disabled) {
  background: #2980b9;
  box-shadow: 0 2px 8px rgba(52, 152, 219, 0.25);
}

.save-button:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

@media (max-width: 600px) {
  .signature-pad-wrapper {
    height: 150px;
  }
  .signature-pad {
    height: 100% !important;
    width: 100% !important;
    position: relative;
    display: block;
    transform: translateZ(0);
  }
}
</style>
