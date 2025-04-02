<template>
    <div>
      <div v-if="!user">
        <p>Veuillez vous connecter via CAS.</p>
        <button @click="login">Se connecter</button>
      </div>
      <div v-else>
        <p>Bienvenue, {{ user }}!</p>
        <button @click="logout">Se déconnecter</button>
      </div>
    </div>
  </template>
  
  <script>
  export default {
    data() {
      return {
        user: null, // L'utilisateur connecté
      };
    },
    methods: {
      // Appel pour initier la connexion CAS
      login() {
        window.location.href = "http://localhost:5020/login"; // Appelle l'URL de login du backend
      },
      // Déconnexion de la session
      logout() {
        this.user = null;
        // Peut-être une API pour la déconnexion côté backend ici
      }
    },
    mounted() {
      // Vérifier si l'utilisateur est déjà connecté
      fetch("http://localhost:5020/api/user")
        .then((response) => response.json())
        .then((data) => {
          if (data.success) {
            this.user = data.user;
          }
        });
    }
  };
  </script>
  