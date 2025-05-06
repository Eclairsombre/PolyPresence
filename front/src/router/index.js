import { createRouter, createWebHistory } from "vue-router";
import StudentsListPage from "../components/pages/StudentsListPage.vue";
import HomePage from "../components/pages/HomePage.vue";
import StudentsSessionPage from "../components/pages/StudentsSessionPage.vue";
import ListAttendancePerSession from "../components/pages/ListAttendancePerSession.vue";
import SignaturePage from "../components/pages/SignaturePage.vue";
import NotFoundPage from "../components/pages/errorPages/NotFoundPage.vue";
import UnauthorizedPage from "../components/pages/errorPages/UnauthorizedPage.vue";
import ProfSignaturePage from "../components/pages/ProfSignaturePage.vue";
import AdminImportIcsPage from "../components/pages/AdminImportIcsPage.vue";
import { requiresAdmin, requiresAuth } from "./middleware";

const routes = [
  {
    path: "/",
    name: "home",
    component: HomePage,
  },
  {
    path: "/students",
    name: "students",
    component: StudentsListPage,
    beforeEnter: requiresAdmin,
  },
  {
    path: "/sessions",
    name: "sessions",
    component: StudentsSessionPage,
    beforeEnter: requiresAdmin,
  },
  {
    path: "/sessions/:id",
    name: "SessionAttendance",
    component: ListAttendancePerSession,
    beforeEnter: requiresAdmin,
  },
  {
    path: "/signature",
    name: "signature",
    component: SignaturePage,
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
    component: UnauthorizedPage,
  },
  {
    path: "/not-found",
    name: "not-found",
    component: NotFoundPage,
  },
  {
    path: "/prof-signature/:token",
    component: ProfSignaturePage,
  },
  {
    path: "/:pathMatch(.*)*",
    name: "catch-all",
    component: NotFoundPage,
  },
  {
    path: "/admin/import-edt",
    name: "AdminImportIcs",
    component: AdminImportIcsPage,
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
  if (
    to.name === "home" ||
    to.name === "unauthorized" ||
    to.name === "not-found" ||
    to.path.startsWith("/prof-signature")
  ) {
    return next();
  }
  if (authStore.user && authStore.user.existsInDb === false) {
    return next({ name: "unauthorized" });
  }
  next();
});

export default router;
