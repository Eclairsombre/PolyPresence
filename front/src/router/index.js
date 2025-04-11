import { createRouter, createWebHistory } from "vue-router";
import StudentsListPage from "../components/pages/StudentsListPage.vue";
import HomePage from "../components/pages/HomePage.vue";
import StudentsSessionPage from "../components/pages/StudentsSessionPage.vue";
import StudentsAttendanceSheetPage from "../components/pages/StudentsAttendanceSheetPage.vue";

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
  },
  {
    path: "/sessions",
    name: "sessions",
    component: StudentsSessionPage,
  },
  {
    path: "/test",
    name: "test",
    component: StudentsAttendanceSheetPage,
  },
];

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
});

export default router;
