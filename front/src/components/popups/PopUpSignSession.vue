<template>
  <PopUpShell title="Signer la session" size="md" :busy="isSubmitting" @close="close">
    <form id="sign-session-form" @submit.prevent="submitEdit">
      <div class="pp-row">
        <div class="pp-field">
          <label for="editFirstname">Prénom</label>
          <input id="editFirstname" v-model="editSession.firstname" type="text" required />
        </div>
        <div class="pp-field">
          <label for="editName">Nom</label>
          <input id="editName" v-model="editSession.name" type="text" required />
        </div>
      </div>

      <div class="pp-field">
        <label>Signature</label>
        <div class="signature-wrapper">
          <SignatureCreator
            ref="signaturePad"
            :hideSaveButton="true"
            :modelValue="editSession.signature"
            :width="420"
            :height="160"
          />
        </div>
        <p v-if="error" class="pp-hint error-hint">{{ error }}</p>
      </div>
    </form>

    <template #footer>
      <button class="pp-btn pp-btn-ghost" type="button" :disabled="isSubmitting" @click="close">
        Annuler
      </button>
      <button
        class="pp-btn pp-btn-primary"
        type="submit"
        form="sign-session-form"
        :disabled="isSubmitting"
      >
        {{ isSubmitting ? "Enregistrement…" : "Enregistrer" }}
      </button>
    </template>
  </PopUpShell>
</template>

<script setup>
import { ref, watch } from "vue";
import { useSessionStore } from "../../stores/sessionStore";
import { useProfessorStore } from "../../stores/professorStore";
import SignatureCreator from "../signature/SignatureCreator.vue";
import PopUpShell from "./PopUpShell.vue";

const props = defineProps({
  session: {
    type: Object,
    required: true,
  },
});
const emit = defineEmits(["close", "sessionUpdated"]);

const sessionStore = useSessionStore();
const professorStore = useProfessorStore();

const editSession = ref({
  name: "",
  firstname: "",
  signature: "",
});
const professor = ref(null);
const signaturePad = ref(null);
const isSubmitting = ref(false);
const error = ref("");

watch(
  () => props.session,
  async (newSession) => {
    if (newSession && newSession.profId) {
      professor.value = await professorStore.fetchProfessorById(newSession.profId);
      editSession.value = {
        name: professor.value?.name || "",
        firstname: professor.value?.firstname || "",
        signature: newSession.profSignature || "",
      };
    } else if (newSession) {
      editSession.value = {
        name: newSession.profName || "",
        firstname: newSession.profFirstname || "",
        signature: newSession.profSignature || "",
      };
    }
  },
  { immediate: true },
);

const submitEdit = async () => {
  const signatureData = signaturePad.value?.getSignature?.();
  if (!signatureData) {
    // Avant, l'absence de signature faisait un retour silencieux : le bouton
    // semblait ne rien faire et personne ne savait ce qu'il manquait.
    error.value = "Tracez la signature avant d'enregistrer.";
    return;
  }

  error.value = "";
  isSubmitting.value = true;
  try {
    let profId = props.session.profId;
    if (!profId) {
      profId = await professorStore.findOrCreateProfessor({
        name: editSession.value.name,
        firstname: editSession.value.firstname,
        email: professor.value?.email || "",
      });
      if (!profId) {
        error.value = "Le professeur n'a pas pu être enregistré.";
        return;
      }
    }

    props.session.profId = profId.toString();
    props.session.profSignature = signatureData;
    await sessionStore.updateSession(props.session);
    emit("sessionUpdated");
  } catch (e) {
    error.value =
      e?.response?.data?.message || e?.message || "L'enregistrement a échoué.";
  } finally {
    isSubmitting.value = false;
  }
};

const close = () => emit("close");
</script>

<style scoped>
.signature-wrapper {
  border: 1px solid #d7dce1;
  border-radius: 5px;
  padding: 8px;
  background: #fbfcfd;
  display: flex;
  justify-content: center;
  overflow-x: auto;
}

.error-hint {
  color: #a3372e;
  font-weight: 500;
}
</style>
