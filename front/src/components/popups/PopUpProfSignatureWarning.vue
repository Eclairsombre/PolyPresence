<template>
  <PopUpShell
    title="Validation de la signature"
    size="sm"
    close-on-backdrop
    @close="$emit('close')"
  >
    <p class="pp-note">
      <AppIcon name="warning" :size="18" />
      <span>
        Dès que la signature sera validée, <b>les élèves ne pourront plus se
        mettre présents pour cette session.</b>
      </span>
    </p>
    <p class="pp-text">Êtes-vous sûr de vouloir continuer&nbsp;?</p>

    <template #footer>
      <button class="pp-btn pp-btn-ghost" type="button" @click="$emit('close')">
        Annuler
      </button>
      <button class="pp-btn pp-btn-primary" type="button" @click="$emit('confirm')">
        Oui, valider la signature
      </button>
    </template>
  </PopUpShell>
</template>

<script setup>
import AppIcon from "../AppIcon.vue";
import PopUpShell from "./PopUpShell.vue";

defineEmits(["close", "confirm"]);
</script>
