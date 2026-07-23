import {
  Folder, Upload, Download, Trash2, Eye, Plus, X, 
  Search, Tag, Building2, FileType, Inbox, LayoutDashboard, Settings,
  AlertCircle, ChevronRight, FileText, Calendar, DollarSign
} from "lucide-react";
import { useEffect, useState, useMemo } from "react";
import { 
  Dialog, DialogTitle, DialogContent, DialogActions, 
  Button, IconButton, Tooltip, Box, Typography, CircularProgress,
  TextField, Select, MenuItem, FormControl, InputLabel, Chip
} from "@mui/material";

import {
  listLocations, saveLocation, deleteLocation,
  listDocumentTypes, saveDocumentType, deleteDocumentType,
  listCorrespondents, saveCorrespondent, deleteCorrespondent,
  listTags, saveTag, deleteTag,
  listBusinessDocuments, uploadBusinessDocument, deleteBusinessDocument,
  updateBusinessDocumentMetadata, getBusinessDocumentPreviewUrl,
  getBusinessDocumentDownloadUrl, getBusinessDocumentThumbnailUrl,
  analyzeBusinessDocument, emailBusinessDocument, renameBusinessDocument
} from "../api/client";

// --- UI Components ---
const SidebarItem = ({ icon: Icon, label, active, onClick }) => (
  <div 
    onClick={onClick}
    style={{
      display: "flex", alignItems: "center", gap: "12px",
      padding: "12px 16px", cursor: "pointer",
      backgroundColor: active ? "#e2e8f0" : "transparent",
      color: active ? "#0f172a" : "#475569",
      fontWeight: active ? "600" : "400",
      borderRadius: "8px", margin: "4px 8px",
      transition: "all 0.2s"
    }}
  >
    <Icon size={20} />
    <span style={{ fontSize: "15px" }}>{label}</span>
  </div>
);

const ModernButton = ({ icon: Icon, label, onClick, primary, color = "#2563eb", disabled }) => (
  <button
    onClick={onClick}
    disabled={disabled}
    style={{
      display: "flex", alignItems: "center", gap: "8px",
      padding: "10px 16px",
      backgroundColor: disabled ? "#cbd5e1" : (primary ? color : "transparent"),
      color: disabled ? "#94a3b8" : (primary ? "white" : color),
      border: `1px solid ${disabled ? "#cbd5e1" : color}`,
      borderRadius: "6px", cursor: disabled ? "not-allowed" : "pointer",
      fontWeight: "500", fontSize: "14px", transition: "all 0.2s"
    }}
  >
    {Icon && <Icon size={18} />}
    {label}
  </button>
);

// --- Main Component ---
export function BusinessDocumentManager({ companyId, user, onNotice }) {
  // Views: dashboard, documents, tags, types, correspondents, locations
  const [activeView, setActiveView] = useState("dashboard");
  
  // Data State
  const [documents, setDocuments] = useState([]);
  const [locations, setLocations] = useState([]);
  const [documentTypes, setDocumentTypes] = useState([]);
  const [correspondents, setCorrespondents] = useState([]);
  const [tags, setTags] = useState([]);
  
  const [loading, setLoading] = useState(false);

  // Filters
  const [searchQuery, setSearchQuery] = useState("");
  const [filterLocation, setFilterLocation] = useState("");
  const [filterType, setFilterType] = useState("");
  const [filterCorrespondent, setFilterCorrespondent] = useState("");
  const [filterTag, setFilterTag] = useState("");

  // Document Selection
  const [selectedDocument, setSelectedDocument] = useState(null);
  const [isUploadModalOpen, setIsUploadModalOpen] = useState(false);

  const loadMetadata = async () => {
    try {
      const [locs, types, corrs, tgs] = await Promise.all([
        listLocations({ companyId }),
        listDocumentTypes({ companyId }),
        listCorrespondents({ companyId }),
        listTags({ companyId })
      ]);
      setLocations(locs);
      setDocumentTypes(types);
      setCorrespondents(corrs);
      setTags(tgs);
    } catch (e) {
      onNotice({ type: "error", text: "Failed to load metadata: " + e.message });
    }
  };

  const loadDocuments = async () => {
    setLoading(true);
    try {
      const docs = await listBusinessDocuments({
        companyId,
        locationId: filterLocation || undefined,
        documentTypeId: filterType || undefined,
        correspondentId: filterCorrespondent || undefined,
        tagId: filterTag || undefined,
        search: searchQuery || undefined
      });
      setDocuments(docs);
    } catch (e) {
      onNotice({ type: "error", text: "Failed to load documents: " + e.message });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadMetadata();
  }, [companyId]);

  useEffect(() => {
    if (activeView === "documents" || activeView === "dashboard") {
      loadDocuments();
    }
  }, [companyId, activeView, filterLocation, filterType, filterCorrespondent, filterTag, searchQuery]);

  const handleDelete = async (doc) => {
    if (!window.confirm("Are you sure you want to delete this document?")) return;
    try {
      await deleteBusinessDocument(doc.documentId);
      onNotice({ type: "success", text: "Deleted successfully" });
      setSelectedDocument(null);
      loadDocuments();
    } catch(e) {
      onNotice({ type: "error", text: e.message });
    }
  };

  const renderDashboard = () => (
    <div style={{ padding: "32px", maxWidth: "1200px", margin: "0 auto" }}>
      <Typography variant="h4" style={{ fontWeight: "700", color: "#1e293b", marginBottom: "24px" }}>
        Dashboard
      </Typography>
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))", gap: "24px" }}>
        <div style={{ background: "white", padding: "24px", borderRadius: "12px", boxShadow: "0 4px 6px -1px rgb(0 0 0 / 0.1)" }}>
          <Typography variant="h6" color="textSecondary">Total Documents</Typography>
          <Typography variant="h3" style={{ fontWeight: "bold", marginTop: "12px", color: "#2563eb" }}>{documents.length}</Typography>
        </div>
        <div style={{ background: "white", padding: "24px", borderRadius: "12px", boxShadow: "0 4px 6px -1px rgb(0 0 0 / 0.1)" }}>
          <Typography variant="h6" color="textSecondary">Inbox (Unsorted)</Typography>
          <Typography variant="h3" style={{ fontWeight: "bold", marginTop: "12px", color: "#ef4444" }}>
            {documents.filter(d => !d.documentTypeId && !d.correspondentId).length}
          </Typography>
        </div>
      </div>
    </div>
  );

  const renderDocuments = () => (
    <div style={{ display: "flex", height: "100%", width: "100%", overflow: "hidden" }}>
      <div style={{ flex: 1, display: "flex", flexDirection: "column", padding: "24px", overflowY: "auto", background: "#f8fafc" }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "24px" }}>
          <Typography variant="h4" style={{ fontWeight: "700", color: "#1e293b" }}>Documents</Typography>
          <ModernButton icon={Upload} label="Upload Document" primary onClick={() => setIsUploadModalOpen(true)} />
        </div>
        
        {/* Filters */}
        <div style={{ display: "flex", gap: "16px", marginBottom: "24px", flexWrap: "wrap", background: "white", padding: "16px", borderRadius: "12px", boxShadow: "0 1px 3px 0 rgb(0 0 0 / 0.1)" }}>
          <TextField 
            label="Search" 
            variant="outlined" 
            size="small" 
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            style={{ minWidth: "250px" }}
          />
          <FormControl size="small" style={{ minWidth: "150px" }}>
            <InputLabel>Document Type</InputLabel>
            <Select value={filterType} onChange={e => setFilterType(e.target.value)} label="Document Type">
              <MenuItem value=""><em>Any</em></MenuItem>
              {documentTypes.map(t => <MenuItem key={t.documentTypeId} value={t.documentTypeId}>{t.name}</MenuItem>)}
            </Select>
          </FormControl>
          <FormControl size="small" style={{ minWidth: "150px" }}>
            <InputLabel>Correspondent</InputLabel>
            <Select value={filterCorrespondent} onChange={e => setFilterCorrespondent(e.target.value)} label="Correspondent">
              <MenuItem value=""><em>Any</em></MenuItem>
              {correspondents.map(c => <MenuItem key={c.correspondentId} value={c.correspondentId}>{c.name}</MenuItem>)}
            </Select>
          </FormControl>
          <FormControl size="small" style={{ minWidth: "150px" }}>
            <InputLabel>Tags</InputLabel>
            <Select value={filterTag} onChange={e => setFilterTag(e.target.value)} label="Tags">
              <MenuItem value=""><em>Any</em></MenuItem>
              {tags.map(t => <MenuItem key={t.tagId} value={t.tagId}>{t.name}</MenuItem>)}
            </Select>
          </FormControl>
          <FormControl size="small" style={{ minWidth: "150px" }}>
            <InputLabel>Storage Path (Location)</InputLabel>
            <Select value={filterLocation} onChange={e => setFilterLocation(e.target.value)} label="Storage Path (Location)">
              <MenuItem value=""><em>Any</em></MenuItem>
              {locations.map(l => <MenuItem key={l.locationId} value={l.locationId}>{l.name}</MenuItem>)}
            </Select>
          </FormControl>
        </div>

        {/* Document List */}
        {loading ? (
          <div style={{ display: "flex", justifyContent: "center", padding: "40px" }}><CircularProgress /></div>
        ) : (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(280px, 1fr))", gap: "24px" }}>
            {documents.map(doc => (
              <div 
                key={doc.documentId}
                onClick={() => setSelectedDocument(doc)}
                style={{
                  background: "white", borderRadius: "12px", overflow: "hidden", 
                  boxShadow: selectedDocument?.documentId === doc.documentId ? "0 0 0 3px #2563eb" : "0 4px 6px -1px rgb(0 0 0 / 0.1)",
                  cursor: "pointer", transition: "transform 0.2s",
                  transform: "scale(1.0)",
                  border: "1px solid #e2e8f0"
                }}
                onMouseEnter={e => e.currentTarget.style.transform = "scale(1.02)"}
                onMouseLeave={e => e.currentTarget.style.transform = "scale(1.0)"}
              >
                <div style={{ height: "160px", background: "#f1f5f9", display: "flex", alignItems: "center", justifyContent: "center" }}>
                  <img src={getBusinessDocumentThumbnailUrl(doc)} alt="" style={{ maxWidth: "100%", maxHeight: "100%", objectFit: "contain" }} onError={(e) => { e.target.style.display='none'; }} />
                  <FileText size={48} color="#cbd5e1" style={{ position: "absolute", zIndex: 0, opacity: 0.5 }} />
                </div>
                <div style={{ padding: "16px", zIndex: 1, position: "relative", background: "white" }}>
                  <Typography variant="subtitle1" style={{ fontWeight: "600", color: "#1e293b", marginBottom: "8px", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
                    {doc.title || doc.documentName}
                  </Typography>
                  <Typography variant="body2" color="textSecondary" style={{ display: "flex", alignItems: "center", gap: "6px", marginBottom: "4px" }}>
                    <Calendar size={14} /> {doc.documentDate ? new Date(doc.documentDate).toLocaleDateString() : "No Date"}
                  </Typography>
                  {doc.correspondentName && (
                    <Typography variant="body2" color="textSecondary" style={{ display: "flex", alignItems: "center", gap: "6px" }}>
                      <Building2 size={14} /> {doc.correspondentName}
                    </Typography>
                  )}
                  <div style={{ marginTop: "12px", display: "flex", flexWrap: "wrap", gap: "4px" }}>
                    {doc.documentTypeName && <Chip size="small" label={doc.documentTypeName} style={{ backgroundColor: "#e0f2fe", color: "#0369a1" }} />}
                    {doc.tags?.map(tag => (
                      <Chip key={tag.tagId} size="small" label={tag.name} style={{ backgroundColor: tag.color || "#f1f5f9" }} />
                    ))}
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Document Sidebar (Right) */}
      {selectedDocument && (
        <div style={{ width: "450px", background: "white", borderLeft: "1px solid #e2e8f0", display: "flex", flexDirection: "column", zIndex: 10 }}>
          <div style={{ padding: "16px", borderBottom: "1px solid #e2e8f0", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <Typography variant="h6" style={{ fontWeight: "600" }}>Document Details</Typography>
            <IconButton onClick={() => setSelectedDocument(null)}><X size={20} /></IconButton>
          </div>
          <div style={{ flex: 1, overflowY: "auto", padding: "24px" }}>
            <DocumentEditor 
              document={selectedDocument} 
              locations={locations}
              documentTypes={documentTypes}
              correspondents={correspondents}
              tags={tags}
              onSave={async (updates) => {
                try {
                  await updateBusinessDocumentMetadata(selectedDocument.documentId, updates);
                  onNotice({ type: "success", text: "Metadata updated" });
                  loadDocuments();
                  setSelectedDocument(null);
                } catch(e) {
                  onNotice({ type: "error", text: e.message });
                }
              }}
              onDelete={() => handleDelete(selectedDocument)}
            />
            
            <div style={{ marginTop: "24px", borderTop: "1px solid #e2e8f0", paddingTop: "24px" }}>
              <Typography variant="subtitle2" style={{ marginBottom: "12px", color: "#64748b" }}>Preview</Typography>
              <div style={{ height: "400px", background: "#f8fafc", borderRadius: "8px", overflow: "hidden", border: "1px solid #e2e8f0" }}>
                <iframe src={getBusinessDocumentPreviewUrl(selectedDocument)} style={{ width: "100%", height: "100%", border: "none" }} title="preview" />
              </div>
              <div style={{ display: "flex", gap: "12px", marginTop: "16px" }}>
                <ModernButton icon={Download} label="Download" onClick={() => window.open(getBusinessDocumentDownloadUrl(selectedDocument.documentId), '_blank')} />
                <ModernButton icon={Eye} label="Open in Tab" onClick={() => window.open(getBusinessDocumentPreviewUrl(selectedDocument), '_blank')} />
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );

  const renderSimpleManager = (title, items, icon, onAdd, onEdit, onDelete) => (
    <div style={{ padding: "32px", maxWidth: "1000px", margin: "0 auto" }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "24px" }}>
        <Typography variant="h4" style={{ fontWeight: "700", color: "#1e293b", display: "flex", alignItems: "center", gap: "12px" }}>
          {icon} {title}
        </Typography>
        <ModernButton icon={Plus} label={`Add ${title}`} primary onClick={onAdd} />
      </div>
      <div style={{ background: "white", borderRadius: "12px", boxShadow: "0 4px 6px -1px rgb(0 0 0 / 0.1)", overflow: "hidden" }}>
        <table style={{ width: "100%", borderCollapse: "collapse" }}>
          <thead>
            <tr style={{ background: "#f8fafc", borderBottom: "1px solid #e2e8f0", textAlign: "left" }}>
              <th style={{ padding: "16px", fontWeight: "600", color: "#475569" }}>Name</th>
              <th style={{ padding: "16px", fontWeight: "600", color: "#475569" }}>Match Pattern</th>
              <th style={{ padding: "16px", fontWeight: "600", color: "#475569", width: "120px" }}>Actions</th>
            </tr>
          </thead>
          <tbody>
            {items.map(item => (
              <tr key={item.id} style={{ borderBottom: "1px solid #e2e8f0" }}>
                <td style={{ padding: "16px", fontWeight: "500" }}>{item.name}</td>
                <td style={{ padding: "16px", color: "#64748b" }}>{item.matchPattern || "-"}</td>
                <td style={{ padding: "16px" }}>
                  <div style={{ display: "flex", gap: "8px" }}>
                    <IconButton size="small" color="error" onClick={() => onDelete(item.id)}><Trash2 size={16} /></IconButton>
                  </div>
                </td>
              </tr>
            ))}
            {items.length === 0 && (
              <tr>
                <td colSpan={3} style={{ padding: "32px", textAlign: "center", color: "#94a3b8" }}>No items found.</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );

  return (
    <div style={{ display: "flex", height: "100vh", backgroundColor: "#f1f5f9", overflow: "hidden" }}>
      {/* Sidebar */}
      <div style={{ width: "260px", background: "#ffffff", borderRight: "1px solid #e2e8f0", display: "flex", flexDirection: "column", zIndex: 20 }}>
        <div style={{ padding: "24px 16px", borderBottom: "1px solid #e2e8f0" }}>
          <Typography variant="h6" style={{ fontWeight: "800", color: "#2563eb", letterSpacing: "-0.5px" }}>
            Paperless Admin
          </Typography>
        </div>
        <div style={{ padding: "16px 0", flex: 1, overflowY: "auto" }}>
          <Typography variant="overline" style={{ padding: "0 24px", color: "#94a3b8", fontWeight: "700" }}>Views</Typography>
          <SidebarItem icon={LayoutDashboard} label="Dashboard" active={activeView === "dashboard"} onClick={() => setActiveView("dashboard")} />
          <SidebarItem icon={FileText} label="Documents" active={activeView === "documents"} onClick={() => setActiveView("documents")} />
          
          <Typography variant="overline" style={{ padding: "0 24px", color: "#94a3b8", fontWeight: "700", display: "block", marginTop: "24px" }}>Manage</Typography>
          <SidebarItem icon={Tag} label="Tags" active={activeView === "tags"} onClick={() => setActiveView("tags")} />
          <SidebarItem icon={FileType} label="Document Types" active={activeView === "types"} onClick={() => setActiveView("types")} />
          <SidebarItem icon={Building2} label="Correspondents" active={activeView === "correspondents"} onClick={() => setActiveView("correspondents")} />
          <SidebarItem icon={Folder} label="Storage Paths" active={activeView === "locations"} onClick={() => setActiveView("locations")} />
        </div>
      </div>

      {/* Main Content */}
      <div style={{ flex: 1, position: "relative" }}>
        {activeView === "dashboard" && renderDashboard()}
        {activeView === "documents" && renderDocuments()}
        
        {activeView === "tags" && renderSimpleManager("Tags", tags.map(t => ({...t, id: t.tagId})), <Tag size={28} />, 
          () => {
            const name = prompt("Enter Tag Name:");
            if (name) saveTag({ companyId, name }).then(() => loadMetadata()).catch(e => onNotice({type:"error", text: e.message}));
          },
          () => {}, 
          (id) => deleteTag(id, companyId).then(() => loadMetadata()).catch(e => onNotice({type:"error", text: e.message}))
        )}
        
        {activeView === "types" && renderSimpleManager("Document Types", documentTypes.map(t => ({...t, id: t.documentTypeId})), <FileType size={28} />, 
          () => {
            const name = prompt("Enter Document Type Name:");
            if (name) saveDocumentType({ companyId, name }).then(() => loadMetadata()).catch(e => onNotice({type:"error", text: e.message}));
          },
          () => {}, 
          (id) => deleteDocumentType(id, companyId).then(() => loadMetadata()).catch(e => onNotice({type:"error", text: e.message}))
        )}
        
        {activeView === "correspondents" && renderSimpleManager("Correspondents", correspondents.map(c => ({...c, id: c.correspondentId})), <Building2 size={28} />, 
          () => {
            const name = prompt("Enter Correspondent Name:");
            if (name) saveCorrespondent({ companyId, name }).then(() => loadMetadata()).catch(e => onNotice({type:"error", text: e.message}));
          },
          () => {}, 
          (id) => deleteCorrespondent(id, companyId).then(() => loadMetadata()).catch(e => onNotice({type:"error", text: e.message}))
        )}
        
        {activeView === "locations" && renderSimpleManager("Storage Paths", locations.map(l => ({...l, id: l.locationId})), <Folder size={28} />, 
          () => {
            const name = prompt("Enter Storage Path Name:");
            if (name) saveLocation({ companyId, name, type: "business" }).then(() => loadMetadata()).catch(e => onNotice({type:"error", text: e.message}));
          },
          () => {}, 
          (id) => deleteLocation(id, companyId).then(() => loadMetadata()).catch(e => onNotice({type:"error", text: e.message}))
        )}
      </div>

      {/* Upload Modal */}
      <UploadModal 
        open={isUploadModalOpen} 
        onClose={() => setIsUploadModalOpen(false)} 
        companyId={companyId}
        locations={locations}
        documentTypes={documentTypes}
        correspondents={correspondents}
        tags={tags}
        onSuccess={() => {
          setIsUploadModalOpen(false);
          loadDocuments();
          onNotice({ type: "success", text: "Document uploaded successfully!" });
        }}
        onNotice={onNotice}
      />
    </div>
  );
}

// --- Document Editor Component ---
function DocumentEditor({ document, locations, documentTypes, correspondents, tags, onSave, onDelete }) {
  const [title, setTitle] = useState(document.title || document.documentName || "");
  const [asn, setAsn] = useState(document.asn || "");
  const [documentDate, setDocumentDate] = useState(document.documentDate ? document.documentDate.split('T')[0] : "");
  const [documentTypeId, setDocumentTypeId] = useState(document.documentTypeId || "");
  const [correspondentId, setCorrespondentId] = useState(document.correspondentId || "");
  const [tagIds, setTagIds] = useState(document.tags?.map(t => t.tagId) || []);
  const [amount, setAmount] = useState(document.amount || "");

  const handleSave = () => {
    onSave({
      title,
      asn: asn ? parseInt(asn) : null,
      documentDate: documentDate || null,
      documentTypeId: documentTypeId || null,
      correspondentId: correspondentId || null,
      tagIds,
      amount: amount ? parseFloat(amount) : null
    });
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "20px" }}>
      <TextField label="Title" fullWidth size="small" value={title} onChange={e => setTitle(e.target.value)} />
      <TextField label="ASN (Archive Serial Number)" type="number" fullWidth size="small" value={asn} onChange={e => setAsn(e.target.value)} />
      <TextField label="Date" type="date" fullWidth size="small" value={documentDate} onChange={e => setDocumentDate(e.target.value)} InputLabelProps={{ shrink: true }} />
      
      <FormControl fullWidth size="small">
        <InputLabel>Document Type</InputLabel>
        <Select value={documentTypeId} onChange={e => setDocumentTypeId(e.target.value)} label="Document Type">
          <MenuItem value=""><em>None</em></MenuItem>
          {documentTypes.map(t => <MenuItem key={t.documentTypeId} value={t.documentTypeId}>{t.name}</MenuItem>)}
        </Select>
      </FormControl>
      
      <FormControl fullWidth size="small">
        <InputLabel>Correspondent</InputLabel>
        <Select value={correspondentId} onChange={e => setCorrespondentId(e.target.value)} label="Correspondent">
          <MenuItem value=""><em>None</em></MenuItem>
          {correspondents.map(c => <MenuItem key={c.correspondentId} value={c.correspondentId}>{c.name}</MenuItem>)}
        </Select>
      </FormControl>

      <FormControl fullWidth size="small">
        <InputLabel>Tags</InputLabel>
        <Select multiple value={tagIds} onChange={e => setTagIds(e.target.value)} label="Tags"
          renderValue={(selected) => (
            <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>
              {selected.map((value) => {
                const tg = tags.find(t => t.tagId === value);
                return <Chip key={value} label={tg ? tg.name : value} size="small" style={{ height: "20px" }} />;
              })}
            </Box>
          )}
        >
          {tags.map(t => <MenuItem key={t.tagId} value={t.tagId}>{t.name}</MenuItem>)}
        </Select>
      </FormControl>

      <TextField label="Amount" type="number" fullWidth size="small" value={amount} onChange={e => setAmount(e.target.value)} />
      
      <div style={{ display: "flex", gap: "12px", marginTop: "16px" }}>
        <ModernButton label="Save Changes" primary onClick={handleSave} />
        <ModernButton label="Delete" color="#ef4444" onClick={onDelete} />
      </div>
    </div>
  );
}

// --- Upload Modal Component ---
function UploadModal({ open, onClose, companyId, locations, documentTypes, correspondents, tags, onSuccess, onNotice }) {
  const [file, setFile] = useState(null);
  const [title, setTitle] = useState("");
  const [documentDate, setDocumentDate] = useState("");
  const [documentTypeId, setDocumentTypeId] = useState("");
  const [correspondentId, setCorrespondentId] = useState("");
  const [tagIds, setTagIds] = useState([]);
  const [amount, setAmount] = useState("");
  const [analyzing, setAnalyzing] = useState(false);
  const [uploading, setUploading] = useState(false);

  // Auto-analyze when file is selected
  useEffect(() => {
    if (!file) return;
    let ignore = false;
    setAnalyzing(true);
    analyzeBusinessDocument(file).then(data => {
      if (!ignore) {
        if (data.vendorName && correspondents.length > 0) {
          const matchedCorr = correspondents.find(c => c.name.toLowerCase() === data.vendorName.toLowerCase());
          if (matchedCorr) setCorrespondentId(matchedCorr.correspondentId);
        }
        if (data.amount) setAmount(String(data.amount));
        if (data.documentDate) setDocumentDate(String(data.documentDate).split('T')[0]);
        if (data.suggestedCategoryName && documentTypes.length > 0) {
          const matchedType = documentTypes.find(t => t.name.toLowerCase() === data.suggestedCategoryName.toLowerCase());
          if (matchedType) setDocumentTypeId(matchedType.documentTypeId);
        }
      }
    }).catch(e => console.error("Analysis failed", e)).finally(() => !ignore && setAnalyzing(false));
    return () => { ignore = true; };
  }, [file, correspondents, documentTypes]);

  const handleUpload = async () => {
    if (!file) {
      onNotice({ type: "error", text: "Please select a file." });
      return;
    }
    setUploading(true);
    try {
      await uploadBusinessDocument({
        companyId,
        file,
        documentName: title || file.name,
        documentDate: documentDate || undefined,
        documentTypeId: documentTypeId || undefined,
        correspondentId: correspondentId || undefined,
        tags: tagIds.length > 0 ? tagIds : undefined,
        amount: amount || undefined
      });
      onSuccess();
    } catch (e) {
      onNotice({ type: "error", text: e.message });
    } finally {
      setUploading(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth PaperProps={{ style: { borderRadius: "12px" } }}>
      <DialogTitle style={{ fontWeight: "700" }}>Upload Document</DialogTitle>
      <DialogContent dividers style={{ display: "flex", flexDirection: "column", gap: "20px", paddingTop: "20px" }}>
        
        <Button variant="outlined" component="label" fullWidth style={{ padding: "32px", borderStyle: "dashed", borderColor: "#94a3b8", color: "#64748b" }}>
          {file ? file.name : "Select File"}
          <input type="file" hidden onChange={e => setFile(e.target.files[0])} />
        </Button>

        {analyzing && <Typography variant="body2" color="primary" style={{ display: "flex", alignItems: "center", gap: "8px" }}><CircularProgress size={16} /> AI Analyzing document...</Typography>}

        <TextField label="Title" fullWidth size="small" value={title} onChange={e => setTitle(e.target.value)} placeholder={file ? file.name : ""} />
        <TextField label="Date" type="date" fullWidth size="small" value={documentDate} onChange={e => setDocumentDate(e.target.value)} InputLabelProps={{ shrink: true }} />
        
        <FormControl fullWidth size="small">
          <InputLabel>Document Type</InputLabel>
          <Select value={documentTypeId} onChange={e => setDocumentTypeId(e.target.value)} label="Document Type">
            <MenuItem value=""><em>None</em></MenuItem>
            {documentTypes.map(t => <MenuItem key={t.documentTypeId} value={t.documentTypeId}>{t.name}</MenuItem>)}
          </Select>
        </FormControl>
        
        <FormControl fullWidth size="small">
          <InputLabel>Correspondent</InputLabel>
          <Select value={correspondentId} onChange={e => setCorrespondentId(e.target.value)} label="Correspondent">
            <MenuItem value=""><em>None</em></MenuItem>
            {correspondents.map(c => <MenuItem key={c.correspondentId} value={c.correspondentId}>{c.name}</MenuItem>)}
          </Select>
        </FormControl>

        <FormControl fullWidth size="small">
          <InputLabel>Tags</InputLabel>
          <Select multiple value={tagIds} onChange={e => setTagIds(e.target.value)} label="Tags"
            renderValue={(selected) => (
              <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>
                {selected.map((value) => {
                  const tg = tags.find(t => t.tagId === value);
                  return <Chip key={value} label={tg ? tg.name : value} size="small" style={{ height: "20px" }} />;
                })}
              </Box>
            )}
          >
            {tags.map(t => <MenuItem key={t.tagId} value={t.tagId}>{t.name}</MenuItem>)}
          </Select>
        </FormControl>

        <TextField label="Amount" type="number" fullWidth size="small" value={amount} onChange={e => setAmount(e.target.value)} />
      </DialogContent>
      <DialogActions style={{ padding: "16px 24px" }}>
        <Button onClick={onClose} disabled={uploading}>Cancel</Button>
        <Button variant="contained" onClick={handleUpload} disabled={uploading || !file}>
          {uploading ? <CircularProgress size={24} color="inherit" /> : "Upload"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
