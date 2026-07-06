<template>
  <Transition name="confirm-fade">
    <div
      v-if="confirmStore.open"
      class="confirm-overlay"
      @click.self="confirmStore.cancel()"
      @keydown.esc="confirmStore.cancel()"
    >
      <div
        class="confirm-box"
        role="dialog"
        aria-modal="true"
        :aria-label="confirmStore.title"
        ref="boxRef"
        tabindex="-1"
      >
        <h3 class="confirm-title">{{ confirmStore.title }}</h3>
        <p v-if="confirmStore.message" class="confirm-message">
          {{ confirmStore.message }}
        </p>
        <div class="confirm-actions">
          <button class="btn-cancel" type="button" @click="confirmStore.cancel()">
            {{ confirmStore.cancelLabel }}
          </button>
          <button
            class="btn-confirm"
            :class="{ danger: confirmStore.danger }"
            type="button"
            ref="confirmBtnRef"
            @click="confirmStore.confirm()"
          >
            {{ confirmStore.confirmLabel }}
          </button>
        </div>
      </div>
    </div>
  </Transition>
</template>

<script setup>
import { ref, watch, nextTick } from "vue";
import { useConfirmStore } from "../../stores/confirmStore";

const confirmStore = useConfirmStore();
const confirmBtnRef = ref(null);

// Focus le bouton de confirmation à l'ouverture (accessibilité clavier).
watch(
  () => confirmStore.open,
  async (open) => {
    if (open) {
      await nextTick();
      confirmBtnRef.value?.focus();
    }
  },
);
</script>

<style scoped>
.confirm-overlay {
  position: fixed;
  inset: 0;
  background: rgba(15, 20, 35, 0.55);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 4000;
  padding: 16px;
}

.confirm-box {
  background: #fff;
  border-radius: 14px;
  padding: 24px;
  width: 100%;
  max-width: 420px;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
  outline: none;
}

.confirm-title {
  margin: 0 0 8px;
  font-size: 1.15rem;
  font-weight: 700;
  color: #1a1a2e;
}

.confirm-message {
  margin: 0 0 22px;
  color: #495057;
  font-size: 0.95rem;
  line-height: 1.5;
}

.confirm-actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
}

.btn-cancel,
.btn-confirm {
  padding: 10px 18px;
  border-radius: 10px;
  font-size: 0.92rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.15s;
  border: 1px solid transparent;
}

.btn-cancel {
  background: #f1f3f5;
  color: #495057;
  border-color: #dee2e6;
}
.btn-cancel:hover {
  background: #e9ecef;
}

.btn-confirm {
  background: #3498db;
  color: #fff;
}
.btn-confirm:hover {
  background: #2980b9;
}
.btn-confirm.danger {
  background: #e74c3c;
}
.btn-confirm.danger:hover {
  background: #c0392b;
}

.confirm-fade-enter-active,
.confirm-fade-leave-active {
  transition: opacity 0.18s ease;
}
.confirm-fade-enter-from,
.confirm-fade-leave-to {
  opacity: 0;
}
</style>
