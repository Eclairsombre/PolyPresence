<template>
  <div class="admin-import-ics-page">
    <h1>Import EDT</h1>
    <ImportIcs />
  </div>
</template>

<script setup lang="ts">
import ImportIcs from '../imports/ImportIcs.vue';
</script>

<style scoped>
.admin-import-ics-page {
  max-width: 700px;
  margin: 40px auto;
  padding: 32px;
  background: #f8f9fa;
  border-radius: 12px;
  box-shadow: 0 2px 16px rgba(0,0,0,0.07);
}
</style>
