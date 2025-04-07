<template>
    <div class="download-container">
        <button class="download-button" @click="downloadPreset">
            <span class="download-icon">&#x2193;</span>
            Télécharger le modèle
        </button>
    </div>
</template>

<script>
export default {
    methods: {
        downloadPreset() {
            const link = document.createElement('a');
            link.href = '/presets/Import_Students_Preset.xlsx'; 
            link.download = 'Import_Students_Preset.xlsx';
            link.click();
        },
    },
};
</script>

<style scoped>
.download-container {
    margin: 15px 0;
    display: flex;
    justify-content: center;
}

.download-button {
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 10px 20px;
    background-color: #2c3e50;
    color: white;
    border: none;
    border-radius: 4px;
    cursor: pointer;
    font-weight: 500;
    transition: background-color 0.3s ease;
}

.download-button:hover {
    background-color: #1a2533;
}

.download-icon {
    margin-right: 8px;
    font-size: 1.2em;
}
</style>