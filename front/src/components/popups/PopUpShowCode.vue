<template>
  <PopUpShell title="Déflouter le code" size="sm" close-on-backdrop @close="$emit('close')">
    <p class="pp-note">
      <AppIcon name="warning" :size="18" />
      <span>
        Ne défloutez le code que s'il y a un problème avec l'envoi du mail, un
        problème avec le professeur, ou si la session est du travail personnel.
      </span>
    </p>

    <template #footer>
      <button class="pp-btn pp-btn-ghost" type="button" @click="$emit('close')">
        Annuler
      </button>
      <button class="pp-btn pp-btn-primary" type="button" @click="$emit('confirm')">
        Déflouter
      </button>
    </template>
  </PopUpShell>
</template>

<script setup>
import AppIcon from "../AppIcon.vue";
import PopUpShell from "./PopUpShell.vue";

defineEmits(["close", "confirm"]);
</script>
