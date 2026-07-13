import { Folder, Upload, Download, Trash2, Eye, Plus, FolderPlus, DollarSign, Calendar, Building } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import {
  listLocations,
  listCategories,
  createCategory,
  listBusinessDocuments,
  uploadBusinessDocument,
  deleteBusinessDocument,
  getBusinessDocumentDownloadUrl,
  getBusinessDocumentPreviewUrl,
} from "../api/client";

export function BusinessDocumentManager({ companyId, user, onNotice }) {
  const [locations, setLocations] = useState([]);
  const [categories, setCategories] = useState([]);
  const [selectedLocationId, setSelectedLocationId] = useState("");
  const [selectedCategoryId, setSelectedCategoryId] = useState("");
  const [documents, setDocuments] = useState([]);

  // Form states for file upload
  const [file, setFile] = useState(null);
  const [documentName, setDocumentName] = useState("");
  const [documentDate, setDocumentDate] = useState("");
  const [vendorName, setVendorName] = useState("");
  const [amount, setAmount] = useState("");
  const [pages, setPages] = useState("1");
  const [fileInputKey, setFileInputKey] = useState(0);

  // Category addition states
  const [newCatName, setNewCatName] = useState("");
  const [newCatParentId, setNewCatParentId] = useState("");
  const [showAddCategory, setShowAddCategory] = useState(false);

  const [loading, setLoading] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [savingCategory, setSavingCategory] = useState(false);

  // Load Locations and Business Categories on load
  useEffect(() => {
    let ignore = false;
    setLoading(true);
    Promise.all([
      listLocations({ companyId }),
      listCategories({ companyId, type: "business" }),
    ])
      .then(([locationData, categoryData]) => {
        if (!ignore) {
          const activeLocations = locationData.filter((l) => !l.inactive);
          setLocations(activeLocations);
          setCategories(categoryData);

          // Select first location if available
          if (activeLocations.length > 0) {
            setSelectedLocationId(String(activeLocations[0].locationId));
          }
          // Select first category if available
          if (categoryData.length > 0) {
            setSelectedCategoryId(String(categoryData[0].categoryId));
          }
        }
      })
      .catch((error) => !ignore && onNotice({ type: "error", text: error.message }))
      .finally(() => !ignore && setLoading(false));

    return () => {
      ignore = true;
    };
  }, [companyId, onNotice]);

  // Load Documents when location or category changes
  useEffect(() => {
    if (!selectedLocationId || !selectedCategoryId) {
      setDocuments([]);
      return;
    }
    let ignore = false;
    listBusinessDocuments({
      companyId,
      locationId: Number(selectedLocationId),
      categoryId: Number(selectedCategoryId),
    })
      .then((data) => !ignore && setDocuments(data))
      .catch((error) => !ignore && onNotice({ type: "error", text: error.message }));

    return () => {
      ignore = true;
    };
  }, [companyId, selectedLocationId, selectedCategoryId, onNotice]);

  // Hierarchically sort/group categories
  const orderedCategories = useMemo(() => {
    const roots = categories.filter((c) => !c.parentId);
    const result = [];
    roots.forEach((parent) => {
      result.push(parent);
      categories
        .filter((c) => c.parentId === parent.categoryId)
        .forEach((child) => result.push(child));
    });
    // Append orphans
    categories.forEach((c) => {
      if (c.parentId && !result.some((r) => r.categoryId === c.categoryId)) {
        result.push(c);
      }
    });
    return result;
  }, [categories]);

  async function handleAddCategory(event) {
    event.preventDefault();
    if (!newCatName) return;
    setSavingCategory(true);
    onNotice(null);
    try {
      const category = await createCategory({
        companyId,
        categoryName: newCatName,
        parentId: newCatParentId ? Number(newCatParentId) : null,
        categoryType: "business",
      });
      setCategories((current) => [...current, category]);
      setNewCatName("");
      setNewCatParentId("");
      setShowAddCategory(false);
      onNotice({ type: "success", text: "Business category added." });
      setSelectedCategoryId(String(category.categoryId));
    } catch (error) {
      onNotice({ type: "error", text: error.message });
    } finally {
      setSavingCategory(false);
    }
  }

  async function handleUpload(event) {
    event.preventDefault();
    if (!file || !selectedCategoryId || !selectedLocationId) {
      onNotice({ type: "error", text: "Please select a location, category, and file." });
      return;
    }
    setUploading(true);
    onNotice(null);
    try {
      const document = await uploadBusinessDocument({
        companyId,
        locationId: Number(selectedLocationId),
        categoryId: Number(selectedCategoryId),
        file,
        documentName: documentName || file.name,
        documentDate: documentDate || null,
        vendorName: vendorName || null,
        amount: amount ? Number(amount) : null,
        pages: Number(pages) || 1,
      });

      setDocuments((current) => [document, ...current]);
      setFile(null);
      setDocumentName("");
      setDocumentDate("");
      setVendorName("");
      setAmount("");
      setPages("1");
      setFileInputKey((k) => k + 1);
      onNotice({ type: "success", text: "Business document uploaded successfully." });
    } catch (error) {
      onNotice({ type: "error", text: error.message });
    } finally {
      setUploading(false);
    }
  }

  async function handleDelete(doc) {
    const ok = window.confirm(`Delete business document "${doc.documentName}"?`);
    if (!ok) return;
    onNotice(null);
    try {
      await deleteBusinessDocument(doc.documentId);
      setDocuments((current) => current.filter((item) => item.documentId !== doc.documentId));
      onNotice({ type: "success", text: "Document deleted." });
    } catch (error) {
      onNotice({ type: "error", text: error.message });
    }
  }

  function openDocument(doc) {
    window.open(getBusinessDocumentPreviewUrl(doc), "_blank", "noopener,noreferrer");
  }

  return (
    <div className="business-workspace" style={{ display: "flex", gap: "20px", height: "calc(100vh - 120px)" }}>
      {/* Left Sidebar: Locations & Folder Structure */}
      <aside className="panel" style={{ width: "300px", display: "flex", flexDirection: "column", padding: "15px", flexShrink: 0 }}>
        <h3>Location / Center</h3>
        <select
          value={selectedLocationId}
          onChange={(e) => setSelectedLocationId(e.target.value)}
          style={{ width: "100%", padding: "8px", marginBottom: "20px", fontSize: "14px" }}
        >
          {locations.length === 0 ? (
            <option value="">No centers active</option>
          ) : (
            locations.map((loc) => (
              <option key={loc.locationId} value={loc.locationId}>
                {loc.locationName}
              </option>
            ))
          )}
        </select>

        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "10px" }}>
          <h3 style={{ margin: 0 }}>Categories</h3>
          <button
            type="button"
            className="icon-button"
            onClick={() => setShowAddCategory(!showAddCategory)}
            title="Add business category"
          >
            <FolderPlus size={18} />
          </button>
        </div>

        {showAddCategory && (
          <form
            onSubmit={handleAddCategory}
            style={{
              padding: "10px",
              background: "rgba(0,0,0,0.05)",
              borderRadius: "6px",
              marginBottom: "15px",
              fontSize: "0.9em",
            }}
          >
            <label style={{ display: "block", marginBottom: "8px" }}>
              Category Name
              <input
                value={newCatName}
                onChange={(e) => setNewCatName(e.target.value)}
                required
                style={{ padding: "5px", width: "100%", marginTop: "3px" }}
              />
            </label>
            <label style={{ display: "block", marginBottom: "8px" }}>
              Parent Category (Optional)
              <select
                value={newCatParentId}
                onChange={(e) => setNewCatParentId(e.target.value)}
                style={{ padding: "5px", width: "100%", marginTop: "3px" }}
              >
                <option value="">None (Top-level)</option>
                {categories
                  .filter((c) => !c.parentId)
                  .map((c) => (
                    <option key={c.categoryId} value={c.categoryId}>
                      {c.categoryName}
                    </option>
                  ))}
              </select>
            </label>
            <div style={{ display: "flex", gap: "5px", justifyContent: "flex-end" }}>
              <button
                type="button"
                className="secondary-button"
                style={{ padding: "3px 8px", fontSize: "0.85em" }}
                onClick={() => setShowAddCategory(false)}
              >
                Cancel
              </button>
              <button
                type="submit"
                className="primary-button"
                style={{ padding: "3px 8px", fontSize: "0.85em" }}
                disabled={savingCategory}
              >
                Save
              </button>
            </div>
          </form>
        )}

        <div style={{ flex: 1, overflowY: "auto" }}>
          {orderedCategories.length === 0 ? (
            <p style={{ color: "#666", fontSize: "0.9em" }}>No business categories found.</p>
          ) : (
            <div style={{ display: "flex", flexDirection: "column", gap: "4px" }}>
              {orderedCategories.map((cat) => {
                const isSelected = selectedCategoryId === String(cat.categoryId);
                return (
                  <button
                    key={cat.categoryId}
                    type="button"
                    onClick={() => setSelectedCategoryId(String(cat.categoryId))}
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: "8px",
                      width: "100%",
                      padding: "8px 12px",
                      textAlign: "left",
                      border: "none",
                      borderRadius: "6px",
                      background: isSelected ? "var(--primary-color, #c1692a)" : "transparent",
                      color: isSelected ? "#fff" : "inherit",
                      paddingLeft: cat.parentId ? "25px" : "12px",
                      cursor: "pointer",
                      fontSize: "14px",
                      transition: "background 0.2s",
                      fontWeight: cat.parentId ? "normal" : "600",
                    }}
                  >
                    <Folder size={16} style={{ color: isSelected ? "#fff" : "#c1692a" }} />
                    <span style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                      {cat.parentId ? `↳ ${cat.categoryName}` : cat.categoryName}
                    </span>
                  </button>
                );
              })}
            </div>
          )}
        </div>
      </aside>

      {/* Main Panel: Documents display & Upload form */}
      <div style={{ flex: 1, display: "flex", flexDirection: "column", gap: "20px" }}>
        {/* Upload Panel */}
        <section className="panel" style={{ padding: "15px" }}>
          <h3 style={{ marginTop: 0, marginBottom: "15px" }}>Upload Finance / Administration Document</h3>
          <form onSubmit={handleUpload} style={{ display: "flex", gap: "12px", flexWrap: "wrap", alignItems: "flex-end" }}>
            <label style={{ flex: "1 1 180px", fontSize: "0.85em" }}>
              Choose File
              <input
                key={fileInputKey}
                type="file"
                onChange={(e) => setFile(e.target.files?.[0] || null)}
                required
                style={{ display: "block", marginTop: "4px" }}
              />
            </label>
            <label style={{ flex: "1 1 150px", fontSize: "0.85em" }}>
              Document Name
              <input
                type="text"
                value={documentName}
                onChange={(e) => setDocumentName(e.target.value)}
                placeholder={file?.name || "Optional custom name"}
                style={{ width: "100%", padding: "6px", marginTop: "4px" }}
              />
            </label>
            <label style={{ flex: "1 1 120px", fontSize: "0.85em" }}>
              Vendor / Party
              <input
                type="text"
                value={vendorName}
                onChange={(e) => setVendorName(e.target.value)}
                placeholder="e.g. Bank of America"
                style={{ width: "100%", padding: "6px", marginTop: "4px" }}
              />
            </label>
            <label style={{ flex: "1 1 100px", fontSize: "0.85em" }}>
              Amount ($)
              <input
                type="number"
                step="0.01"
                value={amount}
                onChange={(e) => setAmount(e.target.value)}
                placeholder="0.00"
                style={{ width: "100%", padding: "6px", marginTop: "4px" }}
              />
            </label>
            <label style={{ flex: "1 1 110px", fontSize: "0.85em" }}>
              Document Date
              <input
                type="date"
                value={documentDate}
                onChange={(e) => setDocumentDate(e.target.value)}
                style={{ width: "100%", padding: "6px", marginTop: "4px" }}
              />
            </label>
            <label style={{ flex: "0 0 60px", fontSize: "0.85em" }}>
              Pages
              <input
                type="number"
                min="1"
                value={pages}
                onChange={(e) => setPages(e.target.value)}
                style={{ width: "100%", padding: "6px", marginTop: "4px" }}
              />
            </label>
            <button
              type="submit"
              className="primary-button"
              disabled={uploading || !file || !selectedCategoryId || !selectedLocationId}
              style={{ height: "35px" }}
            >
              <Upload size={16} />
              {uploading ? "Uploading..." : "Upload"}
            </button>
          </form>
        </section>

        {/* Thumbnail/List Panel */}
        <section className="panel" style={{ flex: 1, padding: "15px", display: "flex", flexDirection: "column", overflow: "hidden" }}>
          <h3 style={{ marginTop: 0, marginBottom: "15px" }}>Business Documents</h3>
          <div style={{ flex: 1, overflowY: "auto" }}>
            {loading ? (
              <p>Loading files...</p>
            ) : documents.length === 0 ? (
              <p style={{ textAlign: "center", padding: "40px", color: "#666" }}>
                No business documents uploaded in this folder yet.
              </p>
            ) : (
              <div
                style={{
                  display: "grid",
                  gridTemplateColumns: "repeat(auto-fill, minmax(220px, 1fr))",
                  gap: "15px",
                }}
              >
                {documents.map((doc) => (
                  <article
                    key={doc.documentId}
                    style={{
                      border: "1px solid rgba(0,0,0,0.1)",
                      borderRadius: "8px",
                      padding: "12px",
                      background: "var(--background-paper)",
                      display: "flex",
                      flexDirection: "column",
                      justifyContent: "space-between",
                      transition: "box-shadow 0.2s",
                      cursor: "pointer",
                    }}
                    onClick={() => openDocument(doc)}
                  >
                    <div>
                      <h4
                        style={{
                          margin: "0 0 8px 0",
                          wordBreak: "break-all",
                          fontSize: "14px",
                          lineHeight: "1.3",
                        }}
                      >
                        {doc.documentName}
                      </h4>
                      <div
                        style={{
                          display: "flex",
                          flexDirection: "column",
                          gap: "4px",
                          fontSize: "0.85em",
                          color: "#666",
                          marginBottom: "10px",
                        }}
                      >
                        {doc.vendorName && (
                          <span style={{ display: "flex", alignItems: "center", gap: "4px" }}>
                            <Building size={12} /> {doc.vendorName}
                          </span>
                        )}
                        {doc.amount && (
                          <span style={{ display: "flex", alignItems: "center", gap: "4px", fontWeight: "600", color: "#2e7d32" }}>
                            <DollarSign size={12} /> {doc.amount.toFixed(2)}
                          </span>
                        )}
                        {doc.documentDate && (
                          <span style={{ display: "flex", alignItems: "center", gap: "4px" }}>
                            <Calendar size={12} /> {new Date(doc.documentDate).toLocaleDateString()}
                          </span>
                        )}
                        <span>{doc.numberOfPages} pages | Uploaded {new Date(doc.date).toLocaleDateString()}</span>
                      </div>
                    </div>

                    <div
                      style={{
                        display: "flex",
                        justifyContent: "flex-end",
                        gap: "8px",
                        borderTop: "1px solid rgba(0,0,0,0.05)",
                        paddingTop: "8px",
                      }}
                      onClick={(e) => e.stopPropagation()}
                    >
                      <button
                        type="button"
                        className="icon-button"
                        onClick={() => openDocument(doc)}
                        title="Preview document"
                      >
                        <Eye size={15} />
                      </button>
                      <a
                        href={getBusinessDocumentDownloadUrl(doc.documentId)}
                        className="icon-button"
                        title="Download document"
                      >
                        <Download size={15} />
                      </a>
                      <button
                        type="button"
                        className="icon-button danger"
                        onClick={() => handleDelete(doc)}
                        title="Delete document"
                      >
                        <Trash2 size={15} />
                      </button>
                    </div>
                  </article>
                ))}
              </div>
            )}
          </div>
        </section>
      </div>
    </div>
  );
}
