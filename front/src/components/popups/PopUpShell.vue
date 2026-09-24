<template>
  <div class="pp-overlay" @click.self="onBackdrop">
    <div
      class="pp-dialog"
      :class="`pp-${size}`"
      role="dialog"
      aria-modal="true"
      :aria-labelledby="titleId"
    >
      <header class="pp-header">
        <div class="pp-header-text">
          <h2 :id="titleId">{{ title }}</h2>
          <p v-if="subtitle || $slots.subtitle" class="pp-subtitle">
            <slot name="subtitle">{{ subtitle }}</slot>
          </p>
        </div>
        <button
          v-if="!hideClose"
          class="pp-close"
          type="button"
          aria-label="Fermer"
          :disabled="busy"
          @click="$emit('close')"
        >
          &times;
        </button>
      </header>

      <div class="pp-body">
        <slot />
      </div>

      <footer v-if="$slots.footer" class="pp-footer">
        <slot name="footer" />
      </footer>
    </div>
  </div>
</template>

<script setup>
import { onMounted, onUnmounted, useId } from "vue";

/**
 * Coquille commune à toutes les boîtes de dialogue.
 *
 * Elle existe pour une raison précise : chaque popup portait sa propre copie de
 * l'overlay, du cartouche et des boutons — z-index, arrondis et couleurs compris —
 * et les copies avaient divergé. Surtout, aucune ne bornait sa hauteur : dès qu'un
 * formulaire dépassait l'écran, ses boutons devenaient inatteignables.
 *
 * Ici l'en-tête et le pied sont fixes, le corps défile. Le reste (Échap, blocage du
 * défilement de la page, rôles ARIA) est appliqué une fois pour toutes.
 */
const props = defineProps({
  title: { type: String, required: true },
  subtitle: { type: String, default: "" },
  /** sm ≈ confirmation, md ≈ formulaire courant, lg ≈ formulaire dense. */
  size: {
    type: String,
    default: "md",
    validator: (value) => ["sm", "md", "lg"].includes(value),
  },
  /** Réservé aux dialogues sans saisie : ailleurs, un clic à côté perdrait le formulaire. */
  closeOnBackdrop: { type: Boolean, default: false },
  /** Pendant un enregistrement : neutralise fermeture et Échap. */
  busy: { type: Boolean, default: false },
  hideClose: { type: Boolean, default: false },
});

const emit = defineEmits(["close"]);

const titleId = `pp-title-${useId()}`;

const onBackdrop = () => {
  if (props.closeOnBackdrop && !props.busy) emit("close");
};

const onKeydown = (event) => {
  if (event.key === "Escape" && !props.busy) emit("close");
};

// Le fond continuait de défiler sous la boîte de dialogue, et on en ressortait
// ailleurs qu'où on l'avait ouverte. Compteur plutôt que booléen : deux popups
// peuvent se superposer (une confirmation par-dessus un formulaire).
let restoreOverflow = null;

onMounted(() => {
  window.addEventListener("keydown", onKeydown);
  const open = Number(document.body.dataset.ppOpen || 0) + 1;
  document.body.dataset.ppOpen = String(open);
  if (open === 1) {
    restoreOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
  }
});

onUnmounted(() => {
  window.removeEventListener("keydown", onKeydown);
  const open = Math.max(0, Number(document.body.dataset.ppOpen || 1) - 1);
  document.body.dataset.ppOpen = String(open);
  if (open === 0) document.body.style.overflow = restoreOverflow ?? "";
});
</script>

<style scoped>
.pp-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.55);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  padding: 16px;
  animation: ppFade 0.18s ease-out;
}

@keyframes ppFade {
  from {
    opacity: 0;
  }
  to {
    opacity: 1;
  }
}

/* Hauteur bornée + corps défilant : c'est ce qui garantit que le pied reste
   atteignable quel que soit le contenu. */
.pp-dialog {
  background: #fff;
  width: 100%;
  max-height: calc(100vh - 32px);
  display: flex;
  flex-direction: column;
  border-radius: 10px;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.25);
  overflow: hidden;
  animation: ppSlide 0.18s ease-out;
}

@keyframes ppSlide {
  from {
    transform: translateY(-16px);
    opacity: 0;
  }
  to {
    transform: translateY(0);
    opacity: 1;
  }
}

.pp-sm {
  max-width: 420px;
}
.pp-md {
  max-width: 560px;
}
.pp-lg {
  max-width: 760px;
}

.pp-header {
  background: #2c3e50;
  color: #fff;
  padding: 14px 20px;
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 12px;
  flex-shrink: 0;
}

.pp-header h2 {
  margin: 0;
  font-size: 1.2rem;
  font-weight: 600;
}

.pp-subtitle {
  margin: 3px 0 0;
  font-size: 0.85rem;
  color: #c6d2de;
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.pp-close {
  background: none;
  border: none;
  color: #fff;
  font-size: 1.7rem;
  line-height: 1;
  cursor: pointer;
  padding: 0;
  opacity: 0.85;
}

.pp-close:hover:not(:disabled) {
  opacity: 1;
}

.pp-close:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.pp-body {
  padding: 18px 20px;
  overflow-y: auto;
  flex: 1;
  min-height: 0;
}

.pp-footer {
  padding: 12px 20px;
  background: #f5f7f9;
  border-top: 1px solid #e0e4ea;
  display: flex;
  justify-content: flex-end;
  align-items: center;
  gap: 10px;
  flex-shrink: 0;
}

/* ---------------------------------------------------------------------------
   Vocabulaire commun mis à disposition du contenu inséré.
   Le contenu d'un slot est compilé dans la portée du parent : sans :deep(), la
   feuille de style de cette coquille ne l'atteindrait pas. C'est ce qui permet
   aux onze popups de partager champs et boutons sans les redéclarer.
   --------------------------------------------------------------------------- */

.pp-body :deep(.pp-text) {
  margin: 0 0 12px;
  color: #2c3e50;
  font-size: 0.95rem;
  line-height: 1.5;
}

.pp-body :deep(.pp-text:last-child) {
  margin-bottom: 0;
}

.pp-body :deep(.pp-section) {
  padding-bottom: 14px;
  margin-bottom: 14px;
  border-bottom: 1px solid #eceff3;
}

.pp-body :deep(.pp-section:last-child) {
  border-bottom: none;
  margin-bottom: 0;
  padding-bottom: 0;
}

.pp-body :deep(.pp-section-title) {
  margin: 0 0 12px;
  font-size: 0.75rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.6px;
  color: #8592a0;
}

.pp-body :deep(.pp-field) {
  margin-bottom: 14px;
}

.pp-body :deep(.pp-field:last-child) {
  margin-bottom: 0;
}

.pp-body :deep(.pp-field > label),
.pp-body :deep(.form-group > label) {
  display: block;
  margin-bottom: 5px;
  font-weight: 500;
  color: #2c3e50;
  font-size: 0.88rem;
}

.pp-body :deep(input[type="text"]),
.pp-body :deep(input[type="email"]),
.pp-body :deep(input[type="date"]),
.pp-body :deep(input[type="time"]),
.pp-body :deep(input[type="number"]),
.pp-body :deep(select),
.pp-body :deep(textarea) {
  width: 100%;
  padding: 9px 10px;
  border: 1px solid #d7dce1;
  border-radius: 5px;
  font-size: 0.95rem;
  font-family: inherit;
  background: #fff;
  color: #1a1a2e;
  box-sizing: border-box;
}

.pp-body :deep(input:focus),
.pp-body :deep(select:focus),
.pp-body :deep(textarea:focus) {
  outline: none;
  border-color: #1f78c8;
  box-shadow: 0 0 0 3px rgba(31, 120, 200, 0.12);
}

.pp-body :deep(input[readonly]) {
  background: #f4f6f8;
  color: #6b7684;
  cursor: default;
}

.pp-body :deep(.pp-required) {
  color: #e74c3c;
}

.pp-body :deep(.pp-row) {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}

.pp-body :deep(.pp-hint) {
  margin: 5px 0 0;
  font-size: 0.76rem;
  color: #6b7684;
  line-height: 1.4;
}

.pp-body :deep(.pp-note) {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  padding: 10px 12px;
  margin-bottom: 14px;
  background: #fdf6ee;
  border: 1px solid #e6c9a0;
  border-radius: 5px;
  color: #8a5a12;
  font-size: 0.84rem;
  line-height: 1.45;
}

.pp-body :deep(.pp-error) {
  padding: 10px 12px;
  margin-top: 14px;
  background: #fdecea;
  border: 1px solid #f5c6c2;
  border-radius: 5px;
  color: #a3372e;
  font-size: 0.85rem;
  line-height: 1.45;
}

.pp-body :deep(.pp-success) {
  padding: 10px 12px;
  margin-top: 14px;
  background: #eef7f1;
  border: 1px solid #bfe0cd;
  border-radius: 5px;
  color: #1e7a45;
  font-size: 0.85rem;
}

.pp-body :deep(.pp-checkbox) {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  padding: 10px 12px;
  border: 1px solid #e0e4ea;
  border-radius: 6px;
  cursor: pointer;
}

.pp-body :deep(.pp-checkbox:hover) {
  background: #f8fafc;
}

.pp-body :deep(.pp-checkbox input) {
  margin-top: 2px;
  width: 16px;
  height: 16px;
  flex-shrink: 0;
}

/* Boutons du pied : une seule définition pour toutes les popups. */
.pp-footer :deep(.pp-btn) {
  padding: 8px 16px;
  border-radius: 5px;
  font-size: 0.88rem;
  font-weight: 600;
  font-family: inherit;
  cursor: pointer;
  border: 1px solid transparent;
  transition: background-color 0.2s;
}

.pp-footer :deep(.pp-btn:disabled) {
  opacity: 0.55;
  cursor: not-allowed;
}

.pp-footer :deep(.pp-btn-ghost) {
  background: #fff;
  border-color: #d7dce1;
  color: #4a5b6a;
}

.pp-footer :deep(.pp-btn-ghost:hover:not(:disabled)) {
  background: #eef1f4;
}

.pp-footer :deep(.pp-btn-primary) {
  background: #1f78c8;
  color: #fff;
}

.pp-footer :deep(.pp-btn-primary:hover:not(:disabled)) {
  background: #1766aa;
}

.pp-footer :deep(.pp-btn-danger) {
  background: #e74c3c;
  color: #fff;
}

.pp-footer :deep(.pp-btn-danger:hover:not(:disabled)) {
  background: #c93c2d;
}

.pp-footer :deep(.pp-btn-warn) {
  background: #d98324;
  color: #fff;
}

.pp-footer :deep(.pp-btn-warn:hover:not(:disabled)) {
  background: #b96c17;
}

.pp-footer :deep(.pp-btn-link) {
  background: none;
  border-color: transparent;
  color: #6c757d;
  text-decoration: underline;
  margin-right: auto;
}

.pp-footer :deep(.pp-btn-link:hover:not(:disabled)) {
  color: #1a1a2e;
}

@media (max-width: 560px) {
  .pp-body :deep(.pp-row) {
    grid-template-columns: 1fr;
    gap: 0;
  }

  .pp-footer {
    flex-wrap: wrap;
  }

  .pp-footer :deep(.pp-btn) {
    flex: 1;
  }
}
</style>
