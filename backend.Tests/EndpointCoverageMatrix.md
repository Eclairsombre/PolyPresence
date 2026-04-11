# Endpoint Coverage Matrix

Snapshot: 2026-04-11
Tests backend.Tests: 250 passed, 0 failed

## Coverage Par Controller (fichier)

| Controller file | Couverture |
|---|---:|
| backend/Controllers/IcsLinkController.cs | 100.0% |
| backend/Controllers/ProfessorController.cs | 100.0% |
| backend/Controllers/StatusController.cs | 100.0% |
| backend/Controllers/TokenController.cs | 100.0% |
| backend/Controllers/SpecializationController.cs | 98.8% |
| backend/Controllers/MailPreferencesController.cs | 81.1% |
| backend/Controllers/UserController.cs | 79.8% |
| backend/Controllers/ImportController.cs | 77.1% |
| backend/Controllers/SessionController.cs | 71.7% |

## SessionController

| Endpoint | Methode controleur | Etat | Tests |
|---|---|---|---|
| GET /api/Session | GetSessions | Couvre | SessionControllerTests |
| GET /api/Session/{id} | GetSession | Couvre | SessionControllerTests |
| GET /api/Session/year/{year} | GetSessionsByYear | Couvre | SessionControllerTests |
| POST /api/Session | PostSession | Couvre | SessionControllerEndpointsTests |
| GET /api/Session/prof-signature/{token} | GetSessionByProfSignatureToken | Couvre | SessionControllerEndpointsTests |
| POST /api/Session/prof-signature/{token} | SaveProfSignature | Couvre | SessionControllerEndpointsTests |
| PUT /api/Session/{id} | PutSession | Couvre | SessionControllerEndpointsTests |
| DELETE /api/Session/{id} | DeleteSession | Couvre | SessionControllerEndpointsTests |
| GET /api/Session/current/{year} | GetCurrentSession | Couvre | SessionControllerTests |
| POST /api/Session/{sessionId}/student/{studentNumber} | AddStudentsToSession | Couvre | SessionControllerTests |
| POST /api/Session/{sessionId}/validate/{studentNumber} | ValidateSession | Couvre | SessionControllerTests |
| GET /api/Session/{sessionId}/attendance/{studentNumber} | GetAttendance | Couvre | SessionControllerEndpointsTests |
| GET /api/Session/{sessionId}/attendances | GetSessionAttendances | Couvre | SessionControllerEndpointsTests |
| POST /api/Session/signature/{studentNumber} | SaveSignature | Couvre | SessionControllerEndpointsTests |
| GET /api/Session/signature/{studentNumber} | GetSignature | Couvre | SessionControllerEndpointsTests |
| GET /api/Session/not-send | GetNotSendSessions | Couvre | SessionControllerEndpointsTests |
| POST /api/Session/{sessionId}/set-prof-email | SetProfEmail | Couvre | SessionControllerEndpointsTests |
| POST /api/Session/{sessionId}/set-prof2-email | SetProf2Email | Couvre | SessionControllerEndpointsTests |
| POST /api/Session/{sessionId}/set-professor/{slot} | SetSessionProfessor | Couvre | SessionControllerTests, SessionControllerEndpointsTests |
| POST /api/Session/{sessionId}/resend-prof-mail | ResendProfMail | Couvre | SessionControllerTests |
| POST /api/Session/{sessionId}/resend-prof2-mail | ResendProf2Mail | Couvre | SessionControllerEndpointsTests |
| POST /api/Session/{sessionId}/attendance-status/{studentNumber} | ChangeAttendanceStatus | Couvre | SessionControllerEndpointsTests |
| GET /api/Session/timers | GetTimers | Couvre | SessionControllerTests |
| GET /api/Session/auto-import-status | GetAutoImportStatus | Couvre | SessionControllerTests |
| POST /api/Session/auto-import-status | SetAutoImportStatus | Couvre | SessionControllerTests |
| POST /api/Session/{sessionId}/attendance-comment/{studentNumber} | UpdateAttendanceComment | Couvre | SessionControllerEndpointsTests |

Methodes non endpoint couvertes aussi: CheckAndSendSessionMails, SendProfSignatureMail (via chemins publics)

## UserController

| Endpoint | Methode controleur | Etat | Tests |
|---|---|---|---|
| GET /api/User | GetUsers | Couvre | UserControllerTests |
| GET /api/User/{id} | GetUser | Couvre | UserControllerTests |
| GET /api/User/me | GetCurrentUser | Couvre | UserControllerTests |
| GET /api/User/search/{studentNumber} | SearchUserByNumber | Couvre | UserControllerTests |
| GET /api/User/IsUserAdmin/{username} | IsUserAdmin | Couvre | UserControllerTests |
| PUT /api/User/{studentNumber} | PutUser | Couvre | UserControllerEndpointsTests |
| DELETE /api/User/{studentNumber} | DeleteUser | Couvre | UserControllerEndpointsTests |
| GET /api/User/year/{year} | GetUserByYear | Couvre | UserControllerTests |
| POST /api/User/send-register-link | SendRegisterLink | Couvre | UserControllerEndpointsTests |
| POST /api/User/set-password | SetPassword | Couvre | UserControllerEndpointsTests |
| POST /api/User/login | Login | Couvre | UserControllerTests |
| POST /api/User/refresh-token | RefreshToken | Couvre | UserControllerTests |
| POST /api/User/logout | Logout | Couvre | UserControllerTests |
| POST /api/User/change-password | ChangePassword | Couvre | UserControllerTests |
| POST /api/User/forgot-password | ForgotPassword | Couvre | UserControllerEndpointsTests |
| POST /api/User/reset-password | ResetPassword | Couvre | UserControllerEndpointsTests |
| GET /api/User/have-password/{studentNumber} | HavePassword | Couvre | UserControllerTests |
| POST /api/User/make-admin/{studentNumber} | MakeAdmin | Couvre | UserControllerTests |
| POST /api/User/generate-admin-token | GenerateAdminToken | Couvre | UserControllerTests |
| POST /api/User | PostUser | Couvre | UserControllerTests |

## ImportController

| Endpoint | Methode controleur | Etat | Tests |
|---|---|---|---|
| POST /api/Import/import-ics | ImportIcs | Couvre | ImportControllerTests |

Methodes non endpoint couvertes aussi: ImportAllIcsLinks, ParseName, ExtractTargetGroup, ExtractProfessors, CombineStrings, ApplyBusinessRules, MergeOverlappingSessions, MergeConsecutiveSessions, SyncWithDatabase

## MailPreferencesController

| Endpoint | Methode controleur | Etat | Tests |
|---|---|---|---|
| GET /api/MailPreferences/pdf/{sessionId} | GetPdf | Couvre | MailPreferencesControllerTests |
| GET /api/MailPreferences/{userId} | GetPreferences | Couvre | MailPreferencesControllerTests |
| PUT /api/MailPreferences/{userId} | UpdatePreferences | Couvre | MailPreferencesControllerTests |
| POST /api/MailPreferences/test/{mail} | TestMail | Couvre | MailPreferencesControllerTests |

Methodes non endpoint couvertes aussi: GenerateAndSendZip, GetPromoYears

## IcsLinkController

| Endpoint | Methode controleur | Etat | Tests |
|---|---|---|---|
| GET /api/IcsLink | GetAll | Couvre | IcsLinkControllerTests |
| POST /api/IcsLink | Create | Couvre | IcsLinkControllerTests |
| PUT /api/IcsLink/{id} | Update | Couvre | IcsLinkControllerTests |
| DELETE /api/IcsLink/{id} | Delete | Couvre | IcsLinkControllerTests |

## ProfessorController

| Endpoint | Methode controleur | Etat | Tests |
|---|---|---|---|
| GET /api/Professor | GetProfessors | Couvre | ProfessorControllerTests |
| GET /api/Professor/{id} | GetProfessorById | Couvre | ProfessorControllerTests |
| DELETE /api/Professor/{id} | DeleteProfessor | Couvre | ProfessorControllerTests |
| POST /api/Professor | CreateProfessor | Couvre | ProfessorControllerTests |
| PUT /api/Professor/{id}/email | UpdateProfessorEmail | Couvre | ProfessorControllerTests |
| POST /api/Professor/find-or-create | FindOrCreateProfessor | Couvre | ProfessorControllerTests |

## SpecializationController

| Endpoint | Methode controleur | Etat | Tests |
|---|---|---|---|
| GET /api/Specialization | GetAll | Couvre | SpecializationControllerTests |
| GET /api/Specialization/all | GetAllIncludingInactive | Couvre | SpecializationControllerTests |
| GET /api/Specialization/{id} | GetById | Couvre | SpecializationControllerTests |
| POST /api/Specialization | Create | Couvre | SpecializationControllerTests |
| PUT /api/Specialization/{id} | Update | Couvre | SpecializationControllerTests |
| DELETE /api/Specialization/{id} | Delete | Couvre | SpecializationControllerTests |

## TokenController

| Endpoint | Methode controleur | Etat | Tests |
|---|---|---|---|
| POST /api/Token/revoke | RevokeToken | Couvre | TokenControllerTests |

## StatusController

| Endpoint | Methode controleur | Etat | Tests |
|---|---|---|---|
| GET /api/Status | GetStatus | Couvre | StatusControllerTests |
