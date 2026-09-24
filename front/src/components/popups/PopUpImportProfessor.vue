<template>
  <PopUpShell title="Importer des professeurs" size="md" @close="$emit('close')">
    <p class="pp-text">
      Le fichier doit contenir trois colonnes : <strong>Nom</strong>,
      <strong>Prénom</strong> et <strong>Email</strong>.
    </p>
    <p class="pp-note">
      <span>
        Chaque ligne est rapprochée d'un professeur existant sur le nom et le
        prénom, sans tenir compte de la casse ni des accents — un professeur créé
        par l'import de l'emploi du temps, écrit en majuscules par ADE, est donc
        bien retrouvé. Les inconnus sont créés. Un professeur absent du fichier
        n'est jamais supprimé.
      </span>
    </p>

    <ImportProfessor @imported="$emit('imported')" />

    <template #footer>
      <button class="pp-btn pp-btn-ghost" type="button" @click="$emit('close')">
        Fermer
      </button>
    </template>
  </PopUpShell>
</template>

<script setup>
import ImportProfessor from "../imports/ImportProfessor.vue";
import PopUpShell from "./PopUpShell.vue";

defineEmits(["close", "imported"]);
</script>
