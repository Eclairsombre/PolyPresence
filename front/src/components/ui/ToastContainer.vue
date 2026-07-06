<template>
  <div class="toast-region" role="status" aria-live="polite" aria-atomic="false">
    <TransitionGroup name="toast">
      <div
        v-for="t in toastStore.toasts"
        :key="t.id"
        class="toast"
        :class="`toast-${t.type}`"
        @click="toastStore.remove(t.id)"
      >
        <AppIcon :name="iconFor(t.type)" :size="18" class="toast-icon" />
        <span class="toast-msg">{{ t.message }}</span>
        <button
          class="toast-close"
          type="button"
          aria-label="Fermer la notification"
          @click.stop="toastStore.remove(t.id)"
        >
          <AppIcon name="close" :size="14" />
        </button>
      </div>
    </TransitionGroup>
  </div>
</template>

<script setup>
import { useToastStore } from "../../stores/toastStore";
import AppIcon from "../AppIcon.vue";

const toastStore = useToastStore();

function iconFor(type) {
  if (type === "success") return "check";
  if (type === "error") return "warning";
  return "info-circle";
}
</script>

<style scoped>
.toast-region {
  position: fixed;
  top: 76px;
  right: 16px;
  z-index: 3000;
  display: flex;
  flex-direction: column;
  gap: 10px;
  max-width: min(380px, calc(100vw - 32px));
  pointer-events: none;
}

.toast {
  pointer-events: auto;
  display: flex;
  align-items: flex-start;
  gap: 10px;
  padding: 12px 14px;
  border-radius: 12px;
  background: #fff;
  color: #1a1a2e;
  box-shadow:
    0 10px 30px rgba(0, 0, 0, 0.16),
    0 0 0 1px rgba(0, 0, 0, 0.05);
  border-left: 4px solid #6c757d;
  cursor: pointer;
  font-size: 0.92rem;
}

.toast-icon {
  flex-shrink: 0;
  margin-top: 1px;
}

.toast-msg {
  flex: 1;
  line-height: 1.4;
}

.toast-close {
  flex-shrink: 0;
  background: none;
  border: none;
  color: #adb5bd;
  cursor: pointer;
  padding: 2px;
  border-radius: 6px;
  display: inline-flex;
  transition: color 0.15s;
}
.toast-close:hover {
  color: #495057;
}

.toast-success {
  border-left-color: #27ae60;
}
.toast-success .toast-icon {
  color: #27ae60;
}
.toast-error {
  border-left-color: #e74c3c;
}
.toast-error .toast-icon {
  color: #e74c3c;
}
.toast-info {
  border-left-color: #3498db;
}
.toast-info .toast-icon {
  color: #3498db;
}

.toast-enter-active,
.toast-leave-active {
  transition:
    opacity 0.25s ease,
    transform 0.25s ease;
}
.toast-enter-from,
.toast-leave-to {
  opacity: 0;
  transform: translateX(20px);
}

@media (max-width: 480px) {
  .toast-region {
    top: auto;
    bottom: 16px;
    left: 16px;
    right: 16px;
    max-width: none;
  }
  .toast-enter-from,
  .toast-leave-to {
    transform: translateY(20px);
  }
}
</style>
