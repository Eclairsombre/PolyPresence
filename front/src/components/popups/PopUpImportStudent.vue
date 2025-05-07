<template>
    <div class="popup-import-student">
        <div class="popup-content">
            <div class="popup-header">
                <h2>Importer des étudiants - {{ year }}</h2>
                <button class="close-button" @click="closePopup">&times;</button>
            </div>
            <div class="popup-body">
                <p class="instruction">Pour importer des étudiants, veuillez télécharger le modèle et le remplir avec les informations requises.</p>
                <p class="warning">Attention, cette action écrasera les données existantes pour les {{ year }}.</p>
                <DownloadPreset />
                <div class="import-section">
                    <ImportStudent :year="year" :students="students" />
                </div>
            </div>
            <div class="popup-footer">
                <button class="cancel-button" @click="closePopup">Fermer</button>
            </div>
        </div>
    </div>
</template>

<script>
import ImportStudent from '../imports/ImportStudent.vue';
import DownloadPreset from '../imports/DownloadPreset.vue';

export default {
    name: 'PopUpImportStudent',
    components: {
        DownloadPreset,
        ImportStudent,
    },
    props: {
        year: {
            type: String,
            required: true,
        },
        students : {
            type: Array,
            required: true,
        },
    },
    methods: {
        closePopup() {
            this.$emit('close');
        },
    },
};
</script>

<style scoped>
.popup-import-student {
    position: fixed;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    background: rgba(0, 0, 0, 0.6);
    display: flex;
    justify-content: center;
    align-items: center;
    z-index: 1000;
    animation: fadeIn 0.3s ease-in-out;
}

@keyframes fadeIn {
    from { opacity: 0; }
    to { opacity: 1; }
}

.popup-content {
    background: white;
    width: 90%;
    max-width: 600px;
    border-radius: 8px;
    box-shadow: 0 4px 20px rgba(0, 0, 0, 0.2);
    overflow: hidden;
    animation: slideIn 0.3s ease-in-out;
}

@keyframes slideIn {
    from { transform: translateY(-30px); opacity: 0; }
    to { transform: translateY(0); opacity: 1; }
}

.popup-header {
    background: #2c3e50;
    color: white;
    padding: 15px 20px;
    display: flex;
    justify-content: space-between;
    align-items: center;
}

.popup-header h2 {
    margin: 0;
    font-size: 1.4rem;
    font-weight: 500;
}

.close-button {
    background: none;
    border: none;
    color: white;
    font-size: 1.8rem;
    cursor: pointer;
    padding: 0;
    line-height: 1;
}

.popup-body {
    padding: 20px;
}

.instruction {
    font-size: 1rem;
    color: #333;
    margin-bottom: 10px;
}

.warning {
    font-size: 0.95rem;
    color: #e74c3c;
    margin-bottom: 20px;
    padding: 10px;
    background-color: #fdecea;
    border-radius: 4px;
    border-left: 4px solid #e74c3c;
}

.import-section {
    margin-top: 20px;
}

.popup-footer {
    background: #f5f5f5;
    padding: 15px 20px;
    display: flex;
    justify-content: flex-end;
    border-top: 1px solid #e0e0e0;
}

.cancel-button {
    padding: 8px 16px;
    background-color: #6c757d;
    color: white;
    border: none;
    border-radius: 4px;
    cursor: pointer;
    transition: background-color 0.2s;
}

.cancel-button:hover {
    background-color: #5a6268;
}

@media (max-width: 600px) {
  .popup-content {
    padding: 8px 2vw;
    max-width: 98vw;
  }
  .popup-header h2 {
    font-size: 1.1rem;
  }
  .cancel-button {
    width: 100%;
    padding: 8px 0;
    font-size: 1em;
  }
}
</style>