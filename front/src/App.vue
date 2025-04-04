<template>
  <div class="app-container">
    <header class="app-header">
      <h1>PolyPresence</h1>
    </header>
    <main class="app-content">
      <Auth />
      <ImportStudent />
    </main>
    <footer class="app-footer">
      <p>&copy; 2025 PolyPresence</p>
    </footer>
  </div>
</template>
  
<script>
import Auth from './components/Auth.vue';
import ImportStudent from './components/ImportStudent.vue';

export default {
  components: {
    Auth,
    ImportStudent
  }
};
</script>

<style>
/* Style global */
* {
  box-sizing: border-box;
  margin: 0;
  padding: 0;
}

body {
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
  line-height: 1.6;
  color: #333;
  background-color: #f5f7fa;
}

/* Styles spécifiques à l'application */
.app-container {
  display: flex;
  flex-direction: column;
  min-height: 100vh;
}

.app-header {
  background-color: #2c3e50;
  color: white;
  padding: 15px 20px;
  text-align: center;
  box-shadow: 0 2px 5px rgba(0, 0, 0, 0.1);
}

.app-header h1 {
  font-size: 1.8rem;
  font-weight: 500;
}

.app-content {
  flex: 1;
  padding: 20px;
  display: flex;
  justify-content: center;
  align-items: flex-start;
  margin-top: 20px;
}

.app-footer {
  background-color: #2c3e50;
  color: white;
  text-align: center;
  padding: 15px;
  font-size: 0.9rem;
  margin-top: auto;
}

/* Style des débogages réutilisable */
.debug-section {
  margin-top: 30px;
  padding: 15px;
  background-color: #f8f9fa;
  border-radius: 5px;
  box-shadow: inset 0 0 5px rgba(0, 0, 0, 0.05);
}

pre {
  white-space: pre-wrap;
  word-break: break-all;
  max-height: 300px;
  overflow: auto;
  background-color: #eee;
  padding: 10px;
  border-radius: 4px;
  font-size: 0.9rem;
}

/* Media queries pour la responsivité */
@media (max-width: 768px) {
  .app-header h1 {
    font-size: 1.5rem;
  }
  
  .app-content {
    padding: 15px;
  }
}
</style>