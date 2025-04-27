import { useAuthStore } from "../stores/authStore";

export const requiresAuth = (to, from, next) => {
  const authStore = useAuthStore();

  if (!authStore.user) {
    next({
      name: "not-found",
      query: { message: "Veuillez vous connecter pour accéder à cette page." },
    });
  } else {
    next();
  }
};

export const requiresAdmin = async (to, from, next) => {
  const authStore = useAuthStore();

  // Si l'utilisateur n'est pas connecté
  if (!authStore.user) {
    next({
      name: "unauthorized",
    });
    return;
  }

  // Si isAdmin n'est pas encore défini, vérifier avec l'API
  if (authStore.user.isAdmin === undefined) {
    try {
      const isAdmin = await authStore.isAdmin();
      if (!isAdmin) {
        next({ name: "unauthorized" });
        return;
      }
    } catch (error) {
      console.error(
        "Erreur lors de la vérification des droits d'administrateur:",
        error
      );
      next({ name: "unauthorized" });
      return;
    }
  } else if (authStore.user.isAdmin === false) {
    next({ name: "unauthorized" });
    return;
  }

  next();
};
