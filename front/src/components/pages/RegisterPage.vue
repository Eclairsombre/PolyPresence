<template>
  <div class="reg-page">
    <div class="reg-card">
      <!-- Progress stepper -->
      <div class="stepper">
        <div v-for="(st, i) in steps" :key="i" class="step">
          <div
            class="step-bar"
            :style="{ background: st.active || st.done ? '#3498db' : '#e6e9ee' }"
          ></div>
          <div
            class="step-label"
            :style="{ color: st.active ? '#1a1a2e' : st.done ? '#6c8aa8' : '#b3bac3' }"
          >
            {{ st.label }}
          </div>
        </div>
      </div>

      <div class="card-body">
        <!-- STEP 1 — EMAIL -->
        <div v-if="step === 'email'">
          <h1 class="title">Créez votre compte</h1>
          <p class="subtitle">
            Entrez votre adresse <strong>universitaire</strong>. Votre filière
            est déjà rattachée à votre compte.
          </p>

          <div class="field">
            <label>Adresse e-mail</label>
            <input
              v-model.trim="email"
              type="email"
              placeholder="prenom.nom@etu.univ-lyon1.fr"
              autocomplete="email"
              :class="{ 'input-error': !!emailError }"
              @input="emailError = ''"
              @keyup.enter="submitEmail"
            />
          </div>

          <Transition name="fade">
            <div v-if="emailError" class="alert-error">{{ emailError }}</div>
          </Transition>

          <button
            class="btn-primary"
            :disabled="!emailValid || sending"
            @click="submitEmail"
          >
            {{ sending ? "Envoi…" : "Continuer" }}
          </button>

          <div class="card-foot">
            <span>Déjà un compte ? </span>
            <a href="#" @click.prevent="goToLogin">Se connecter</a>
          </div>
        </div>

        <!-- STEP 2 — OTP -->
        <div v-else-if="step === 'verify'">
          <button class="back-btn" @click="step = 'email'"><AppIcon name="arrow-left" :size="14" /> Retour</button>
          <h1 class="title">Vérifiez votre e-mail</h1>
          <p class="subtitle">
            Un code à 6 chiffres a été envoyé à <strong>{{ email }}</strong>. Il
            confirme que l'adresse est bien la vôtre.
          </p>

          <div class="otp-row">
            <input
              v-for="(d, i) in otp"
              :key="i"
              :ref="(el) => (otpRefs[i] = el)"
              v-model="otp[i]"
              inputmode="numeric"
              maxlength="1"
              class="otp-cell"
              :class="{ 'otp-filled': !!otp[i] }"
              @input="onOtpInput(i, $event)"
              @keydown="onOtpKeydown(i, $event)"
              @paste="onOtpPaste"
            />
          </div>

          <Transition name="fade">
            <div v-if="otpError" class="alert-error mt">{{ otpError }}</div>
          </Transition>

          <div class="resend">
            Pas reçu ?
            <a href="#" @click.prevent="resendCode">Renvoyer le code</a>
            <span v-if="resendMsg" class="resend-ok"> · {{ resendMsg }}</span>
          </div>

          <button
            class="btn-primary"
            :disabled="!otpComplete || verifying"
            @click="verifyOtp"
          >
            {{ verifying ? "Vérification…" : "Vérifier" }}
          </button>
        </div>

        <!-- STEP 3 — PASSWORD -->
        <div v-else-if="step === 'secure'">
          <button class="back-btn" @click="step = 'verify'"><AppIcon name="arrow-left" :size="14" /> Retour</button>
          <h1 class="title">Choisissez un mot de passe</h1>
          <p class="subtitle">
            Dernière étape. Il vous servira à vous connecter à PolyPresence.
          </p>

          <div class="field">
            <label>Mot de passe</label>
            <input
              v-model="password"
              type="password"
              placeholder="Au moins 8 caractères"
              autocomplete="new-password"
            />
            <div v-if="password.length" class="strength">
              <div class="strength-track">
                <div
                  class="strength-fill"
                  :style="{ width: strength.pct, background: strength.color }"
                ></div>
              </div>
              <span class="strength-label" :style="{ color: strength.color }">{{
                strength.label
              }}</span>
            </div>
          </div>

          <div class="field">
            <label>Confirmer le mot de passe</label>
            <input
              v-model="confirm"
              type="password"
              placeholder="Retapez votre mot de passe"
              autocomplete="new-password"
              :class="{ 'input-error': confirmMismatch }"
              @keyup.enter="finish"
            />
            <span v-if="confirmMismatch" class="mismatch"
              >Les mots de passe ne correspondent pas.</span
            >
          </div>

          <Transition name="fade">
            <div v-if="secureError" class="alert-error mt">{{ secureError }}</div>
          </Transition>

          <button
            class="btn-primary"
            :disabled="!canFinish || finishing"
            @click="finish"
          >
            {{ finishing ? "Création…" : "Créer mon compte" }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, nextTick } from "vue";
import AppIcon from "../AppIcon.vue";
import axios from "axios";
import { useRouter } from "vue-router";
import { useAuthStore } from "../../stores/authStore";

const API_URL = import.meta.env.VITE_API_URL || "/api";
const router = useRouter();
const authStore = useAuthStore();

const step = ref("email");
const email = ref("");
const emailError = ref("");
const sending = ref(false);

const otp = ref(["", "", "", "", "", ""]);
const otpRefs = ref([]);
const otpError = ref("");
const verifying = ref(false);
const resendMsg = ref("");

const setupToken = ref("");
const password = ref("");
const confirm = ref("");
const secureError = ref("");
const finishing = ref(false);

// --- Email validity (le rôle réel est déterminé au login via IsProfessor) ---
const emailValid = computed(() => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.value));

// --- Stepper ---
const steps = computed(() => {
  const order = ["email", "verify", "secure"];
  const labels = ["Identité", "Vérification", "Sécurité"];
  const cur = order.indexOf(step.value);
  return order.map((k, i) => ({
    label: labels[i],
    done: i < cur,
    active: i === cur,
  }));
});

// --- OTP ---
const otpComplete = computed(() => otp.value.every((d) => d !== ""));

function onOtpInput(i, e) {
  const ch = (e.target.value || "").replace(/\D/g, "").slice(-1);
  otp.value[i] = ch;
  otpError.value = "";
  if (ch && i < 5) otpRefs.value[i + 1]?.focus();
}
function onOtpKeydown(i, e) {
  if (e.key === "Backspace" && !otp.value[i] && i > 0) {
    otpRefs.value[i - 1]?.focus();
  }
}
function onOtpPaste(e) {
  const digits = (e.clipboardData?.getData("text") || "")
    .replace(/\D/g, "")
    .slice(0, 6);
  if (!digits) return;
  e.preventDefault();
  for (let i = 0; i < 6; i++) otp.value[i] = digits[i] || "";
  const next = Math.min(digits.length, 5);
  nextTick(() => otpRefs.value[next]?.focus());
}

// --- Password strength ---
const strength = computed(() => {
  const pw = password.value;
  let s = 0;
  if (pw.length >= 8) s++;
  if (pw.length >= 12) s++;
  if (/[0-9]/.test(pw) && /[a-z]/.test(pw)) s++;
  if (/[A-Z]/.test(pw) && /[^A-Za-z0-9]/.test(pw)) s++;
  s = Math.min(s, 4);
  const map = [
    { label: "Faible", color: "#c0392b", pct: "25%" },
    { label: "Faible", color: "#c0392b", pct: "25%" },
    { label: "Moyen", color: "#d98a1e", pct: "55%" },
    { label: "Fort", color: "#2f9e44", pct: "80%" },
    { label: "Excellent", color: "#2f9e44", pct: "100%" },
  ];
  return map[s];
});
const confirmMismatch = computed(
  () => confirm.value.length > 0 && confirm.value !== password.value,
);
const canFinish = computed(
  () => password.value.length >= 8 && confirm.value === password.value,
);

// --- Actions ---
async function submitEmail() {
  if (!emailValid.value || sending.value) return;
  emailError.value = "";
  sending.value = true;
  try {
    await axios.post(`${API_URL}/User/register/request-code`, {
      email: email.value,
    });
    otp.value = ["", "", "", "", "", ""];
    otpError.value = "";
    step.value = "verify";
    nextTick(() => otpRefs.value[0]?.focus());
  } catch (error) {
    emailError.value =
      error?.response?.data?.message ||
      "Impossible d'envoyer le code. Réessayez plus tard.";
  } finally {
    sending.value = false;
  }
}

async function resendCode() {
  resendMsg.value = "";
  try {
    await axios.post(`${API_URL}/User/register/request-code`, {
      email: email.value,
    });
    resendMsg.value = "Nouveau code envoyé";
  } catch (error) {
    otpError.value =
      error?.response?.data?.message || "Erreur lors de l'envoi du code.";
  }
}

async function verifyOtp() {
  if (!otpComplete.value || verifying.value) return;
  otpError.value = "";
  verifying.value = true;
  try {
    const res = await axios.post(`${API_URL}/User/register/verify-code`, {
      email: email.value,
      code: otp.value.join(""),
    });
    setupToken.value = res.data.setupToken ?? res.data.SetupToken;
    step.value = "secure";
  } catch (error) {
    otpError.value =
      error?.response?.data?.message || "Code invalide ou expiré.";
  } finally {
    verifying.value = false;
  }
}

async function finish() {
  if (!canFinish.value || finishing.value) return;
  secureError.value = "";
  finishing.value = true;
  try {
    await axios.post(`${API_URL}/User/set-password`, {
      token: setupToken.value,
      password: password.value,
    });
    // Connexion automatique puis redirection directe : le rôle réel vient
    // du compte (IsProfessor).
    const user = await authStore.loginWithCredentials(
      email.value,
      password.value,
    );
    router.push(user?.isProfessor ? "/professor/dashboard" : "/");
  } catch (error) {
    secureError.value =
      error?.response?.data?.message ||
      error?.message ||
      "Erreur lors de la création du compte.";
  } finally {
    finishing.value = false;
  }
}

function goToLogin() {
  router.push("/login");
}
</script>

<style scoped>
.reg-page {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 8px 20px 40px;
  font-family: Inter, system-ui, -apple-system, sans-serif;
  color: #1a1a2e;
}

/* Card */
.reg-card {
  width: 100%;
  max-width: 480px;
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 18px;
  box-shadow: 0 10px 40px rgba(20, 28, 48, 0.1);
  overflow: hidden;
}

/* Stepper */
.stepper {
  display: flex;
  gap: 7px;
  padding: 22px 30px 0;
}
.step {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 7px;
}
.step-bar {
  height: 4px;
  border-radius: 99px;
  transition: background 0.3s;
}
.step-label {
  font-size: 10.5px;
  font-weight: 600;
  letter-spacing: 0.4px;
  text-transform: uppercase;
}

.card-body {
  padding: 26px 30px 30px;
}

.title {
  font-size: 22px;
  font-weight: 700;
  margin: 0 0 6px;
}
.subtitle {
  color: #6c757d;
  font-size: 14px;
  line-height: 1.5;
  margin: 0 0 22px;
}
.subtitle strong {
  color: #495057;
}

/* Fields */
.field {
  display: flex;
  flex-direction: column;
  gap: 7px;
  margin-bottom: 14px;
}
.field label {
  font-size: 12px;
  font-weight: 600;
  color: #495057;
  text-transform: uppercase;
  letter-spacing: 0.3px;
}
.field input {
  width: 100%;
  padding: 13px 14px;
  border: 1.5px solid #d1d5db;
  border-radius: 10px;
  font-size: 15px;
  font-family: inherit;
  color: #1a1a2e;
  background: #fff;
  outline: none;
  transition:
    border-color 0.2s,
    box-shadow 0.2s;
  box-sizing: border-box;
}
.field input:focus {
  border-color: #3498db;
  box-shadow: 0 0 0 3px rgba(52, 152, 219, 0.14);
}
.field input.input-error {
  border-color: #f2a5a5;
}

/* Alerts */
.alert-error {
  padding: 10px 13px;
  border-radius: 9px;
  background: #fff5f5;
  border: 1px solid #fecaca;
  color: #c0392b;
  font-size: 13px;
  font-weight: 500;
}
.alert-error.mt {
  margin-top: 12px;
}

/* Buttons */
.btn-primary {
  margin-top: 22px;
  width: 100%;
  padding: 13px 20px;
  border: none;
  border-radius: 11px;
  font-size: 15px;
  font-weight: 600;
  font-family: inherit;
  cursor: pointer;
  color: #fff;
  background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
  box-shadow: 0 6px 18px rgba(26, 26, 46, 0.22);
  transition:
    transform 0.15s,
    box-shadow 0.2s;
}
.btn-primary:hover:not(:disabled) {
  transform: translateY(-1px);
}
.btn-primary:disabled {
  background: #c7ccd4;
  box-shadow: none;
  cursor: not-allowed;
}
.back-btn {
  border: none;
  background: none;
  color: #6c757d;
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  padding: 0;
  margin-bottom: 14px;
  font-family: inherit;
}

.card-foot {
  margin-top: 18px;
  padding-top: 18px;
  border-top: 1px solid #eef0f4;
  text-align: center;
  font-size: 13px;
  color: #6c757d;
}
.card-foot a {
  color: #3498db;
  font-weight: 600;
  text-decoration: none;
}

/* OTP */
.otp-row {
  display: flex;
  gap: 9px;
  justify-content: space-between;
}
.otp-cell {
  width: 100%;
  aspect-ratio: 1;
  text-align: center;
  font-size: 22px;
  font-weight: 700;
  border: 1.5px solid #dde1e7;
  border-radius: 11px;
  color: #1a1a2e;
  background: #fbfcfd;
  outline: none;
  font-family: inherit;
  transition:
    border-color 0.15s,
    box-shadow 0.15s;
  min-width: 0;
}
.otp-cell.otp-filled {
  border-color: #3498db;
}
.otp-cell:focus {
  border-color: #3498db;
  box-shadow: 0 0 0 3px rgba(52, 152, 219, 0.14);
  background: #fff;
}
.resend {
  margin-top: 16px;
  font-size: 13px;
  color: #6c757d;
}
.resend a {
  color: #3498db;
  font-weight: 600;
  text-decoration: none;
}
.resend-ok {
  color: #2f9e44;
  font-weight: 600;
}

/* Strength */
.strength {
  display: flex;
  align-items: center;
  gap: 9px;
  margin-top: 3px;
}
.strength-track {
  flex: 1;
  height: 5px;
  border-radius: 99px;
  background: #eef0f4;
  overflow: hidden;
}
.strength-fill {
  height: 100%;
  border-radius: 99px;
  transition:
    width 0.25s,
    background 0.25s;
}
.strength-label {
  font-size: 11.5px;
  font-weight: 600;
  min-width: 54px;
  text-align: right;
}
.mismatch {
  font-size: 12px;
  color: #c0392b;
  font-weight: 500;
  margin-top: 2px;
}

/* Transitions */
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

@media (max-width: 520px) {
  .reg-card {
    border-radius: 14px;
  }
  .card-body {
    padding: 22px 20px 26px;
  }
  .stepper {
    padding: 20px 20px 0;
  }
}
</style>
