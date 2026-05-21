const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || "/api";

export function getApiBaseUrl() {
  return API_BASE_URL;
}

async function request(path, options = {}) {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    credentials: "include",
    headers: {
      "Content-Type": "application/json",
      ...(options.headers || {}),
    },
    ...options,
  });

  if (!response.ok) {
    let message = `Request failed with status ${response.status}`;
    try {
      const body = await response.json();
      message = body.message || message;
    } catch {
      // Keep default message.
    }
    throw new Error(message);
  }

  if (response.status === 204) {
    return null;
  }

  return response.json();
}

async function requestForm(path, formData) {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: "POST",
    credentials: "include",
    body: formData,
  });

  if (!response.ok) {
    let message = `Request failed with status ${response.status}`;
    try {
      const body = await response.json();
      message = body.message || message;
    } catch {
      // Keep default message.
    }
    throw new Error(message);
  }

  return response.json();
}

export function searchPatients({ companyId, search, take = 100 }) {
  const params = new URLSearchParams({
    companyId: String(companyId),
    take: String(take),
  });
  if (search) {
    params.set("search", search);
  }
  return request(`/patients?${params.toString()}`);
}

export function createPatient(patient) {
  return request("/patients", {
    method: "POST",
    body: JSON.stringify(patient),
  });
}

export function updatePatient(patientId, patient) {
  return request(`/patients/${patientId}`, {
    method: "PUT",
    body: JSON.stringify(patient),
  });
}

export function deletePatient(patientId) {
  return request(`/patients/${patientId}`, { method: "DELETE" });
}

export function listBoxes({ companyId }) {
  const params = new URLSearchParams({
    companyId: String(companyId),
  });
  return request(`/boxes?${params.toString()}`);
}

export function createBox(box) {
  return request("/boxes", {
    method: "POST",
    body: JSON.stringify(box),
  });
}

export function deleteBox(boxId) {
  return request(`/boxes/${boxId}`, { method: "DELETE" });
}

export function listCategories({ companyId }) {
  const params = new URLSearchParams({
    companyId: String(companyId),
  });
  return request(`/categories?${params.toString()}`);
}

export function createCategory(category) {
  return request("/categories", {
    method: "POST",
    body: JSON.stringify(category),
  });
}

export function deleteCategory(categoryId) {
  return request(`/categories/${categoryId}`, { method: "DELETE" });
}

export function listDocuments({ companyId, patientId }) {
  const params = new URLSearchParams({
    companyId: String(companyId),
    patientId: String(patientId),
  });
  return request(`/documents?${params.toString()}`);
}

export function uploadDocument({ companyId, patientId, categoryId, file, documentName, dateOfService, pages, uploadedBy = "Miranda" }) {
  const formData = new FormData();
  formData.set("companyId", String(companyId));
  formData.set("patientId", String(patientId));
  formData.set("categoryId", String(categoryId));
  formData.set("uploadedBy", uploadedBy);
  if (documentName) {
    formData.set("documentName", documentName);
  }
  if (dateOfService) {
    formData.set("dateOfService", dateOfService);
  }
  if (pages) {
    formData.set("pages", String(pages));
  }
  formData.set("file", file);
  return requestForm("/documents", formData);
}

export function getDocumentDownloadUrl(documentId) {
  return `${API_BASE_URL}/documents/${documentId}/download`;
}

export function getDocumentPreviewUrl(document) {
  const documentId = typeof document === "object" ? document.documentId : document;
  const sourceName = typeof document === "object" ? (document.url || document.documentName || "document") : "document";
  const fileName = String(sourceName).split("?")[0].split("/").pop() || "document";
  return `${API_BASE_URL}/documents/${documentId}/preview/${encodeURIComponent(fileName)}`;
}

export function getDocumentThumbnailUrl(document) {
  const documentId = typeof document === "object" ? document.documentId : document;
  return `${API_BASE_URL}/documents/${documentId}/thumbnail`;
}

export function deleteDocument(documentId) {
  return request(`/documents/${documentId}`, {
    method: "DELETE",
  });
}

export function listCompanies() {
  return request("/companies");
}

export function saveCompany(company) {
  return request("/companies", {
    method: "POST",
    body: JSON.stringify(company),
  });
}

export function deleteCompany(companyId) {
  return request(`/companies/${companyId}`, { method: "DELETE" });
}

export function listUsers({ companyId }) {
  const params = new URLSearchParams({
    companyId: String(companyId),
  });
  return request(`/users?${params.toString()}`);
}

export function saveUser(user) {
  return request("/users", {
    method: "POST",
    body: JSON.stringify(user),
  });
}

export function deleteUser(username) {
  return request(`/users/${encodeURIComponent(username)}`, { method: "DELETE" });
}

export function login({ username, password }) {
  return request("/auth/login", {
    method: "POST",
    body: JSON.stringify({ username, password }),
  });
}

export function verifyEmailOtp({ challengeId, code }) {
  return request("/auth/verify-email-otp", {
    method: "POST",
    body: JSON.stringify({ challengeId, code }),
  });
}

export function getCurrentUser() {
  return request("/auth/me");
}

export function logout() {
  return request("/auth/logout", { method: "POST" });
}

export function getMicrosoftSignInUrl(returnUrl = "/") {
  const params = new URLSearchParams({ returnUrl });
  return `${API_BASE_URL}/auth/microsoft?${params.toString()}`;
}

export function changePassword({ username, currentPassword, newPassword }) {
  return request("/auth/change-password", {
    method: "POST",
    body: JSON.stringify({ username, currentPassword, newPassword }),
  });
}

export function getSecuritySettings() {
  return request("/settings/security");
}

export function getBrandingSettings() {
  return request("/settings/branding");
}

export function saveSecuritySettings(settings) {
  return request("/settings/security", {
    method: "POST",
    body: JSON.stringify(settings),
  });
}

export function getDocumentReport({ companyId, fromDate, toDate, take = 500 }) {
  const params = new URLSearchParams({ companyId: String(companyId), take: String(take) });
  if (fromDate) params.set("fromDate", fromDate);
  if (toDate) params.set("toDate", toDate);
  return request(`/reports/documents?${params.toString()}`);
}
