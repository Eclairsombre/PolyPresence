import { createRouter, createWebHistory } from "vue-router";
import StudentsListPage from "../components/pages/StudentsListPage.vue";
import HomePage from "../components/pages/HomePage.vue";

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
];

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
});

export default router;
