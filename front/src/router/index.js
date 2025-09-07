import { createRouter, createWebHistory } from "vue-router";
import { requiresAdmin, requiresAuth } from "./middleware";

const routes = [
  {
    path: "/",
    name: "home",
    component: () => import("../components/pages/HomePage.vue"),
  },
  {
    path: "/students",
    name: "students",
    component: () => import("../components/pages/StudentsListPage.vue"),
    beforeEnter: requiresAdmin,
  },
  {
    path: "/sessions",
    name: "sessions",
    component: () => import("../components/pages/SessionListPage.vue"),
    beforeEnter: requiresAdmin,
  },
  {
    path: "/sessions/:id",
    name: "SessionAttendance",
    component: () => import("../components/pages/ListAttendancePerSession.vue"),
    beforeEnter: requiresAdmin,
  },
  {
    path: "/signature",
    name: "signature",
    component: () => import("../components/pages/SignaturePage.vue"),
    beforeEnter: requiresAuth,
  },
  {
    path: "/mail-preferences",
    name: "MailPreferences",
    component: () => import("../components/pages/MailPreferencesPage.vue"),
    beforeEnter: requiresAdmin,
  },
  {
    path: "/unauthorized",
    name: "unauthorized",
    component: () =>
      import("../components/pages/errorPages/UnauthorizedPage.vue"),
  },
  {
    path: "/not-found",
    name: "not-found",
    component: () => import("../components/pages/errorPages/NotFoundPage.vue"),
  },
  {
    path: "/prof-signature/:token",
    component: () => import("../components/pages/ProfSignaturePage.vue"),
  },
  {
    path: "/:pathMatch(.*)*",
    name: "catch-all",
    component: () => import("../components/pages/errorPages/NotFoundPage.vue"),
  },
  {
    path: "/admin/import-edt",
    name: "AdminImportIcs",
    component: () => import("../components/pages/AdminImportIcsPage.vue"),
    meta: { requiresAdmin: true },
  },
  {
    path: "/login",
    name: "login",
    component: () => import("../components/pages/LoginPage.vue"),
  },
  {
    path: "/register",
    name: "register",
    component: () => import("../components/pages/RegisterPage.vue"),
  },
  {
    path: "/set-password",
    name: "set-password",
    component: () => import("../components/pages/SetPasswordPage.vue"),
  },
  {
    path: "/reset-password",
    name: "reset-password",
    component: () => import("../components/pages/SetPasswordPage.vue"),
  },
  {
    path: "/forgot-password",
    name: "forgot-password",
    component: () => import("../components/pages/ForgotPasswordPage.vue"),
  },
];

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
});

import { useAuthStore } from "../stores/authStore";
router.beforeEach(async (to, from, next) => {

  const authStore = useAuthStore();
  await authStore.initialize();

  const publicPages = [
    "login",
    "register",
    "forgot-password",
    "set-password",
    "reset-password",
    "unauthorized",
    "not-found",
  ];
  if (
    publicPages.includes(to.name) ||
    to.name === "home" ||
    to.path.startsWith("/prof-signature") ||
    to.path.startsWith("/reset-password") 
  ) {
    return next();
  }

  if (!authStore.isAuthenticated()) {
    return next({
      name: "unauthorized",
      query: { message: "Veuillez vous connecter pour accéder à cette page." },
    });
  }

  const userExists = await authStore.checkIfUserExists();
  if (userExists === false) {
    return next({
      name: "unauthorized",
      query: {
        message: "Votre compte n'existe pas dans notre base de données.",
      },
    });
  }

  if (to.meta && to.meta.requiresAdmin) {
    const isAdmin = await authStore.isAdmin();
    if (!isAdmin) {
      return next({
        name: "unauthorized",
        query: {
          message: "Vous n'avez pas les droits administrateur nécessaires.",
        },
      });
    }
  }

  next();
});

export default router;
