<template>
  <div class="popup-overlay" @click.self="close">
    <div class="popup-content">
      <h2>Signer la session</h2>
      <form @submit.prevent="submitEdit">
        <div class="form-group">
          <label for="editName">Nom</label>
          <input id="editName" v-model="editSession.name" type="text" class="form-control" required/>
        </div>
        <div class="form-group">
          <label for="editFirstname">Prénom</label>
          <input id="editFirstname" v-model="editSession.firstname" type="text" class="form-control"  required />
        </div>
        <div class="form-group signature-zone">
          <label>Signature</label>
          <div class="signature-wrapper">
            <SignatureCreator v-bind:hideSaveButton="true" ref="signaturePad" :modelValue="editSession.signature" :width="420" :height="160" />
          </div>
        </div>
        <div class="popup-actions">
          <button type="submit" class="save-btn">Enregistrer</button>
          <button type="button" class="cancel-btn" @click="close">Annuler</button>
        </div>
      </form>
    </div>
  </div>
</template>

<script setup>
import { ref, watch } from 'vue';
import { useSessionStore } from '../../stores/sessionStore';
import SignatureCreator from '../signature/SignatureCreator.vue';

const props = defineProps({
  session: {
    type: Object,
    required: true
  }
});
const emit = defineEmits(['close', 'sessionUpdated']);

const sessionStore = useSessionStore();

const editSession = ref({
  name: '',
  firstname: '',
  signature: ''
});
const signaturePad = ref(null);

watch(
  () => props.session,
  (newSession) => {
    if (newSession) {
      editSession.value = {
        name: newSession.profName || '',
        firstname: newSession.profFirstname || '',
        signature: newSession.profSignature || ''
      };
    }
  },
  { immediate: true }
);

const submitEdit = async () => {
  const signatureData = signaturePad.value?.getSignature?.();
  if (!signatureData) {
    return;
  }
  props.session.profName = editSession.value.name;
  props.session.profFirstname = editSession.value.firstname;
  props.session.profSignature = signatureData;
  await sessionStore.updateSession(props.session);
  emit('sessionUpdated');
};

const close = () => {
  emit('close');
};
</script>

<style scoped>
.popup-overlay {
  position: fixed;
  top: 0; left: 0; right: 0; bottom: 0;
  background: rgba(0,0,0,0.3);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}
.popup-content {
  background: #fff;
  border-radius: 8px;
  padding: 30px 24px;
  min-width: 320px;
  max-width: 95vw;
  box-shadow: 0 2px 16px rgba(0,0,0,0.15);
}
.form-group {
  margin-bottom: 18px;
}
.form-group label {
  display: block;
  margin-bottom: 6px;
  font-weight: 500;
}
.form-control {
  width: 100%;
  padding: 8px 10px;
  border-radius: 4px;
  border: 1px solid #ccc;
}
.signature-zone {
  margin-bottom: 18px;
}
.signature-wrapper {
  width: 420px;
  max-width: 100%;
  margin: 0 auto;
}
.signature-wrapper :deep(canvas) {
  width: 420px !important;
  height: 160px !important;
  max-width: 100%;
  border: 1.5px solid #bfc9d1;
  border-radius: 5px;
  background: #fafdff;
  display: block;
}
.popup-actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 18px;
}
.save-btn {
  background: #3498db;
  color: #fff;
  border: none;
  padding: 8px 18px;
  border-radius: 4px;
  cursor: pointer;
}
.cancel-btn {
  background: #eee;
  color: #333;
  border: none;
  padding: 8px 18px;
  border-radius: 4px;
  cursor: pointer;
}
.save-btn:hover {
  background: #2980b9;
}
.cancel-btn:hover {
  background: #ddd;
}
</style>
