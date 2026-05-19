import { ArrowLeft, Barcode, Download, Grid2X2, Images, List, ScanLine, Trash2, Upload } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { deleteDocument, getDocumentDownloadUrl, getDocumentPreviewUrl, listCategories, listDocuments, uploadDocument } from "../api/client";

export function DocumentManager({ companyId, patient, user, onBack, onNotice, onScan, onBarcode }) {
  const [categories, setCategories] = useState([]);
  const [documents, setDocuments] = useState([]);
  const [categoryId, setCategoryId] = useState("");
  const [file, setFile] = useState(null);
  const [fileInputKey, setFileInputKey] = useState(0);
  const [loading, setLoading] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [viewMode, setViewMode] = useState("list");

  const canUpload = Boolean(user?.uploadDocument || user?.isAdmin || user?.superUser);
  const canDelete = Boolean(user?.deleteDocument || user?.isAdmin || user?.superUser);
  const canDownload = Boolean(user?.downloadDocument || user?.printDocument || user?.isAdmin || user?.superUser);
  const canScan = Boolean(user?.scanDocument || user?.isAdmin || user?.superUser);

  useEffect(() => {
    let ignore = false;
    setLoading(true);
    Promise.all([
      listCategories({ companyId }),
      listDocuments({ companyId, patientId: patient.patientId }),
    ])
      .then(([categoryData, documentData]) => {
        if (!ignore) {
          setCategories(categoryData);
          setDocuments(documentData);
          setCategoryId(categoryData[0]?.categoryId ? String(categoryData[0].categoryId) : "");
        }
      })
      .catch((error) => {
        if (!ignore) {
          onNotice({ type: "error", text: error.message });
        }
      })
      .finally(() => {
        if (!ignore) {
          setLoading(false);
        }
      });
    return () => {
      ignore = true;
    };
  }, [companyId, patient.patientId, onNotice]);

  const groupedDocuments = useMemo(() => {
    return documents.reduce((groups, document) => {
      const key = document.categoryName || "Uncategorized";
      groups[key] = groups[key] || [];
      groups[key].push(document);
      return groups;
    }, {});
  }, [documents]);

  async function handleUpload(event) {
    event.preventDefault();
    if (!file || !categoryId) {
      onNotice({ type: "error", text: "Choose a category and document first." });
      return;
    }

    setUploading(true);
    onNotice(null);
    try {
      const document = await uploadDocument({
        companyId,
        patientId: patient.patientId,
        categoryId: Number(categoryId),
        file,
      });
      setDocuments((current) => [document, ...current]);
      setFile(null);
      setFileInputKey((current) => current + 1);
      onNotice({ type: "success", text: "Document uploaded." });
    } catch (error) {
      onNotice({ type: "error", text: error.message });
    } finally {
      setUploading(false);
    }
  }

  async function handleDelete(document) {
    const ok = window.confirm(`Delete ${document.documentName}?`);
    if (!ok) {
      return;
    }

    onNotice(null);
    try {
      await deleteDocument(document.documentId);
      setDocuments((current) => current.filter((item) => item.documentId !== document.documentId));
      onNotice({ type: "success", text: "Document deleted." });
    } catch (error) {
      onNotice({ type: "error", text: error.message });
    }
  }

  function openDocument(document) {
    window.open(getDocumentPreviewUrl(document.documentId), "_blank", "noopener,noreferrer");
  }

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <h2>{patient.lastName}, {patient.firstName}</h2>
          <p>Documents for patient ID {patient.externalPatientId || patient.patientId}</p>
        </div>
        <div className="panel-actions">
          {canScan && (
            <button className="primary-button" type="button" onClick={onScan}>
              <ScanLine size={18} />
              Scan
            </button>
          )}
          <button className="secondary-button" type="button" onClick={onBarcode}>
            <Barcode size={17} />
            Barcode
          </button>
          <button className="secondary-button" type="button" onClick={onBack}>
            <ArrowLeft size={17} />
            Back
          </button>
        </div>
      </div>

      {canUpload && <form className="document-upload" onSubmit={handleUpload}>
        <label>
          Category
          <select value={categoryId} onChange={(event) => setCategoryId(event.target.value)}>
            {categories.length === 0 ? (
              <option value="">No categories</option>
            ) : (
              categories.map((category) => (
                <option key={category.categoryId} value={category.categoryId}>
                  {category.categoryName}
                </option>
              ))
            )}
          </select>
        </label>
        <label>
          Document
          <input key={fileInputKey} type="file" onChange={(event) => setFile(event.target.files?.[0] || null)} />
        </label>
        <button className="primary-button" type="submit" disabled={uploading || !file || !categoryId}>
          <Upload size={18} />
          {uploading ? "Uploading..." : "Upload"}
        </button>
      </form>}

      <div className="document-list">
        <div className="view-toggle">
          <button className={viewMode === "list" ? "icon-button active" : "icon-button"} type="button" onClick={() => setViewMode("list")} aria-label="List view">
            <List size={16} />
          </button>
          <button className={viewMode === "cards" ? "icon-button active" : "icon-button"} type="button" onClick={() => setViewMode("cards")} aria-label="Card view">
            <Grid2X2 size={16} />
          </button>
          <button className={viewMode === "thumbs" ? "icon-button active" : "icon-button"} type="button" onClick={() => setViewMode("thumbs")} aria-label="Thumbnail view">
            <Images size={16} />
          </button>
        </div>
        {loading ? (
          <p className="empty-state">Loading documents...</p>
        ) : documents.length === 0 ? (
          <p className="empty-state">No documents found</p>
        ) : viewMode === "thumbs" ? (
          <div className="document-thumb-grid">
            {documents.map((document) => (
              <article className="document-thumb clickable-card" key={document.documentId} onClick={() => openDocument(document)} role="button" tabIndex={0} onKeyDown={(event) => handleOpenKey(event, document, openDocument)}>
                <DocumentThumbnail document={document} />
                <div className="document-thumb-body">
                  <h3>{document.documentName}</h3>
                  <p>{document.categoryName || "Uncategorized"}</p>
                  <p>{document.numberOfPages} pages | {formatDate(document.date)}</p>
                  <div className="document-thumb-actions">
                    {canDownload && (
                      <a className="icon-button" href={getDocumentDownloadUrl(document.documentId)} onClick={(event) => event.stopPropagation()} aria-label="Download document">
                        <Download size={16} />
                      </a>
                    )}
                    {canDelete && (
                      <button className="icon-button danger" type="button" onClick={(event) => { event.stopPropagation(); handleDelete(document); }} aria-label="Delete document">
                        <Trash2 size={16} />
                      </button>
                    )}
                  </div>
                </div>
              </article>
            ))}
          </div>
        ) : viewMode === "cards" ? (
          <div className="document-card-grid">
            {documents.map((document) => (
              <article className="document-card clickable-card" key={document.documentId} onClick={() => openDocument(document)} role="button" tabIndex={0} onKeyDown={(event) => handleOpenKey(event, document, openDocument)}>
                <h3>{document.documentName}</h3>
                <p>{document.categoryName || "Uncategorized"}</p>
                <p>{document.numberOfPages} pages | {formatDate(document.date)}</p>
                <div className="document-card-actions">
                  {canDownload && (
                    <a className="icon-button" href={getDocumentDownloadUrl(document.documentId)} onClick={(event) => event.stopPropagation()} aria-label="Download document">
                      <Download size={16} />
                    </a>
                  )}
                  {canDelete && (
                    <button className="icon-button danger" type="button" onClick={(event) => { event.stopPropagation(); handleDelete(document); }} aria-label="Delete document">
                      <Trash2 size={16} />
                    </button>
                  )}
                </div>
              </article>
            ))}
          </div>
        ) : (
          Object.entries(groupedDocuments).map(([categoryName, categoryDocuments]) => (
            <div className="document-group" key={categoryName}>
              <h3>{categoryName} <span>{categoryDocuments.length}</span></h3>
              <table>
                <thead>
                  <tr>
                    <th>Document Name</th>
                    <th>Pages</th>
                    <th>Uploaded</th>
                    <th>Path</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {categoryDocuments.map((document) => (
                    <tr className="clickable-row" key={document.documentId} onClick={() => openDocument(document)}>
                      <td>{document.documentName}</td>
                      <td>{document.numberOfPages}</td>
                      <td>{formatDate(document.date)}</td>
                      <td>{document.url}</td>
                      <td className="row-actions">
                        {canDownload && (
                          <a className="icon-button" href={getDocumentDownloadUrl(document.documentId)} onClick={(event) => event.stopPropagation()} aria-label="Download document">
                            <Download size={16} />
                          </a>
                        )}
                        {canDelete && (
                          <button className="icon-button danger" type="button" onClick={(event) => { event.stopPropagation(); handleDelete(document); }} aria-label="Delete document">
                            <Trash2 size={16} />
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ))
        )}
      </div>
    </section>
  );
}

function formatDate(value) {
  if (!value) {
    return "";
  }
  return new Date(value).toLocaleString();
}

function handleOpenKey(event, document, openDocument) {
  if (event.key === "Enter" || event.key === " ") {
    event.preventDefault();
    openDocument(document);
  }
}

function DocumentThumbnail({ document }) {
  const previewUrl = getDocumentPreviewUrl(document.documentId);
  const extension = getExtension(document.documentName || document.url);

  if (["png", "jpg", "jpeg", "gif", "webp"].includes(extension)) {
    return (
      <a className="document-thumb-preview" href={previewUrl} target="_blank" rel="noreferrer" onClick={(event) => event.stopPropagation()} aria-label={`Open ${document.documentName}`}>
        <img src={previewUrl} alt="" loading="lazy" />
      </a>
    );
  }

  if (extension === "pdf") {
    return (
      <a className="document-thumb-preview" href={previewUrl} target="_blank" rel="noreferrer" onClick={(event) => event.stopPropagation()} aria-label={`Open ${document.documentName}`}>
        <iframe src={`${previewUrl}#toolbar=0&navpanes=0&scrollbar=0`} title={document.documentName} />
      </a>
    );
  }

  return (
    <a className="document-thumb-preview" href={previewUrl} target="_blank" rel="noreferrer" onClick={(event) => event.stopPropagation()} aria-label={`Open ${document.documentName}`}>
      <div className="document-thumb-placeholder">
        <span>{extension || "file"}</span>
      </div>
    </a>
  );
}

function getExtension(value) {
  const cleanValue = String(value || "").split("?")[0];
  const fileName = cleanValue.split("/").pop() || "";
  const index = fileName.lastIndexOf(".");
  return index >= 0 ? fileName.slice(index + 1).toLowerCase() : "";
}
