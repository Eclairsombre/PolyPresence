<template>
  <div class="group-slots">
    <div class="form-group">
      <label for="sub-group">Sous-groupe</label>
      <select id="sub-group" :value="subGroupId ?? ''" @change="emitSlot('subGroupId', $event)">
        <option value="">Aucun — reçoit tous les cours de la promotion</option>
        <option v-for="group in subGroups" :key="group.id" :value="group.id">
          {{ group.displayName }}
        </option>
      </select>
      <p v-if="!subGroupId" class="slot-hint">
        Tant qu'aucun sous-groupe n'est choisi, l'étudiant est inscrit à toutes
        les séances de son année et de sa filière.
      </p>
    </div>

    <!-- Deux emplacements, pas deux catégories : un seul calendrier ADE porte toutes
         les langues, donc rien ne permet de dire qu'un groupe est « la » LV1. Les deux
         listes proposent les mêmes groupes et l'ordre n'a aucune conséquence — seule
         compte l'appartenance de l'étudiant au groupe. -->
    <div class="form-group">
      <label for="lang1-group">Groupe de langue 1</label>
      <select id="lang1-group" :value="lv1GroupId ?? ''" @change="emitSlot('lv1GroupId', $event)">
        <option value="">Aucun</option>
        <option v-for="group in languageGroups" :key="group.id" :value="group.id">
          {{ group.displayName }}
        </option>
      </select>
    </div>

    <div class="form-group">
      <label for="lang2-group">Groupe de langue 2</label>
      <select id="lang2-group" :value="lv2GroupId ?? ''" @change="emitSlot('lv2GroupId', $event)">
        <option value="">Aucun</option>
        <option v-for="group in languageGroups" :key="group.id" :value="group.id">
          {{ group.displayName }}
        </option>
      </select>
      <p class="slot-hint">
        Les deux emplacements sont interchangeables : l'étudiant reçoit les séances
        des groupes choisis, quel que soit l'ordre.
      </p>
    </div>

    <p v-if="noGroupsAtAll" class="slot-warning">
      Aucun groupe n'est encore connu. Ils apparaissent après le premier import
      de l'emploi du temps, ou peuvent être déclarés à la main dans
      <router-link to="/admin/groups">la gestion des groupes</router-link>.
    </p>
  </div>
</template>

<script setup>
import { computed, onMounted } from "vue";
import { useGroupStore } from "../../stores/groupStore.js";

const props = defineProps({
  subGroupId: { type: [Number, String], default: null },
  lv1GroupId: { type: [Number, String], default: null },
  lv2GroupId: { type: [Number, String], default: null },
  /** Restreint les sous-groupes proposés à la filière de l'étudiant. */
  specializationId: { type: [Number, String], default: null },
});

const emit = defineEmits([
  "update:subGroupId",
  "update:lv1GroupId",
  "update:lv2GroupId",
]);

const groupStore = useGroupStore();

onMounted(() => groupStore.fetchGroups());

// Les sous-groupes sont filtrés sur la filière choisie : proposer "MECA 1-A" à un
// étudiant d'INFO n'aurait aucun sens, et le backend le refuserait de toute façon.
// Les groupes de langues, eux, mélangent volontairement les filières.
const subGroups = computed(() => {
  const specId = props.specializationId ? Number(props.specializationId) : null;
  return groupStore.subGroups.filter(
    (g) => !specId || !g.specializationId || g.specializationId === specId,
  );
});

const languageGroups = computed(() => groupStore.languageGroups);

const noGroupsAtAll = computed(
  () =>
    !groupStore.loading &&
    subGroups.value.length === 0 &&
    languageGroups.value.length === 0,
);

const emitSlot = (slot, event) => {
  const raw = event.target.value;
  emit(`update:${slot}`, raw === "" ? null : Number(raw));
};
</script>

<style scoped>
.group-slots {
  display: contents;
}

.form-group {
  margin-bottom: 15px;
}

label {
  display: block;
  margin-bottom: 5px;
  font-weight: 500;
  color: #2c3e50;
}

select {
  width: 100%;
  padding: 8px 10px;
  border: 1px solid #ddd;
  border-radius: 4px;
  font-size: 1em;
  background: #fff;
}

.slot-hint {
  margin: 5px 0 0;
  font-size: 0.78em;
  color: #6b7684;
  line-height: 1.4;
}

.slot-warning {
  margin: 0 0 15px;
  padding: 8px 10px;
  font-size: 0.8em;
  line-height: 1.4;
  color: #9a5b12;
  background: #fdf6ee;
  border: 1px solid #e6b98a;
  border-radius: 4px;
}
</style>
