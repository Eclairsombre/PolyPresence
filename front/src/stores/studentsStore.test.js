import { beforeEach, describe, expect, it, vi } from "vitest";
import { createPinia, setActivePinia } from "pinia";
import axios from "axios";
import { useStudentsStore } from "./studentsStore";

const getAdminTokenMock = vi.fn();
const logoutMock = vi.fn();
const updateUserLocalStorageMock = vi.fn();

const authState = {
  user: { isAdmin: true, studentId: "ADM" },
};

vi.mock("axios", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
    isAxiosError: vi.fn(() => false),
  },
}));

vi.mock("./authStore", () => ({
  useAuthStore: () => ({
    user: authState.user,
    getAdminToken: getAdminTokenMock,
    logout: logoutMock,
    updateUserLocalStorage: updateUserLocalStorageMock,
  }),
}));

describe("studentsStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    authState.user = { isAdmin: true, studentId: "ADM" };
  });

  it("_createAdminConfig retourne le header admin", async () => {
    getAdminTokenMock.mockResolvedValue("adm-token");
    const store = useStudentsStore();

    const config = await store._createAdminConfig();

    expect(config).toEqual({ headers: { "Admin-Token": "adm-token" } });
  });

  it("_createAdminConfig echoue si utilisateur non admin", async () => {
    authState.user = { isAdmin: false, studentId: "U1" };
    const store = useStudentsStore();

    await expect(store._createAdminConfig()).rejects.toThrow("Non autorisé");
  });

  it("_createAdminConfig echoue et appelle logout si token admin manquant", async () => {
    getAdminTokenMock.mockResolvedValue(null);
    const store = useStudentsStore();

    await expect(store._createAdminConfig()).rejects.toThrow(
      "Échec de l'authentification administrateur",
    );
    expect(logoutMock).toHaveBeenCalled();
  });

  it("fetchStudents mappe correctement le format $values", async () => {
    axios.get.mockResolvedValue({
      data: {
        $values: [
          {
            id: 1,
            name: "Doe",
            firstname: "John",
            studentNumber: "S1",
            email: "john@doe.fr",
            year: "3A",
            isDelegate: true,
          },
        ],
      },
    });

    const store = useStudentsStore();
    const result = await store.fetchStudents("3A");

    expect(result).toHaveLength(1);
    expect(result[0].studentNumber).toBe("S1");
  });

  it("fetchStudents supporte un tableau brut", async () => {
    axios.get.mockResolvedValue({
      data: [
        {
          id: 2,
          name: "Roe",
          firstname: "Jane",
          studentNumber: "S2",
          email: "jane@roe.fr",
          year: "4A",
        },
      ],
    });
    const store = useStudentsStore();

    const result = await store.fetchStudents("4A", 7);
    expect(result).toHaveLength(1);
    expect(axios.get).toHaveBeenCalledWith(
      expect.stringContaining("specializationId=7"),
    );
  });

  it("fetchStudents retourne [] sur format inattendu", async () => {
    axios.get.mockResolvedValue({ data: { foo: "bar" } });
    const store = useStudentsStore();

    const result = await store.fetchStudents("3A");
    expect(result).toEqual([]);
  });

  it("fetchStudents retourne [] sur 404 axios", async () => {
    axios.isAxiosError.mockReturnValue(true);
    axios.get.mockRejectedValue({ response: { status: 404 } });
    const store = useStudentsStore();

    const result = await store.fetchStudents("3A");
    expect(result).toEqual([]);
  });

  it("fetchStudents retourne [] sur 401 axios", async () => {
    axios.isAxiosError.mockReturnValue(true);
    axios.get.mockRejectedValue({ response: { status: 401 } });
    const store = useStudentsStore();

    const result = await store.fetchStudents("3A");
    expect(result).toEqual([]);
  });

  it("addStudent retourne false sur conflit 409", async () => {
    getAdminTokenMock.mockResolvedValue("adm-token");
    axios.post.mockRejectedValue({ response: { status: 409 } });

    const store = useStudentsStore();
    const ok = await store.addStudent({ studentNumber: "S1" });

    expect(ok).toBe(false);
  });

  it("addStudent ajoute l'etudiant en succes", async () => {
    getAdminTokenMock.mockResolvedValue("adm-token");
    axios.post.mockResolvedValue({ data: { studentNumber: "S3" } });
    const store = useStudentsStore();

    const result = await store.addStudent({ studentNumber: "S3" });
    expect(result.studentNumber).toBe("S3");
    expect(store.students).toHaveLength(1);
  });

  it("addStudent retourne false sur 401", async () => {
    getAdminTokenMock.mockResolvedValue("adm-token");
    axios.post.mockRejectedValue({ response: { status: 401 } });
    const store = useStudentsStore();

    const result = await store.addStudent({ studentNumber: "S4" });
    expect(result).toBe(false);
  });

  it("addStudent echoue si utilisateur non connecte", async () => {
    authState.user = null;
    const store = useStudentsStore();

    await expect(store.addStudent({ studentNumber: "S1" })).rejects.toThrow(
      "Non autorisé",
    );
  });

  it("deleteStudent supprime l'etudiant du state", async () => {
    getAdminTokenMock.mockResolvedValue("adm-token");
    axios.delete.mockResolvedValue({ status: 204 });
    const store = useStudentsStore();
    store.students = [{ studentNumber: "S1" }, { studentNumber: "S2" }];

    const ok = await store.deleteStudent("S1");

    expect(ok).toBe(true);
    expect(store.students).toEqual([{ studentNumber: "S2" }]);
  });

  it("deleteStudent retourne true sur 404", async () => {
    getAdminTokenMock.mockResolvedValue("adm-token");
    axios.isAxiosError.mockReturnValue(true);
    axios.delete.mockRejectedValue({ response: { status: 404 } });
    const store = useStudentsStore();

    const ok = await store.deleteStudent("S404");
    expect(ok).toBe(true);
  });

  it("deleteStudent retourne false si status inattendu", async () => {
    getAdminTokenMock.mockResolvedValue("adm-token");
    axios.delete.mockResolvedValue({ status: 202 });
    const store = useStudentsStore();

    const ok = await store.deleteStudent("S1");
    expect(ok).toBe(false);
  });

  it("deleteStudent retourne false sur erreur 400", async () => {
    getAdminTokenMock.mockResolvedValue("adm-token");
    axios.isAxiosError.mockReturnValue(true);
    axios.delete.mockRejectedValue({
      response: { status: 400, data: { detail: "bad" } },
    });
    const store = useStudentsStore();

    const ok = await store.deleteStudent("S1");
    expect(ok).toBe(false);
  });

  it("getStudent retourne l'etudiant formate", async () => {
    axios.get.mockResolvedValue({
      status: 200,
      data: {
        exists: true,
        user: {
          name: "Doe",
          firstname: "John",
          studentNumber: "S1",
          email: "john@doe.fr",
          year: "3A",
        },
      },
    });
    const store = useStudentsStore();

    const student = await store.getStudent("S1");
    expect(student.studentNumber).toBe("S1");
  });

  it("getStudent retourne null si exists=false", async () => {
    axios.get.mockResolvedValue({ status: 200, data: { exists: false } });
    const store = useStudentsStore();

    const student = await store.getStudent("S0");
    expect(student).toBeNull();
  });

  it("getStudent retourne null sur 404", async () => {
    axios.get.mockRejectedValue({ response: { status: 404 } });
    const store = useStudentsStore();

    const student = await store.getStudent("S404");
    expect(student).toBeNull();
  });

  it("getStudentById retourne la reponse brute", async () => {
    axios.get.mockResolvedValue({ data: { id: 99 } });
    const store = useStudentsStore();

    const result = await store.getStudentById(99);
    expect(result.id).toBe(99);
  });

  it("getStudentById retourne null en cas d'erreur", async () => {
    axios.get.mockRejectedValue(new Error("boom"));
    const store = useStudentsStore();

    const result = await store.getStudentById(99);
    expect(result).toBeNull();
  });

  it("updateStudent met a jour students et user courant", async () => {
    axios.put.mockResolvedValue({
      data: { studentNumber: "S1", firstname: "New" },
    });
    authState.user = { isAdmin: true, studentId: "S1" };
    getAdminTokenMock.mockResolvedValue("adm-token");
    const store = useStudentsStore();
    store.students = [{ studentNumber: "S1", firstname: "Old" }];

    const result = await store.updateStudent({ studentNumber: "S1" });

    expect(result.firstname).toBe("New");
    expect(store.students[0].firstname).toBe("New");
    expect(updateUserLocalStorageMock).toHaveBeenCalled();
  });

  it("updateStudent continue meme si _createAdminConfig echoue", async () => {
    axios.put.mockResolvedValue({
      data: { studentNumber: "S2", firstname: "N" },
    });
    authState.user = { isAdmin: true, studentId: "ADM" };
    getAdminTokenMock.mockRejectedValue(new Error("token fail"));
    const store = useStudentsStore();

    const result = await store.updateStudent({ studentNumber: "S2" });
    expect(result.studentNumber).toBe("S2");
  });

  it("updateStudent throw en cas d'erreur API", async () => {
    axios.put.mockRejectedValue(new Error("boom"));
    authState.user = { isAdmin: false, studentId: "S1" };
    const store = useStudentsStore();

    await expect(store.updateStudent({ studentNumber: "S1" })).rejects.toThrow(
      "boom",
    );
  });

  it("havePasword retourne havePassword", async () => {
    axios.get.mockResolvedValue({ data: { havePassword: true } });
    const store = useStudentsStore();

    const result = await store.havePasword("S1");
    expect(result).toBe(true);
  });

  it("havePasword propage l'erreur", async () => {
    axios.get.mockRejectedValue(new Error("boom"));
    const store = useStudentsStore();

    await expect(store.havePasword("S1")).rejects.toThrow("boom");
  });

  it("makeAdmin retourne la data API", async () => {
    getAdminTokenMock.mockResolvedValue("adm-token");
    axios.post.mockResolvedValue({ data: { success: true } });
    const store = useStudentsStore();

    const result = await store.makeAdmin("S1");
    expect(result.success).toBe(true);
  });

  it("makeAdmin propage l'erreur", async () => {
    getAdminTokenMock.mockResolvedValue("adm-token");
    axios.post.mockRejectedValue(new Error("boom"));
    const store = useStudentsStore();

    await expect(store.makeAdmin("S1")).rejects.toThrow("boom");
  });
});
