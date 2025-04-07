<template>
  <div class="home-page">
    <h1>Bienvenue sur PolyPresence</h1>
    <div class="home-content">
      <p>Système de gestion de présence pour Polytech</p>
      <div class="actions">
        <router-link to="/students" class="btn-primary">Voir la liste des étudiants</router-link>
      </div>
    </div>
    <div class="modules">
      <Auth />
    </div>
  </div>
</template>

<script>
import Auth from '../Auth.vue';

export default {
  name: 'HomePage',
  components: {
    Auth,
  }
}
</script>

<style scoped>
.home-page {
  width: 100%;
  max-width: 1200px;
  margin: 0 auto;
  padding: 20px;
}

.home-content {
  text-align: center;
  margin: 40px 0;
}

.actions {
  margin-top: 30px;
}

.btn-primary {
  display: inline-block;
  background-color: #2c3e50;
  color: white;
  padding: 12px 24px;
  border-radius: 4px;
  text-decoration: none;
  font-weight: bold;
  transition: background-color 0.3s;
}

.btn-primary:hover {
  background-color: #1a2533;
}

.modules {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 20px;
  margin-top: 40px;
}
</style>
