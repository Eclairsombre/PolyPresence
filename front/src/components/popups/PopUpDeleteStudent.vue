<template>
  <PopUpShell
    title="Supprimer l'étudiant"
    size="sm"
    close-on-backdrop
    @close="$emit('close')"
  >
    <p v-if="studentName" class="pp-text">
      Voulez-vous vraiment supprimer <strong>{{ studentName }}</strong>&nbsp;?
    </p>
    <p v-else class="pp-text">Voulez-vous vraiment supprimer cet étudiant&nbsp;?</p>

    <p class="pp-text">
      Il disparaîtra des listes et des séances à venir. Ses émargements déjà
      saisis ne sont pas effacés.
    </p>

    <template #footer>
      <button class="pp-btn pp-btn-ghost" type="button" @click="$emit('close')">
        Annuler
      </button>
      <button class="pp-btn pp-btn-danger" type="button" @click="$emit('confirm')">
        Supprimer
      </button>
    </template>
  </PopUpShell>
</template>

<script setup>
import PopUpShell from "./PopUpShell.vue";

defineProps({
  studentName: { type: String, default: "" },
});
defineEmits(["close", "confirm"]);
</script>
