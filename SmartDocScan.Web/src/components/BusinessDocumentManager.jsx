import { Folder, Upload, Download, Trash2, Eye, FolderPlus, DollarSign, Calendar, Building, FileText, Plus, X, FolderOpen } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { Dialog, DialogTitle, DialogContent, DialogActions, Button, IconButton, Tooltip, Box, Typography } from "@mui/material";
import {
  listLocations,
  listCategories,
  createCategory,
  listBusinessDocuments,
  uploadBusinessDocument,
  deleteBusinessDocument,
  getBusinessDocumentDownloadUrl,
  getBusinessDocumentPreviewUrl,
  getBusinessDocumentThumbnailUrl,
} from "../api/client";

function getExtension(value) {
  const cleanValue = String(value || "").split("?")[0];
  const fileName = cleanValue.split("/").pop() || "";
  const index = fileName.lastIndexOf(".");
  return index >= 0 ? fileName.slice(index + 1).toLowerCase() : "";
}

function BusinessDocumentThumbnail({ document }) {
  const previewUrl = getBusinessDocumentPreviewUrl(document);
  const thumbnailUrl = getBusinessDocumentThumbnailUrl(document);
  const extension = getExtension(document.url) || getExtension(document.documentName);

  const thumbStyle = {
    width: "100%",
    height: "220px", // Larger modern thumbnail
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    overflow: "hidden",
    background: "#ffffff",
    borderBottom: "1px solid #e0e0e0",
    position: "relative",
  };

  if (["tif", "tiff"].includes(extension)) {
    return (
      <div style={thumbStyle}>
        <img src={thumbnailUrl} alt="" loading="lazy" style={{ width: "100%", height: "100%", objectFit: "contain", padding: "10px" }} />
      </div>
    );
  }

  if (["png", "jpg", "jpeg", "gif", "webp"].includes(extension)) {
    return (
      <div style={thumbStyle}>
        <img src={previewUrl} alt="" loading="lazy" style={{ width: "100%", height: "100%", objectFit: "contain", padding: "10px" }} />
      </div>
    );
  }

  if (extension === "pdf") {
    return (
      <div style={thumbStyle}>
        <iframe
          src={`${previewUrl}#toolbar=0&navpanes=0&scrollbar=0`}
          title={document.documentName}
          style={{ width: "100%", height: "100%", border: "none", overflow: "hidden" }}
          scrolling="no"
        />
        <div style={{ position: "absolute", inset: 0, background: "transparent" }} />
      </div>
    );
  }

  return (
    <div style={{ ...thumbStyle, background: "#f8fafc" }}>
      <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: "12px", color: "#94a3b8" }}>
        <FileText size={48} strokeWidth={1.5} />
        <span style={{ fontSize: "0.85em", fontWeight: "600", textTransform: "uppercase", letterSpacing: "0.5px" }}>{extension || "DOCUMENT"}</span>
      </div>
    </div>
  );
}

export function BusinessDocumentManager({ companyId, user, onNotice }) {
  const [locations, setLocations] = useState([]);
  const [categories, setCategories] = useState([]);
  const [selectedLocationId, setSelectedLocationId] = useState("");
  const [selectedCategoryId, setSelectedCategoryId] = useState("");
  const [documents, setDocuments] = useState([]);

  // Form states for file upload
  const [isUploadModalOpen, setIsUploadModalOpen] = useState(false);
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
    setLoading(true);
    listBusinessDocuments({
      companyId,
      locationId: Number(selectedLocationId),
      categoryId: Number(selectedCategoryId),
    })
      .then((data) => !ignore && setDocuments(data))
      .catch((error) => !ignore && onNotice({ type: "error", text: error.message }))
      .finally(() => !ignore && setLoading(false));

    return () => {
      ignore = true;
    };
  }, [companyId, selectedLocationId, selectedCategoryId, onNotice]);

  // Hierarchically sort/group categories
  const orderedCategories = useMemo(() => {
    const roots = categories.filter((c) => !c.parentId);
    const result = [];
    roots.forEach((parent) => {
      result.push({ ...parent, level: 0 });
      categories
        .filter((c) => c.parentId === parent.categoryId)
        .forEach((child) => result.push({ ...child, level: 1 }));
    });
    // Append orphans
    categories.forEach((c) => {
      if (c.parentId && !result.some((r) => r.categoryId === c.categoryId)) {
        result.push({ ...c, level: 0 });
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
      onNotice({ type: "success", text: "Business folder created." });
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
      setIsUploadModalOpen(false);
      onNotice({ type: "success", text: "Document uploaded to folder successfully." });
    } catch (error) {
      onNotice({ type: "error", text: error.message });
    } finally {
      setUploading(false);
    }
  }

  async function handleDelete(doc) {
    const ok = window.confirm(`Delete document "${doc.documentName}"?`);
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
    <div style={{ display: "flex", height: "calc(100vh - 120px)", borderRadius: "8px", overflow: "hidden", background: "#f1f5f9", border: "1px solid #e2e8f0" }}>
      {/* Left Sidebar: Modern Folder Tree */}
      <aside style={{ width: "280px", display: "flex", flexDirection: "column", background: "#ffffff", borderRight: "1px solid #e2e8f0", flexShrink: 0 }}>
        
        <div style={{ padding: "16px", borderBottom: "1px solid #f1f5f9" }}>
          <Typography variant="subtitle2" sx={{ fontWeight: 600, color: "#475569", mb: 1, textTransform: "uppercase", fontSize: "0.75rem", letterSpacing: "0.5px" }}>
            Location / Center
          </Typography>
          <select
            value={selectedLocationId}
            onChange={(e) => setSelectedLocationId(e.target.value)}
            style={{ width: "100%", padding: "10px", fontSize: "14px", borderRadius: "6px", border: "1px solid #cbd5e1", outline: "none", background: "#f8fafc" }}
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
        </div>

        <div style={{ flex: 1, overflowY: "auto", padding: "16px 8px" }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "12px", padding: "0 8px" }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 600, color: "#475569", textTransform: "uppercase", fontSize: "0.75rem", letterSpacing: "0.5px" }}>
              Folders
            </Typography>
            <Tooltip title="New Folder">
              <IconButton size="small" onClick={() => setShowAddCategory(!showAddCategory)} sx={{ color: "#c1692a" }}>
                <FolderPlus size={16} />
              </IconButton>
            </Tooltip>
          </div>

          {showAddCategory && (
            <div style={{ padding: "12px", background: "#f8fafc", borderRadius: "6px", marginBottom: "16px", margin: "0 8px", border: "1px solid #e2e8f0" }}>
              <form onSubmit={handleAddCategory}>
                <input
                  placeholder="Folder Name"
                  value={newCatName}
                  onChange={(e) => setNewCatName(e.target.value)}
                  required
                  style={{ width: "100%", padding: "8px", marginBottom: "8px", borderRadius: "4px", border: "1px solid #cbd5e1", fontSize: "13px" }}
                />
                <select
                  value={newCatParentId}
                  onChange={(e) => setNewCatParentId(e.target.value)}
                  style={{ width: "100%", padding: "8px", marginBottom: "12px", borderRadius: "4px", border: "1px solid #cbd5e1", fontSize: "13px" }}
                >
                  <option value="">Top-level folder</option>
                  {categories
                    .filter((c) => !c.parentId)
                    .map((c) => (
                      <option key={c.categoryId} value={c.categoryId}>
                        {c.categoryName}
                      </option>
                    ))}
                </select>
                <div style={{ display: "flex", gap: "8px", justifyContent: "flex-end" }}>
                  <Button size="small" variant="text" onClick={() => setShowAddCategory(false)}>Cancel</Button>
                  <Button size="small" variant="contained" type="submit" disabled={savingCategory} disableElevation>Create</Button>
                </div>
              </form>
            </div>
          )}

          {orderedCategories.length === 0 ? (
            <Typography variant="body2" sx={{ color: "#94a3b8", px: 1 }}>No folders found.</Typography>
          ) : (
            <div style={{ display: "flex", flexDirection: "column", gap: "2px" }}>
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
                      gap: "10px",
                      width: "100%",
                      padding: "8px 12px",
                      textAlign: "left",
                      border: "none",
                      borderRadius: "6px",
                      background: isSelected ? "#fff2ea" : "transparent",
                      color: isSelected ? "#a05522" : "#334155",
                      paddingLeft: cat.level === 1 ? "32px" : "12px",
                      cursor: "pointer",
                      fontSize: "14px",
                      transition: "all 0.2s ease",
                      fontWeight: isSelected ? "600" : "500",
                    }}
                    onMouseEnter={(e) => { if (!isSelected) e.currentTarget.style.background = "#f8fafc"; }}
                    onMouseLeave={(e) => { if (!isSelected) e.currentTarget.style.background = "transparent"; }}
                  >
                    {isSelected ? (
                      <FolderOpen size={18} style={{ color: "#c1692a", flexShrink: 0 }} />
                    ) : (
                      <Folder size={18} style={{ color: "#94a3b8", flexShrink: 0 }} />
                    )}
                    <span style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                      {cat.categoryName}
                    </span>
                  </button>
                );
              })}
            </div>
          )}
        </div>
      </aside>

      {/* Main Panel: Modern Desktop Workspace */}
      <div style={{ flex: 1, display: "flex", flexDirection: "column", position: "relative" }}>
        
        {/* Top Toolbar */}
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", padding: "12px 24px", background: "#ffffff", borderBottom: "1px solid #e2e8f0", zIndex: 10 }}>
          <Typography variant="h6" sx={{ fontSize: "1.1rem", fontWeight: 600, color: "#1e293b", display: "flex", alignItems: "center", gap: "10px" }}>
            {categories.find(c => String(c.categoryId) === selectedCategoryId)?.categoryName || "Workspace"}
          </Typography>
          
          <Button
            variant="contained"
            disableElevation
            startIcon={<Upload size={18} />}
            onClick={() => setIsUploadModalOpen(true)}
            sx={{ borderRadius: "6px", textTransform: "none", fontWeight: 600 }}
          >
            Upload Document
          </Button>
        </div>

        {/* Desktop Area */}
        <div style={{ flex: 1, overflowY: "auto", padding: "30px", background: "#f8fafc" }}>
          {loading ? (
            <div style={{ display: "flex", justifyContent: "center", alignItems: "center", height: "100%" }}>
              <Typography variant="body1" color="textSecondary">Loading documents...</Typography>
            </div>
          ) : documents.length === 0 ? (
            <div style={{ display: "flex", flexDirection: "column", justifyContent: "center", alignItems: "center", height: "100%", color: "#94a3b8" }}>
              <FolderOpen size={64} style={{ marginBottom: "16px", opacity: 0.5 }} />
              <Typography variant="h6" sx={{ fontWeight: 500, mb: 1 }}>Empty Folder</Typography>
              <Typography variant="body2">No documents have been uploaded to this folder yet.</Typography>
            </div>
          ) : (
            <div
              style={{
                display: "grid",
                gridTemplateColumns: "repeat(auto-fill, minmax(220px, 1fr))",
                gap: "35px",
                alignItems: "start"
              }}
            >
              {documents.map((doc) => {
                const hasMultiplePages = doc.numberOfPages > 1;
                // Modern paper stack effect
                const stackShadow = hasMultiplePages
                  ? "0 1px 1px rgba(0,0,0,0.05), 0 4px 0 -1px #fff, 0 4px 1px -1px rgba(0,0,0,0.1), 0 8px 0 -2px #fff, 0 8px 1px -2px rgba(0,0,0,0.1), 0 12px 24px rgba(0,0,0,0.08)"
                  : "0 4px 6px -1px rgba(0,0,0,0.1), 0 2px 4px -1px rgba(0,0,0,0.06)";

                return (
                  <article
                    key={doc.documentId}
                    style={{
                      background: "#ffffff",
                      borderRadius: "4px",
                      display: "flex",
                      flexDirection: "column",
                      transition: "transform 0.2s cubic-bezier(0.4, 0, 0.2, 1), box-shadow 0.2s cubic-bezier(0.4, 0, 0.2, 1)",
                      boxShadow: stackShadow,
                      position: "relative",
                      border: "1px solid #e2e8f0",
                      cursor: "pointer",
                      overflow: "hidden"
                    }}
                    onClick={() => openDocument(doc)}
                    onMouseEnter={(e) => {
                      e.currentTarget.style.transform = "translateY(-4px)";
                      e.currentTarget.style.boxShadow = hasMultiplePages
                        ? "0 1px 1px rgba(0,0,0,0.05), 0 4px 0 -1px #fff, 0 4px 1px -1px rgba(0,0,0,0.1), 0 8px 0 -2px #fff, 0 8px 1px -2px rgba(0,0,0,0.1), 0 20px 30px rgba(193, 105, 42, 0.15)"
                        : "0 10px 20px rgba(0,0,0,0.1), 0 6px 6px rgba(0,0,0,0.05), 0 0 0 1px rgba(193, 105, 42, 0.2)";
                      e.currentTarget.style.borderColor = "transparent";
                    }}
                    onMouseLeave={(e) => {
                      e.currentTarget.style.transform = "none";
                      e.currentTarget.style.boxShadow = stackShadow;
                      e.currentTarget.style.borderColor = "#e2e8f0";
                    }}
                  >
                    {/* Thumbnail frame */}
                    <div style={{ position: "relative" }}>
                      <BusinessDocumentThumbnail document={doc} />
                      {hasMultiplePages && (
                        <span
                          style={{
                            position: "absolute",
                            bottom: "10px",
                            right: "10px",
                            background: "rgba(15, 23, 42, 0.8)",
                            backdropFilter: "blur(4px)",
                            color: "#fff",
                            padding: "4px 8px",
                            borderRadius: "12px",
                            fontSize: "0.7rem",
                            fontWeight: "600",
                            letterSpacing: "0.5px"
                          }}
                        >
                          {doc.numberOfPages} pgs
                        </span>
                      )}
                    </div>

                    {/* Document Details Info */}
                    <div style={{ padding: "16px", display: "flex", flexDirection: "column", flex: 1 }}>
                      <Typography
                        variant="subtitle2"
                        sx={{
                          fontWeight: 600,
                          lineHeight: 1.3,
                          color: "#1e293b",
                          display: "-webkit-box",
                          WebkitLineClamp: 2,
                          WebkitBoxOrient: "vertical",
                          overflow: "hidden",
                          mb: 1.5,
                          fontSize: "0.9rem"
                        }}
                        title={doc.documentName}
                      >
                        {doc.documentName}
                      </Typography>
                      
                      <div style={{ display: "flex", flexDirection: "column", gap: "6px", fontSize: "0.75rem", color: "#64748b", marginTop: "auto" }}>
                        {doc.vendorName && (
                          <span style={{ display: "flex", alignItems: "center", gap: "6px", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                            <Building size={14} style={{ color: "#94a3b8", flexShrink: 0 }} /> {doc.vendorName}
                          </span>
                        )}
                        {doc.amount && (
                          <span style={{ display: "flex", alignItems: "center", gap: "6px", fontWeight: "600", color: "#166534" }}>
                            <DollarSign size={14} style={{ flexShrink: 0 }} /> {doc.amount.toFixed(2)}
                          </span>
                        )}
                        {doc.documentDate && (
                          <span style={{ display: "flex", alignItems: "center", gap: "6px" }}>
                            <Calendar size={14} style={{ color: "#94a3b8", flexShrink: 0 }} /> {new Date(doc.documentDate).toLocaleDateString()}
                          </span>
                        )}
                      </div>

                      {/* Action buttons footer */}
                      <div
                        style={{
                          display: "flex",
                          justifyContent: "space-between",
                          borderTop: "1px solid #f1f5f9",
                          paddingTop: "12px",
                          marginTop: "16px",
                        }}
                        onClick={(e) => e.stopPropagation()}
                      >
                        <div style={{ display: "flex", gap: "4px" }}>
                          <Tooltip title="Preview">
                            <IconButton size="small" onClick={() => openDocument(doc)} sx={{ color: "#64748b", '&:hover': { color: "#c1692a", background: "#fff2ea" } }}>
                              <Eye size={16} />
                            </IconButton>
                          </Tooltip>
                          <Tooltip title="Download">
                            <IconButton size="small" component="a" href={getBusinessDocumentDownloadUrl(doc.documentId)} sx={{ color: "#64748b", '&:hover': { color: "#c1692a", background: "#fff2ea" } }}>
                              <Download size={16} />
                            </IconButton>
                          </Tooltip>
                        </div>
                        <Tooltip title="Delete">
                          <IconButton size="small" onClick={() => handleDelete(doc)} sx={{ color: "#94a3b8", '&:hover': { color: "#ef4444", background: "#fef2f2" } }}>
                            <Trash2 size={16} />
                          </IconButton>
                        </Tooltip>
                      </div>
                    </div>
                  </article>
                );
              })}
            </div>
          )}
        </div>
      </div>

      {/* Modern Upload Modal */}
      <Dialog open={isUploadModalOpen} onClose={() => setIsUploadModalOpen(false)} maxWidth="sm" fullWidth PaperProps={{ sx: { borderRadius: '12px' } }}>
        <DialogTitle sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', pb: 1 }}>
          <Typography variant="h6" sx={{ fontWeight: 600 }}>Upload to {categories.find(c => String(c.categoryId) === selectedCategoryId)?.categoryName}</Typography>
          <IconButton onClick={() => setIsUploadModalOpen(false)} size="small">
            <X size={20} />
          </IconButton>
        </DialogTitle>
        <DialogContent dividers sx={{ p: 3 }}>
          <form id="upload-doc-form" onSubmit={handleUpload} style={{ display: "flex", flexDirection: "column", gap: "20px" }}>
            <div style={{ padding: "20px", border: "2px dashed #cbd5e1", borderRadius: "8px", background: "#f8fafc", textAlign: "center", position: "relative" }}>
              <input
                key={fileInputKey}
                type="file"
                onChange={(e) => setFile(e.target.files?.[0] || null)}
                required
                style={{ position: "absolute", inset: 0, width: "100%", height: "100%", opacity: 0, cursor: "pointer" }}
              />
              <Upload size={32} style={{ color: "#94a3b8", marginBottom: "8px" }} />
              <Typography variant="body2" sx={{ color: "#475569", fontWeight: 500 }}>
                {file ? file.name : "Click or drag file to upload"}
              </Typography>
            </div>

            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "16px" }}>
              <div>
                <Typography variant="caption" sx={{ fontWeight: 600, color: "#64748b", mb: 0.5, display: "block" }}>Document Name</Typography>
                <input
                  type="text"
                  value={documentName}
                  onChange={(e) => setDocumentName(e.target.value)}
                  placeholder={file?.name || "Optional custom name"}
                  style={{ width: "100%", padding: "10px", borderRadius: "6px", border: "1px solid #cbd5e1", fontSize: "14px" }}
                />
              </div>
              <div>
                <Typography variant="caption" sx={{ fontWeight: 600, color: "#64748b", mb: 0.5, display: "block" }}>Vendor / Party</Typography>
                <input
                  type="text"
                  value={vendorName}
                  onChange={(e) => setVendorName(e.target.value)}
                  placeholder="e.g. Bank of America"
                  style={{ width: "100%", padding: "10px", borderRadius: "6px", border: "1px solid #cbd5e1", fontSize: "14px" }}
                />
              </div>
              <div>
                <Typography variant="caption" sx={{ fontWeight: 600, color: "#64748b", mb: 0.5, display: "block" }}>Amount ($)</Typography>
                <input
                  type="number"
                  step="0.01"
                  value={amount}
                  onChange={(e) => setAmount(e.target.value)}
                  placeholder="0.00"
                  style={{ width: "100%", padding: "10px", borderRadius: "6px", border: "1px solid #cbd5e1", fontSize: "14px" }}
                />
              </div>
              <div>
                <Typography variant="caption" sx={{ fontWeight: 600, color: "#64748b", mb: 0.5, display: "block" }}>Document Date</Typography>
                <input
                  type="date"
                  value={documentDate}
                  onChange={(e) => setDocumentDate(e.target.value)}
                  style={{ width: "100%", padding: "10px", borderRadius: "6px", border: "1px solid #cbd5e1", fontSize: "14px" }}
                />
              </div>
              <div>
                <Typography variant="caption" sx={{ fontWeight: 600, color: "#64748b", mb: 0.5, display: "block" }}>Pages</Typography>
                <input
                  type="number"
                  min="1"
                  value={pages}
                  onChange={(e) => setPages(e.target.value)}
                  style={{ width: "100%", padding: "10px", borderRadius: "6px", border: "1px solid #cbd5e1", fontSize: "14px" }}
                />
              </div>
            </div>
          </form>
        </DialogContent>
        <DialogActions sx={{ p: 2, px: 3 }}>
          <Button onClick={() => setIsUploadModalOpen(false)} sx={{ color: "#64748b" }}>Cancel</Button>
          <Button
            type="submit"
            form="upload-doc-form"
            variant="contained"
            disableElevation
            disabled={uploading || !file || !selectedCategoryId || !selectedLocationId}
            sx={{ borderRadius: "6px", fontWeight: 600 }}
          >
            {uploading ? "Uploading..." : "Upload Document"}
          </Button>
        </DialogActions>
      </Dialog>
    </div>
  );
}
