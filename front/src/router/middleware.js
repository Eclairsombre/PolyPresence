import { useAuthStore } from "../stores/authStore";

export const requiresAuth = (to, from, next) => {
  const authStore = useAuthStore();

  if (!authStore.isAuthenticated()) {
    next({
      name: "unauthorized",
      query: { message: "Veuillez vous connecter pour accéder à cette page." },
    });
  } else {
    next();
  }
};

export const requiresAdmin = async (to, from, next) => {
  const authStore = useAuthStore();

  if (!authStore.isAuthenticated()) {
    next({
      name: "unauthorized",
      query: { message: "Veuillez vous connecter pour accéder à cette page." },
    });
    return;
  }

  if (authStore.user.isAdmin === undefined) {
    try {
      const isAdmin = await authStore.isAdmin();
      if (!isAdmin) {
        next({ 
          name: "unauthorized",
          query: { message: "Vous n'avez pas les droits d'administrateur nécessaires." },
        });
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
    next({ 
      name: "unauthorized",
      query: { message: "Vous n'avez pas les droits d'administrateur nécessaires." },
    });
    return;
  }

  next();
};
