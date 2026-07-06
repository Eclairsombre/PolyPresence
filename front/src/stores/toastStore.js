import { defineStore } from "pinia";

let nextId = 1;

/**
 * Notifications éphémères (toasts) affichées globalement par <ToastContainer>.
 * Utilisation depuis n'importe quel composant/store :
 *   const toast = useToastStore();
 *   toast.success("Présence enregistrée");
 *   toast.error("Une erreur est survenue");
 */
export const useToastStore = defineStore("toast", {
  state: () => ({
    toasts: [],
  }),
  actions: {
    push(message, type = "info", timeout = 4500) {
      if (!message) return null;
      const id = nextId++;
      this.toasts.push({ id, message, type });
      if (timeout > 0) {
        setTimeout(() => this.remove(id), timeout);
      }
      return id;
    },
    success(message, timeout = 4000) {
      return this.push(message, "success", timeout);
    },
    error(message, timeout = 6000) {
      return this.push(message, "error", timeout);
    },
    info(message, timeout = 4500) {
      return this.push(message, "info", timeout);
    },
    remove(id) {
      this.toasts = this.toasts.filter((t) => t.id !== id);
    },
  },
});
