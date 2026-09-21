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
    /** Groupe de LV1, facultatif. */
    lv1GroupId?: number | null;
    lv1GroupLabel?: string | null;
    /** Groupe de LV2, facultatif. */
    lv2GroupId?: number | null;
    lv2GroupLabel?: string | null;
}

export interface Group {
    id: number;
    label: string;
    displayName: string;
    /** 0 = sous-groupe, 1 = promotion entiere, 2 = LV1, 3 = LV2 */
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