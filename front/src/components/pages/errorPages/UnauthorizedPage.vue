<template>
  <div class="unauthorized-page">
    <div class="error-container">
      <div class="error-code">403</div>
      <h1>Accès non autorisé</h1>
      <p>Vous n'avez pas les permissions nécessaires pour accéder à cette page.</p>
      <router-link to="/" class="home-button">
        Retour à l'accueil
      </router-link>
    </div>
  </div>
</template>

<style scoped>
.unauthorized-page {
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
  color: #f39c12;
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
</style>
