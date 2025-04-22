import axios from "axios";

const API_URL = import.meta.env.VITE_API_URL;

export const api = axios.create({
  baseURL: API_URL,
  headers: {
    "Content-Type": "application/json",
  },
});

export const userService = {
  getAll: async () => {
    const response = await api.get("/Students");
    return response.data;
  },

  getById: async (id: number) => {
    const response = await api.get(`/Students/${id}`);
    return response.data;
  },

  create: async (user: any) => {
    // Assurez-vous que l'en-tête est explicitement défini ici
    const response = await api.post("/Students", JSON.stringify(user), {
      headers: {
        "Content-Type": "application/json"
      }
    });
    return response.data;
  },

  update: async (id: number, user: any) => {
    const response = await api.put(`/Students/${id}`, JSON.stringify(user), {
      headers: {
        "Content-Type": "application/json"
      }
    });
    return response.data;
  },

  delete: async (id: number) => {
    const response = await api.delete(`/Students/${id}`);
    return response.data;
  },
};