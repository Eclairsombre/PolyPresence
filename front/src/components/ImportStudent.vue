<template>
    <div>
        <h2>Import Student Excel File</h2>
        <input type="file" @change="handleFileUpload" accept=".xlsx, .xls" />
    </div>
</template>

<script setup lang="ts">

import * as XLSX from 'xlsx';
import { useStudentsStore } from '../stores/studentsStore.ts';
import type { Student } from '../types';

const studentStore = useStudentsStore();

const handleFileUpload = (event: Event) => {
    const fileInput = event.target as HTMLInputElement;
    if (fileInput.files && fileInput.files.length > 0) {
        const file = fileInput.files[0];
        const reader = new FileReader();
        reader.onload = (e) => {
            const data = e.target?.result;
            if (data) {
                const workbook = XLSX.read(data, { type: 'array' });
                const firstSheetName = workbook.SheetNames[0];
                const worksheet = workbook.Sheets[firstSheetName];
                const rows: (string | undefined)[][] = XLSX.utils.sheet_to_json(worksheet, { header: 1 });
                console.log(rows);
                for (let i = 1; i < rows.length; i++) { 
                    const student : Student = {
                        name: rows[i][0] ?? 'Unknown Name',
                        firstname: rows[i][1] ?? 'Unknown Firstname',
                        studentNumber: rows[i][2] ?? 'Unknown Student Number',
                        email: rows[i][3] ?? 'Unknown Email',
                    };
                    studentStore.addStudent(student).then(() => {
                        console.log(`Student ${student.name} ${student.firstname} added successfully.`);
                    }).catch((error: unknown) => {
                        console.error(`Error adding student ${student.name} ${student.firstname}:`, error);
                    });
                }
            }
        };
        reader.readAsArrayBuffer(file);
    }
};
</script>