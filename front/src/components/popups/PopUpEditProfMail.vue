<template>
  <PopUpShell title="Modifier l'email du professeur" size="sm" @close="$emit('close')">
    <p class="pp-note">
      <AppIcon name="warning" :size="18" />
      <span>
        N'utilisez cette option que si l'email du professeur était mal renseigné.
        Un mail de signature sera envoyé à la nouvelle adresse.
      </span>
    </p>

    <form id="edit-prof-mail-form" class="pp-field" @submit.prevent="save">
      <label for="prof-new-mail">Nouvel email</label>
      <input
        id="prof-new-mail"
        v-model.trim="inputValue"
        type="email"
        required
        placeholder="professeur@univ-lyon1.fr"
      />
    </form>

    <p v-if="error" class="pp-error">{{ error }}</p>

    <template #footer>
      <button class="pp-btn pp-btn-ghost" type="button" @click="$emit('close')">
        Annuler
      </button>
      <button class="pp-btn pp-btn-primary" type="submit" form="edit-prof-mail-form">
        Enregistrer
      </button>
    </template>
  </PopUpShell>
</template>

<script setup>
import { ref, watch } from "vue";
import AppIcon from "../AppIcon.vue";
import PopUpShell from "./PopUpShell.vue";

const props = defineProps({
  value: String,
});

const emit = defineEmits(["close", "save"]);

const inputValue = ref(props.value || "");
const error = ref("");

watch(
  () => props.value,
  (val) => {
    inputValue.value = val ?? "";
  },
);

const save = () => {
  if (!inputValue.value) {
    error.value = "L'email ne peut pas être vide.";
    return;
  }
  if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(inputValue.value)) {
    error.value = "Format d'email invalide.";
    return;
  }
  error.value = "";
  emit("save", inputValue.value);
};
</script>
