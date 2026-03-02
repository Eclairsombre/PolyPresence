--
-- PostgreSQL database dump
--

\restrict iR6bRtJ3hTyeuds705w8Tedwoi3CFnVs5hZqm5bh0oWDR1xqQhiK3c2XiVkdr6y

-- Dumped from database version 16.12 (Debian 16.12-1.pgdg13+1)
-- Dumped by pg_dump version 16.12 (Debian 16.12-1.pgdg13+1)

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: Attendances; Type: TABLE; Schema: public; Owner: polypresence
--

CREATE TABLE public."Attendances" (
    "Id" integer NOT NULL,
    "SessionId" integer NOT NULL,
    "StudentId" integer NOT NULL,
    "Status" integer NOT NULL,
    "Comment" text DEFAULT ''::text
);


ALTER TABLE public."Attendances" OWNER TO polypresence;

--
-- Name: IcsLinks; Type: TABLE; Schema: public; Owner: polypresence
--

CREATE TABLE public."IcsLinks" (
    "Id" integer NOT NULL,
    "Year" text NOT NULL,
    "Url" text NOT NULL
);


ALTER TABLE public."IcsLinks" OWNER TO polypresence;

--
-- Name: MailPreferences; Type: TABLE; Schema: public; Owner: polypresence
--

CREATE TABLE public."MailPreferences" (
    "Id" integer NOT NULL,
    "EmailTo" text NOT NULL,
    "Days" text NOT NULL,
    "Active" boolean DEFAULT false NOT NULL
);


ALTER TABLE public."MailPreferences" OWNER TO polypresence;

--
-- Name: Professors; Type: TABLE; Schema: public; Owner: polypresence
--

CREATE TABLE public."Professors" (
    "Id" integer NOT NULL,
    "Name" text NOT NULL,
    "Firstname" text NOT NULL,
    "Email" text NOT NULL
);


ALTER TABLE public."Professors" OWNER TO polypresence;

--
-- Name: SessionSentToUsers; Type: TABLE; Schema: public; Owner: polypresence
--

CREATE TABLE public."SessionSentToUsers" (
    "Id" integer NOT NULL,
    "SessionId" integer NOT NULL,
    "UserId" integer NOT NULL,
    "SentAt" timestamp without time zone NOT NULL
);


ALTER TABLE public."SessionSentToUsers" OWNER TO polypresence;

--
-- Name: Sessions; Type: TABLE; Schema: public; Owner: polypresence
--

CREATE TABLE public."Sessions" (
    "Id" integer NOT NULL,
    "Date" timestamp without time zone NOT NULL,
    "StartTime" timestamp without time zone NOT NULL,
    "EndTime" timestamp without time zone NOT NULL,
    "Year" text DEFAULT ''''::text NOT NULL,
    "Name" text DEFAULT ''''::text NOT NULL,
    "Room" text DEFAULT ''''::text NOT NULL,
    "ValidationCode" text DEFAULT ''''::text NOT NULL,
    "ProfSignature" text,
    "ProfSignatureToken" text,
    "IsSent" boolean DEFAULT false NOT NULL,
    "IsMailSent" boolean DEFAULT false NOT NULL,
    "IsMailSent2" boolean DEFAULT false NOT NULL,
    "IsMerged" boolean DEFAULT false NOT NULL,
    "ProfId" text,
    "ProfId2" text,
    "ProfSignature2" text,
    "ProfSignatureToken2" text,
    "TargetGroup" text DEFAULT ''''::text NOT NULL
);


ALTER TABLE public."Sessions" OWNER TO polypresence;

--
-- Name: Users; Type: TABLE; Schema: public; Owner: polypresence
--

CREATE TABLE public."Users" (
    "Id" integer NOT NULL,
    "Name" text NOT NULL,
    "Firstname" text NOT NULL,
    "StudentNumber" text NOT NULL,
    "Email" text NOT NULL,
    "Year" text NOT NULL,
    "Signature" text NOT NULL,
    "IsAdmin" boolean DEFAULT false NOT NULL,
    "IsDelegate" boolean DEFAULT false NOT NULL,
    "PasswordHash" text,
    "RegisterToken" text,
    "RegisterTokenExpiration" timestamp without time zone,
    "RegisterMailSent" boolean DEFAULT false NOT NULL,
    "MailPreferencesId" integer
);


ALTER TABLE public."Users" OWNER TO polypresence;

--
-- Name: __EFMigrationsHistory; Type: TABLE; Schema: public; Owner: polypresence
--

CREATE TABLE public."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL
);


ALTER TABLE public."__EFMigrationsHistory" OWNER TO polypresence;

--
-- Name: Attendances PK_Attendances; Type: CONSTRAINT; Schema: public; Owner: polypresence
--

ALTER TABLE ONLY public."Attendances"
    ADD CONSTRAINT "PK_Attendances" PRIMARY KEY ("Id");


--
-- Name: IcsLinks PK_IcsLinks; Type: CONSTRAINT; Schema: public; Owner: polypresence
--

ALTER TABLE ONLY public."IcsLinks"
    ADD CONSTRAINT "PK_IcsLinks" PRIMARY KEY ("Id");


--
-- Name: MailPreferences PK_MailPreferences; Type: CONSTRAINT; Schema: public; Owner: polypresence
--

ALTER TABLE ONLY public."MailPreferences"
    ADD CONSTRAINT "PK_MailPreferences" PRIMARY KEY ("Id");


--
-- Name: Professors PK_Professors; Type: CONSTRAINT; Schema: public; Owner: polypresence
--

ALTER TABLE ONLY public."Professors"
    ADD CONSTRAINT "PK_Professors" PRIMARY KEY ("Id");


--
-- Name: SessionSentToUsers PK_SessionSentToUsers; Type: CONSTRAINT; Schema: public; Owner: polypresence
--

ALTER TABLE ONLY public."SessionSentToUsers"
    ADD CONSTRAINT "PK_SessionSentToUsers" PRIMARY KEY ("Id");


--
-- Name: Sessions PK_Sessions; Type: CONSTRAINT; Schema: public; Owner: polypresence
--

ALTER TABLE ONLY public."Sessions"
    ADD CONSTRAINT "PK_Sessions" PRIMARY KEY ("Id");


--
-- Name: Users PK_Users; Type: CONSTRAINT; Schema: public; Owner: polypresence
--

ALTER TABLE ONLY public."Users"
    ADD CONSTRAINT "PK_Users" PRIMARY KEY ("Id");


--
-- Name: __EFMigrationsHistory PK___EFMigrationsHistory; Type: CONSTRAINT; Schema: public; Owner: polypresence
--

ALTER TABLE ONLY public."__EFMigrationsHistory"
    ADD CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId");


--
-- Name: IX_Attendances_SessionId_StudentId; Type: INDEX; Schema: public; Owner: polypresence
--

CREATE UNIQUE INDEX "IX_Attendances_SessionId_StudentId" ON public."Attendances" USING btree ("SessionId", "StudentId");


--
-- Name: IX_Attendances_StudentId; Type: INDEX; Schema: public; Owner: polypresence
--

CREATE INDEX "IX_Attendances_StudentId" ON public."Attendances" USING btree ("StudentId");


--
-- Name: IX_Users_MailPreferencesId; Type: INDEX; Schema: public; Owner: polypresence
--

CREATE INDEX "IX_Users_MailPreferencesId" ON public."Users" USING btree ("MailPreferencesId");


--
-- Name: Attendances FK_Attendances_Sessions_SessionId; Type: FK CONSTRAINT; Schema: public; Owner: polypresence
--

ALTER TABLE ONLY public."Attendances"
    ADD CONSTRAINT "FK_Attendances_Sessions_SessionId" FOREIGN KEY ("SessionId") REFERENCES public."Sessions"("Id") ON DELETE CASCADE;


--
-- Name: Attendances FK_Attendances_Users_StudentId; Type: FK CONSTRAINT; Schema: public; Owner: polypresence
--

ALTER TABLE ONLY public."Attendances"
    ADD CONSTRAINT "FK_Attendances_Users_StudentId" FOREIGN KEY ("StudentId") REFERENCES public."Users"("Id") ON DELETE CASCADE;


--
-- Name: Users FK_Users_MailPreferences_MailPreferencesId; Type: FK CONSTRAINT; Schema: public; Owner: polypresence
--

ALTER TABLE ONLY public."Users"
    ADD CONSTRAINT "FK_Users_MailPreferences_MailPreferencesId" FOREIGN KEY ("MailPreferencesId") REFERENCES public."MailPreferences"("Id");


--
-- PostgreSQL database dump complete
--

\unrestrict iR6bRtJ3hTyeuds705w8Tedwoi3CFnVs5hZqm5bh0oWDR1xqQhiK3c2XiVkdr6y

