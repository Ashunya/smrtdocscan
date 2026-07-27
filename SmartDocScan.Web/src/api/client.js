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
      message = body.detail || body.message || body.title || message;
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
      message = body.detail || body.message || body.title || message;
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

export function listCategories({ companyId, type }) {
  const params = new URLSearchParams({
    companyId: String(companyId),
  });
  if (type) {
    params.set("type", type);
  }
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

export function changePassword({ username, currentPassword, newPassword, confirmPassword }) {
  return request("/auth/change-password", {
    method: "POST",
    body: JSON.stringify({ username, currentPassword, newPassword, confirmPassword }),
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

export function getAuditLogs({ companyId, actor, action, outcome, fromDate, toDate, take = 200 } = {}) {
  const params = new URLSearchParams({ take: String(take) });
  if (companyId) params.set("companyId", String(companyId));
  if (actor) params.set("actor", actor);
  if (action) params.set("action", action);
  if (outcome) params.set("outcome", outcome);
  if (fromDate) params.set("fromDate", fromDate);
  if (toDate) params.set("toDate", toDate);
  return request(`/audit-logs?${params.toString()}`);
}

export function listLocations({ companyId }) {
  const params = new URLSearchParams({
    companyId: String(companyId),
  });
  return request(`/locations?${params.toString()}`);
}

export function saveLocation(location) {
  return request("/locations", {
    method: "POST",
    body: JSON.stringify(location),
  });
}

export function deleteLocation(locationId, companyId) {
  const params = new URLSearchParams({
    companyId: String(companyId),
  });
  return request(`/locations/${locationId}?${params.toString()}`, { method: "DELETE" });
}

export function listBusinessDocuments({ companyId, locationId, documentTypeId, correspondentId, tagId, search }) {
  const params = new URLSearchParams({
    companyId: String(companyId),
  });
  if (locationId) params.set("locationId", String(locationId));
  if (documentTypeId) params.set("documentTypeId", String(documentTypeId));
  if (correspondentId) params.set("correspondentId", String(correspondentId));
  if (tagId) params.set("tagId", String(tagId));
  if (search) params.set("search", search);
  return request(`/business-documents?${params.toString()}`);
}

export function uploadBusinessDocument({ companyId, locationId, documentTypeId, file, documentName, title, documentDate, correspondentId, amount, pages, tags, onProgress }) {
  return new Promise((resolve, reject) => {
    const formData = new FormData();
    formData.set("companyId", String(companyId));
    if (locationId) formData.set("locationId", String(locationId));
    if (documentTypeId) formData.set("documentTypeId", String(documentTypeId));
    if (documentName) formData.set("documentName", documentName);
    if (title) formData.set("title", title);
    if (documentDate) formData.set("documentDate", documentDate);
    if (correspondentId) formData.set("correspondentId", String(correspondentId));
    if (amount) formData.set("amount", String(amount));
    if (pages) formData.set("pages", String(pages));
    if (tags && tags.length > 0) formData.set("tags", JSON.stringify(tags));
    formData.set("file", file);

    const xhr = new XMLHttpRequest();
    xhr.open("POST", `${API_BASE_URL}/business-documents`, true);
    xhr.withCredentials = true;

    if (onProgress && xhr.upload) {
      xhr.upload.onprogress = (event) => {
        if (event.lengthComputable) {
          const percentComplete = Math.round((event.loaded / event.total) * 100);
          onProgress(percentComplete);
        }
      };
    }

    xhr.onload = () => {
      if (xhr.status >= 200 && xhr.status < 300) {
        try {
          resolve(JSON.parse(xhr.responseText));
        } catch (e) {
          resolve(xhr.responseText);
        }
      } else {
        let message = `Request failed with status ${xhr.status}`;
        try {
          const body = JSON.parse(xhr.responseText);
          message = body.detail || body.message || body.title || message;
        } catch (e) {}
        reject(new Error(message));
      }
    };
    xhr.onerror = () => reject(new Error("Network Error"));
    xhr.send(formData);
  });
}

export function updateBusinessDocumentMetadata(documentId, data) {
  return request(`/business-documents/${documentId}/metadata`, {
    method: "PUT",
    body: JSON.stringify(data),
  });
}

export function moveBusinessDocument(documentId, categoryId) {
  return request(`/business-documents/${documentId}/category`, {
    method: "PUT",
    body: JSON.stringify({ categoryId }),
  });
}

export function analyzeBusinessDocument(companyId, file) {
  const formData = new FormData();
  formData.set("file", file);
  return requestForm(`/business-documents/analyze?companyId=${companyId}`, formData);
}

export function getBusinessDocumentDownloadUrl(documentId) {
  return `${API_BASE_URL}/business-documents/${documentId}/download`;
}

export function getBusinessDocumentPreviewUrl(document) {
  const documentId = typeof document === "object" ? document.documentId : document;
  const sourceName = typeof document === "object" ? (document.url || document.documentName || "document") : "document";
  const fileName = String(sourceName).split("?")[0].split("/").pop() || "document";
  return `${API_BASE_URL}/business-documents/${documentId}/preview/${encodeURIComponent(fileName)}`;
}

export function getBusinessDocumentThumbnailUrl(document) {
  const documentId = typeof document === "object" ? document.documentId : document;
  return `${API_BASE_URL}/business-documents/${documentId}/thumbnail`;
}

export function deleteBusinessDocument(documentId) {
  return request(`/business-documents/${documentId}`, { method: "DELETE" });
}

export function renameBusinessDocument(documentId, name) {
  return request(`/business-documents/${documentId}/rename`, {
    method: "PUT",
    body: JSON.stringify({ name }),
  });
}

export function emailBusinessDocument(documentId, to, subject, body) {
  return request(`/business-documents/${documentId}/email`, {
    method: "POST",
    body: JSON.stringify({ to, subject, body }),
  });
}

// ---------------------------------------------
// Correspondents
// ---------------------------------------------
export function listCorrespondents({ companyId }) {
  const params = new URLSearchParams({ companyId: String(companyId) });
  return request(`/correspondents?${params.toString()}`);
}

export function saveCorrespondent(correspondent) {
  return request("/correspondents", {
    method: "POST",
    body: JSON.stringify(correspondent),
  });
}

export function deleteCorrespondent(correspondentId, companyId) {
  const params = new URLSearchParams({ companyId: String(companyId) });
  return request(`/correspondents/${correspondentId}?${params.toString()}`, { method: "DELETE" });
}

// ---------------------------------------------
// Document Types
// ---------------------------------------------
export function listDocumentTypes({ companyId }) {
  const params = new URLSearchParams({ companyId: String(companyId) });
  return request(`/document-types?${params.toString()}`);
}

export function saveDocumentType(documentType) {
  return request("/document-types", {
    method: "POST",
    body: JSON.stringify(documentType),
  });
}

export function deleteDocumentType(documentTypeId, companyId) {
  const params = new URLSearchParams({ companyId: String(companyId) });
  return request(`/document-types/${documentTypeId}?${params.toString()}`, { method: "DELETE" });
}

// ---------------------------------------------
// Tags
// ---------------------------------------------
export function listTags({ companyId }) {
  const params = new URLSearchParams({ companyId: String(companyId) });
  return request(`/tags?${params.toString()}`);
}

export function saveTag(tag) {
  return request("/tags", {
    method: "POST",
    body: JSON.stringify(tag),
  });
}

export function deleteTag(tagId, companyId) {
  const params = new URLSearchParams({ companyId: String(companyId) });
  return request(`/tags/${tagId}?${params.toString()}`, { method: "DELETE" });
}


