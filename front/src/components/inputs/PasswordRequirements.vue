<template>
  <div class="requirements" :class="{ compact }">
    <p class="requirements-title">Exigences du mot de passe</p>
    <ul class="requirements-list">
      <li
        v-for="rule in rules"
        :key="rule.key"
        :class="{ ok: rule.satisfied, pending: !rule.satisfied }"
      >
        <span class="mark" aria-hidden="true">{{ rule.satisfied ? "✓" : "•" }}</span>
        <span>{{ rule.label }}</span>
      </li>
    </ul>
  </div>
</template>

<script setup>
import { computed } from "vue";
import { checkPassword } from "../../utils/passwordRules.js";

const props = defineProps({
  password: { type: String, default: "" },
  compact: { type: Boolean, default: false },
});

// Les règles restent affichées en permanence, cochées au fur et à mesure : les
// masquer tant qu'elles sont remplies obligerait à deviner ce qui manque au moment
// précis où on s'en soucie.
const rules = computed(() => checkPassword(props.password).rules);
</script>

<style scoped>
.requirements {
  background: #f6f8fa;
  border: 1px solid #e0e4ea;
  border-radius: 8px;
  padding: 12px 14px;
  margin-bottom: 16px;
}

.requirements.compact {
  padding: 10px 12px;
  margin-bottom: 12px;
}

.requirements-title {
  margin: 0 0 8px;
  font-size: 0.78rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: #6c757d;
}

.requirements-list {
  margin: 0;
  padding: 0;
  list-style: none;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.requirements-list li {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 0.85rem;
  line-height: 1.35;
  transition: color 0.15s;
}

.mark {
  width: 14px;
  text-align: center;
  font-weight: 700;
  flex-shrink: 0;
}

.pending {
  color: #6c757d;
}

.ok {
  color: #2f9e44;
}
</style>
