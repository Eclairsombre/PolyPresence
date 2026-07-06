import { defineStore } from "pinia";

/**
 * Modale de confirmation unique, pilotée par une promesse.
 * Utilisation :
 *   const confirmer = useConfirmStore();
 *   if (await confirmer.ask({ message: "Supprimer Jean Dupont ?", danger: true })) { ... }
 */
export const useConfirmStore = defineStore("confirm", {
  state: () => ({
    open: false,
    title: "Confirmation",
    message: "",
    confirmLabel: "Confirmer",
    cancelLabel: "Annuler",
    danger: false,
    _resolve: null,
  }),
  actions: {
    ask({
      title = "Confirmation",
      message = "",
      confirmLabel = "Confirmer",
      cancelLabel = "Annuler",
      danger = false,
    } = {}) {
      // Résout une éventuelle demande précédente restée ouverte.
      if (this._resolve) this._resolve(false);
      this.title = title;
      this.message = message;
      this.confirmLabel = confirmLabel;
      this.cancelLabel = cancelLabel;
      this.danger = danger;
      this.open = true;
      return new Promise((resolve) => {
        this._resolve = resolve;
      });
    },
    _settle(result) {
      this.open = false;
      const resolve = this._resolve;
      this._resolve = null;
      if (resolve) resolve(result);
    },
    confirm() {
      this._settle(true);
    },
    cancel() {
      this._settle(false);
    },
  },
});
