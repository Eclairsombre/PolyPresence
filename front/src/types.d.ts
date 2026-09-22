export interface Student {
    name: string;
    firstname: string;
    studentNumber: string;
    email: string;
    year: string;
    signature: string;
    isDelegate?: boolean;
    specializationId?: number | null;
    /** Sous-groupe de promotion. Null = non affecte : l'etudiant recoit alors toutes
     *  les seances de son annee et de sa filiere, comme avant les groupes. */
    subGroupId?: number | null;
    subGroupLabel?: string | null;
    /** Premier groupe de langue, facultatif. Les deux emplacements sont
     *  interchangeables : un seul calendrier ADE porte toutes les langues, donc
     *  rien ne permet de dire qu'un groupe est « la LV1 » plutot que « la LV2 ». */
    lv1GroupId?: number | null;
    lv1GroupLabel?: string | null;
    /** Second groupe de langue, facultatif. */
    lv2GroupId?: number | null;
    lv2GroupLabel?: string | null;
}

export interface Group {
    id: number;
    label: string;
    displayName: string;
    /** 0 = sous-groupe, 1 = promotion entiere, 4 = langue.
     *  2 et 3 (ex-LV1 / ex-LV2) ne sont plus ecrits, seulement relus. */
    type: number;
    specializationId?: number | null;
    specializationCode?: string | null;
    year?: string | null;
    seenCount: number;
    lastSeenAt?: string | null;
    isActive: boolean;
    studentCount: number;
    sessionCount: number;
}