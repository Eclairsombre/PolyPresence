<template>
  <div class="not-found-page">
    <div class="error-container">
      <div class="error-code">404</div>
      <h1>Page non trouvée</h1>
      <p>{{ message }}</p>
      <router-link to="/" class="home-button">
        Retour à l'accueil
      </router-link>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import { useRoute } from 'vue-router';

const route = useRoute();
const message = ref("La page que vous recherchez n'existe pas ou a été déplacée.");

onMounted(() => {
  if (route.params.message || route.query.message) {
    message.value = route.params.message || route.query.message;
  }
});
</script>

<style scoped>
.not-found-page {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 80vh;
  padding: 20px;
}

.error-container {
  background: white;
  border-radius: 8px;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.1);
  padding: 40px 30px;
  text-align: center;
  max-width: 500px;
  width: 100%;
}

.error-code {
  font-size: 8rem;
  font-weight: 700;
  color: #e74c3c;
  line-height: 1;
  margin-bottom: 20px;
}

h1 {
  color: #2c3e50;
  margin-bottom: 20px;
  font-size: 2rem;
}

p {
  color: #7f8c8d;
  margin-bottom: 30px;
  font-size: 1.1rem;
}

.home-button {
  display: inline-block;
  background-color: #3498db;
  color: white;
  padding: 12px 25px;
  border-radius: 30px;
  text-decoration: none;
  font-weight: 500;
  transition: all 0.3s;
}

.home-button:hover {
  background-color: #2980b9;
  transform: translateY(-2px);
  box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
}

@media (max-width: 600px) {
  .error-container {
    padding: 18px 4vw;
  }
  .home-button {
    width: 100%;
    padding: 10px 0;
    font-size: 1em;
  }
  .error-code {
    font-size: 4rem;
  }
}
</style>
