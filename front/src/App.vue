<template>
  <div>
    <div v-if="!user">
      <p>Veuillez vous connecter via CAS.</p>
      <button @click="login">Se connecter</button>
    </div>
    <div v-else>
      <p>Bienvenue, {{ user.studentId }}!</p>
      <button @click="logout">Se déconnecter</button>
      
      <div v-if="debugData" class="debug-section">
        <h3>Données de débogage:</h3>
        <pre>{{ JSON.stringify(debugData, null, 2) }}</pre>
      </div>
    </div>
  </div>
</template>
  
<script>
import axios from 'axios';

export default {
  data() {
    return {
      user: null, 
      debugData: null, 
    };
  },
  created() {
    const urlParams = new URLSearchParams(window.location.search);
    const ticket = urlParams.get('ticket');
    
    if (ticket) {
      this.processTicket(ticket);
    } else {
      this.checkSession();
    }
  },
  methods: {
    login() {
      window.location.href = "http://localhost:5020/login";
    },
    
    logout() {
      this.user = null;
      this.debugData = null;
      localStorage.removeItem('user');
      window.location.href = "http://localhost:5020/logout";
    },
    
    async processTicket(ticket) {
      try {
        const response = await axios.get(`http://localhost:5020/callback?ticket=${ticket}`);
        
        this.debugData = response.data;
        
        if (response.data.success) {
          if (response.data.rawResponse) {
            const data = this.parseRawData(response.data.rawResponse);
            console.log("Données brutes:", data);
            this.user = {
              studentId: data.user,
              firstname: data.firstname || 'N/A',
              lastname: data.lastname || 'N/A',
              email: data.email || 'N/A',
            };
          }
          
          localStorage.setItem('user', JSON.stringify(this.user));
          window.history.replaceState({}, document.title, window.location.pathname);
        } else {
          console.error("Échec de l'authentification:", response.data.message);
        }
      } catch (error) {
        console.error("Erreur lors du traitement du ticket:", error);
        this.debugData = {
          error: error.message,
          response: error.response?.data
        };
      }
    },

    parseRawData(rawData) {
      if (!rawData) return { user: null, firstname: 'N/A', lastname: 'N/A', email: 'N/A' };
      
      const lines = rawData.split("\n");
      const data = {};

      lines.forEach(line => {
        const match = line.match(/<cas:(\w+)>(.*?)<\/cas:\1>/);
        if (match) {
          const key = match[1];
          const value = match[2];
          data[key] = value;
        }
      });

      return {
        user: data["user"] || null,
        firstname: data["firstname"] || "N/A",
        lastname: data["name"] || "N/A",
        email: data["email"] || "N/A",
      };
    },
    
    checkSession() {
      const savedUser = localStorage.getItem('user');
      if (savedUser) {
        try {
          this.user = JSON.parse(savedUser);
        } catch (e) {
          console.error("Erreur lors de la récupération de l'utilisateur depuis localStorage:", e);
          localStorage.removeItem('user');
        }
      }
    },
  },
};
</script>

<style scoped>
.debug-section {
  margin-top: 30px;
  padding: 15px;
  background-color: #f5f5f5;
  border-radius: 5px;
}

pre {
  white-space: pre-wrap;
  word-break: break-all;
  max-height: 300px;
  overflow: auto;
}
</style>