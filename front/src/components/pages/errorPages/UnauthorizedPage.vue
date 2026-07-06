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
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 16px;
  box-shadow: 0 4px 24px rgba(0, 0, 0, 0.06);
  padding: 48px 40px;
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
  color: #1a1a2e;
  margin-bottom: 20px;
  font-size: 2rem;
  font-weight: 700;
}

p {
  color: #6c757d;
  margin-bottom: 30px;
  font-size: 1.05rem;
}

.home-button {
  display: inline-block;
  background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
  color: #fff;
  padding: 11px 28px;
  border-radius: 10px;
  text-decoration: none;
  font-weight: 600;
  transition: all 0.2s;
}

.home-button:hover {
  transform: translateY(-1px);
  box-shadow: 0 6px 20px rgba(26, 26, 46, 0.25);
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
