<template>
  <PopUpShell
    title="Renvoyer le mail au professeur ?"
    size="sm"
    close-on-backdrop
    @close="$emit('close')"
  >
    <p class="pp-note">
      <AppIcon name="warning" :size="18" />
      <span>
        N'utilisez cette option que si l'email du professeur était mal renseigné.
        Un nouveau mail de signature sera envoyé à l'adresse actuellement
        enregistrée.
      </span>
    </p>

    <template #footer>
      <button class="pp-btn pp-btn-ghost" type="button" @click="$emit('close')">
        Annuler
      </button>
      <button class="pp-btn pp-btn-warn" type="button" @click="$emit('confirm')">
        Renvoyer le mail
      </button>
    </template>
  </PopUpShell>
</template>

<script setup>
import AppIcon from "../AppIcon.vue";
import PopUpShell from "./PopUpShell.vue";

defineEmits(["close", "confirm"]);
</script>
