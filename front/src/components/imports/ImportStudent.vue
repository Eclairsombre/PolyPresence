<template>
    <div class="import-container">
        <div class="file-upload-container">
            <label for="file-upload" class="file-upload-label">
                <span class="upload-icon">&#x21E7;</span>
                <span>Choisir un fichier Excel</span>
            </label>
            <input id="file-upload" type="file" @change="handleFileUpload" accept=".xlsx, .xls" />
            <span class="file-format">.xlsx, .xls</span>
        </div>
        <p v-if="successMessage" class="success-message">{{ successMessage }}</p>
    </div>
</template>

<script setup lang="ts">

import { ref } from 'vue';
import * as XLSX from 'xlsx';
import { useStudentsStore } from '../../stores/studentsStore.ts';
import type { Student } from '../../types';

const props = defineProps({
    year: {
        type: String,
        required: true,
    },
    students: {
        type: Array,
        required: true,
    },
});

const studentStore = useStudentsStore();
const successMessage = ref('');

const handleDelete = async () => {
    const deletePromises = (props.students as Student[]).map(student =>
        studentStore.deleteStudent(student.studentNumber)
            .then(() => {
                console.log(`Student ${student.name} ${student.firstname} deleted successfully.`);
            })
            .catch(error => {
                console.error(`Error deleting student ${student.name} ${student.firstname}:`, error);
            })
    );
    await Promise.all(deletePromises);
};

const handleFileUpload = async (event: Event) => {
    const fileInput = event.target as HTMLInputElement;
    if (fileInput.files && fileInput.files.length > 0) {
        const file = fileInput.files[0];
        const reader = new FileReader();
        reader.onload = async (e) => {
            const data = e.target?.result;
            if (data) {
                await handleDelete(); 
                const workbook = XLSX.read(data, { type: 'array' });
                const firstSheetName = workbook.SheetNames[0];
                const worksheet = workbook.Sheets[firstSheetName];
                const rows: (string | undefined)[][] = XLSX.utils.sheet_to_json(worksheet, { header: 1 });
                console.log(rows);
                const addPromises = rows.slice(1).map(row => {
                    if (row.length < 4) {
                        console.error('Row does not contain enough data:', row);
                        return Promise.resolve(); 
                    }
                    const student: Student = {
                        name: row[0] ?? 'Unknown Name',
                        firstname: row[1] ?? 'Unknown Firstname',
                        studentNumber: row[2] ?? 'Unknown Student Number',
                        email: row[3] ?? 'Unknown Email',
                        year: props.year,
                    };
                    return studentStore.addStudent(student)
                        .then(() => {
                            console.log(`Student ${student.name} ${student.firstname} added successfully.`);
                        })
                        .catch(error => {
                            console.error(`Error adding student ${student.name} ${student.firstname}:`, error);
                        });
                });
                await Promise.all(addPromises); 
                successMessage.value = 'All students have been successfully imported!';
            }
        };
        reader.readAsArrayBuffer(file);
    }
};
</script>

<style scoped>
.import-container {
    margin: 20px 0;
    display: flex;
    flex-direction: column;
    align-items: center;
}

.file-upload-container {
    display: flex;
    flex-direction: column;
    align-items: center;
    width: 100%;
    max-width: 400px;
}

.file-upload-label {
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 12px 24px;
    background-color: #4caf50;
    color: white;
    border-radius: 4px;
    cursor: pointer;
    font-weight: 500;
    transition: background-color 0.3s ease;
    width: 100%;
    text-align: center;
    margin-bottom: 8px;
}

.file-upload-label:hover {
    background-color: #45a049;
}

.upload-icon {
    margin-right: 10px;
    font-size: 1.2em;
}

.file-format {
    color: #666;
    font-size: 0.8em;
    margin-top: 5px;
}

input[type="file"] {
    display: none;
}

.success-message {
    margin-top: 20px;
    padding: 10px 15px;
    background-color: #e7f7ee;
    color: #28a745;
    border: 1px solid #d4edda;
    border-radius: 4px;
    font-weight: 500;
    text-align: center;
    animation: fadeIn 0.5s ease-in-out;
}

@keyframes fadeIn {
    from { opacity: 0; transform: translateY(-10px); }
    to { opacity: 1; transform: translateY(0); }
}
</style>