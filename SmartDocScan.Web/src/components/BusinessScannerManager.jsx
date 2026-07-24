import { ScanLine, Settings2, Trash2, Upload, XCircle } from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";
import { 
  uploadBusinessDocument, 
  listLocations, 
  listDocumentTypes, 
  listCorrespondents, 
  listTags 
} from "../api/client";
import { TextField, MenuItem, Select, FormControl, InputLabel, Chip, Box, OutlinedInput } from "@mui/material";

export function BusinessScannerManager({ companyId, onNotice, onSaved }) {
  const webTwainRef = useRef(null);
  const productKeyRef = useRef("");
  const lastPageCountRef = useRef(0);
  
  const [locations, setLocations] = useState([]);
  const [documentTypes, setDocumentTypes] = useState([]);
  const [correspondents, setCorrespondents] = useState([]);
  const [tags, setTags] = useState([]);

  const [locationId, setLocationId] = useState("");
  const [documentTypeId, setDocumentTypeId] = useState("");
  const [correspondentId, setCorrespondentId] = useState("");
  const [selectedTags, setSelectedTags] = useState([]);
  const [title, setTitle] = useState("");

  const [ready, setReady] = useState(false);
  const [pageCount, setPageCount] = useState(0);
  const [documentName, setDocumentName] = useState("");
  const [documentDate, setDocumentDate] = useState("");
  const [saving, setSaving] = useState(false);
  const [acquiring, setAcquiring] = useState(false);

  useEffect(() => {
    let ignore = false;
    Promise.all([
        listLocations({ companyId }),
        listDocumentTypes({ companyId }),
        listCorrespondents({ companyId }),
        listTags({ companyId })
      ])
        .then(([locs, types, corrs, tgs]) => {
          if (!ignore) {
            setLocations(locs);
            setDocumentTypes(types);
            setCorrespondents(corrs);
            setTags(tgs);
          }
        }).catch((error) => !ignore && onNotice({ type: "error", text: error.message }));
    return () => {
      ignore = true;
    };
  }, [companyId, onNotice]);

  useEffect(() => {
    loadScript("/Resources/dynamsoft.webtwain.config.js")
      .then(() => applyDynamsoftProductKey())
      .then(() => loadScript("/Resources/dynamsoft.webtwain.initiate.js"))
      .then(() => initializeViewer())
      .catch(() => onNotice({ type: "error", text: "Dynamsoft resources could not be loaded." }));
  }, [onNotice]);

  useEffect(() => {
    if (!ready) {
      return;
    }

    clearScannerBuffer();
    setDocumentName("");
  }, [ready]);

  useEffect(() => {
    if (!ready) {
      return undefined;
    }

    const timer = window.setInterval(() => {
      updatePageCount(getWebTwain());
    }, 500);

    return () => window.clearInterval(timer);
  }, [ready]);

  async function initializeViewer() {
    const dwt = window.Dynamsoft?.DWT;
    if (!dwt) {
      throw new Error("DWT is not available.");
    }

    dwt.ResourcesPath = "/Resources";

    const existing = await waitForWebTwain(dwt);
    if (existing) {
      webTwainRef.current = existing;
      registerScanEvents(existing);
      showViewer(existing);
      updatePageCount(existing);
      setReady(true);
      return;
    }

    onNotice({ type: "error", text: "Dynamsoft viewer could not be initialized." });
  }

  function applyDynamsoftProductKey() {
    const productKey = import.meta.env.VITE_DYNAMSOFT_PRODUCT_KEY?.trim();
    const dwt = window.Dynamsoft?.DWT;
    if (!productKey || !dwt) {
      return;
    }

    dwt.ProductKey = productKey;
    dwt.productKeys = productKey;
    productKeyRef.current = productKey;
  }

  function showViewer(webTwain) {
    const container = document.getElementById("dwtcontrolContainer");
    if (container && webTwain.Viewer?.bind) {
      webTwain.Viewer.bind(container);
      webTwain.Viewer.width = "100%";
      webTwain.Viewer.height = "520px";
      webTwain.Viewer.show();
      webTwain.Viewer.setViewMode?.(1, 1);
      if (webTwain.HowManyImagesInBuffer > 0) {
        webTwain.Viewer.gotoPage?.(webTwain.HowManyImagesInBuffer - 1);
      }
      updatePageCount(webTwain);
    }
  }

  function registerScanEvents(webTwain) {
    if (webTwain.__smartDocScanEventsRegistered) {
      return;
    }

    webTwain.RegisterEvent?.("OnPostTransfer", () => {
      showViewer(webTwain);
      webTwain.Viewer?.gotoPage?.(webTwain.HowManyImagesInBuffer - 1);
      updatePageCount(webTwain);
      setReady(true);
      onNotice({ type: "success", text: `${webTwain.HowManyImagesInBuffer} scanned page(s) in buffer.` });
    });

    webTwain.RegisterEvent?.("OnPostAllTransfers", () => {
      showViewer(webTwain);
      webTwain.Viewer?.gotoPage?.(webTwain.HowManyImagesInBuffer - 1);
      updatePageCount(webTwain);
    });

    webTwain.RegisterEvent?.("OnBitmapChanged", () => updatePageCount(webTwain));
    webTwain.RegisterEvent?.("OnImageAreaSelected", () => updatePageCount(webTwain));
    webTwain.RegisterEvent?.("OnMouseClick", () => updatePageCount(webTwain));

    webTwain.__smartDocScanEventsRegistered = true;
  }

  function getWebTwain() {
    return webTwainRef.current || window.Dynamsoft?.DWT?.GetWebTwain?.("dwtcontrolContainer") || null;
  }

  async function acquireImage() {
    const webTwain = getWebTwain();
    if (!webTwain || acquiring) {
      onNotice({ type: "error", text: "Scanner control is not ready." });
      return;
    }

    setAcquiring(true);
    onNotice(null);
    try {
      await selectScannerSource(webTwain, false);
      await acquireFromSelectedSource(webTwain);
      showViewer(webTwain);
      webTwain.Viewer?.gotoPage?.(Math.max(webTwain.HowManyImagesInBuffer - 1, 0));
      updatePageCount(webTwain);
    } catch (error) {
      onNotice({ type: "error", text: error?.message || "Scanner acquisition failed." });
    } finally {
      setAcquiring(false);
    }
  }

  async function changeScanner() {
    const webTwain = getWebTwain();
    if (!webTwain || acquiring) {
      return;
    }

    setAcquiring(true);
    onNotice(null);
    try {
      await selectScannerSource(webTwain, true);
      onNotice({ type: "success", text: "Scanner selection saved for this workstation." });
    } catch (error) {
      onNotice({ type: "error", text: error?.message || "Scanner source selection failed." });
    } finally {
      setAcquiring(false);
    }
  }

  const handleSave = () => {
    if (!webTwainRef.current) return;
    if (pageCount === 0) {
      onNotice({ type: "error", text: "No pages to upload." });
      return;
    }
    if (!documentName) {
      onNotice({ type: "error", text: "Please enter a document name." });
      return;
    }

    setSaving(true);
    const successIndices = [];
    for (let i = 0; i < pageCount; i++) {
      successIndices.push(i);
    }

    webTwainRef.current.ConvertToBlob(
      successIndices,
      window.Dynamsoft.DWT.EnumDWT_ImageType.IT_PDF,
      (blob) => {
        const file = new File([blob], documentName + ".pdf", { type: "application/pdf" });

        uploadBusinessDocument({
          companyId,
          locationId: locationId || null,
          documentTypeId: documentTypeId || null,
          correspondentId: correspondentId || null,
          tags: selectedTags.length > 0 ? selectedTags : null,
          title: title || null,
          documentDate: documentDate || null,
          file,
        })
          .then(() => {
            onNotice({ type: "success", text: "Business document saved successfully." });
            onSaved();
          })
          .catch((error) => {
            onNotice({ type: "error", text: error.message });
            setSaving(false);
          });
      },
      (errorCode, errorString) => {
        onNotice({ type: "error", text: `Failed to convert to PDF: ${errorString}` });
        setSaving(false);
      }
    );
  };

  function deleteCurrentPage() {
    const webTwain = getWebTwain();
    if (!webTwain || webTwain.HowManyImagesInBuffer === 0) {
      onNotice({ type: "error", text: "No scanned pages are available." });
      return;
    }

    const index = Number.isInteger(webTwain.CurrentImageIndexInBuffer)
      ? webTwain.CurrentImageIndexInBuffer
      : Math.max(webTwain.HowManyImagesInBuffer - 1, 0);
    webTwain.RemoveImage?.(index);
    updatePageCount(webTwain);
    window.setTimeout(() => updatePageCount(webTwain), 150);
    if (webTwain.HowManyImagesInBuffer > 0) {
      webTwain.Viewer?.gotoPage?.(Math.min(index, webTwain.HowManyImagesInBuffer - 1));
    }
  }

  function clearPages() {
    const webTwain = getWebTwain();
    if (!webTwain || webTwain.HowManyImagesInBuffer === 0) {
      return;
    }

    webTwain.RemoveAllImages?.();
    updatePageCount(webTwain);
    window.setTimeout(() => updatePageCount(webTwain), 150);
  }

  function clearScannerBuffer() {
    const webTwain = getWebTwain();
    if (!webTwain) {
      setPageCount(0);
      return;
    }

    if (webTwain.HowManyImagesInBuffer > 0) {
      webTwain.RemoveAllImages?.();
    }
    updatePageCount(webTwain);
    window.setTimeout(() => updatePageCount(webTwain), 150);
  }

  function updatePageCount(webTwain) {
    const nextPageCount = Number(webTwain?.HowManyImagesInBuffer) || 0;
    if (lastPageCountRef.current !== nextPageCount) {
      lastPageCountRef.current = nextPageCount;
      setPageCount(nextPageCount);
    }
  }

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <h2>Scan Document</h2>
        </div>
      </div>
      <div className="scanner-toolbar">
        <div className="scanner-form">
          <div className="scanner-form-row">
            <TextField
              label="Document Name"
              value={documentName}
              onChange={(e) => setDocumentName(e.target.value)}
              disabled={saving}
              required
              fullWidth
              size="small"
            />
          </div>
          <div className="scanner-form-row">
            <TextField
              label="Title"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              disabled={saving}
              fullWidth
              size="small"
            />
          </div>
          <div className="scanner-form-row">
            <FormControl size="small" fullWidth disabled={saving}>
              <InputLabel>Document Type</InputLabel>
              <Select value={documentTypeId} onChange={e => setDocumentTypeId(e.target.value)} label="Document Type">
                <MenuItem value=""><em>None</em></MenuItem>
                {documentTypes.map(t => <MenuItem key={t.documentTypeId} value={t.documentTypeId}>{t.name}</MenuItem>)}
              </Select>
            </FormControl>
          </div>
          <div className="scanner-form-row">
            <FormControl size="small" fullWidth disabled={saving}>
              <InputLabel>Correspondent</InputLabel>
              <Select value={correspondentId} onChange={e => setCorrespondentId(e.target.value)} label="Correspondent">
                <MenuItem value=""><em>None</em></MenuItem>
                {correspondents.map(c => <MenuItem key={c.correspondentId} value={c.correspondentId}>{c.name}</MenuItem>)}
              </Select>
            </FormControl>
          </div>
          <div className="scanner-form-row">
            <FormControl size="small" fullWidth disabled={saving}>
              <InputLabel>Storage Path (Location)</InputLabel>
              <Select value={locationId} onChange={e => setLocationId(e.target.value)} label="Storage Path (Location)">
                <MenuItem value=""><em>None</em></MenuItem>
                {locations.map(l => <MenuItem key={l.locationId} value={l.locationId}>{l.locationName}</MenuItem>)}
              </Select>
            </FormControl>
          </div>
          <div className="scanner-form-row">
            <FormControl size="small" fullWidth disabled={saving}>
              <InputLabel>Tags</InputLabel>
              <Select
                multiple
                value={selectedTags}
                onChange={e => setSelectedTags(e.target.value)}
                input={<OutlinedInput label="Tags" />}
                renderValue={(selected) => (
                  <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>
                    {selected.map((value) => {
                      const tag = tags.find(t => t.tagId === value);
                      return tag ? <Chip key={value} label={tag.name} size="small" style={{ backgroundColor: tag.color, color: 'white' }} /> : null;
                    })}
                  </Box>
                )}
              >
                {tags.map((tag) => (
                  <MenuItem key={tag.tagId} value={tag.tagId}>{tag.name}</MenuItem>
                ))}
              </Select>
            </FormControl>
          </div>
          <div className="scanner-form-row">
            <TextField
              label="Document Date"
              type="date"
              value={documentDate}
              onChange={(e) => setDocumentDate(e.target.value)}
              InputLabelProps={{ shrink: true }}
              disabled={saving}
              fullWidth
              size="small"
            />
          </div>
          <div className="scanner-form-actions">
            <div className="scanner-count-pill" aria-live="polite">
              {pageCount} {pageCount === 1 ? "page" : "pages"}
            </div>
            <button className="primary-button" type="button" onClick={acquireImage} disabled={!ready || saving || acquiring}>
              <ScanLine size={18} />
              {acquiring ? "Starting..." : "Scan"}
            </button>
            <button className="secondary-button" type="button" onClick={changeScanner} disabled={!ready || saving || acquiring}>
              <Settings2 size={18} />
              Change scanner
            </button>
            <button className="secondary-button danger-text" type="button" onClick={deleteCurrentPage} disabled={!ready || pageCount === 0 || saving}>
              <Trash2 size={18} />
              Delete Page
            </button>
            <button className="secondary-button" type="button" onClick={clearPages} disabled={!ready || pageCount === 0 || saving}>
              <XCircle size={18} />
              Clear
            </button>
            <button className="secondary-button" type="button" onClick={handleSave} disabled={!ready || pageCount === 0 || saving}>
              <Upload size={18} />
              {saving ? "Saving..." : "Save Document"}
            </button>
          </div>
        </div>
      </div>
      <div className="scan-review-note">
        {pageCount > 0 ? `${pageCount} page(s) ready for review. Delete unwanted pages before saving.` : "Scan pages will appear below for review before saving."}
      </div>
      <div className="scanner-frame" id="dwtcontrolContainer" />
    </section>
  );
}

const SCANNER_SOURCE_KEY = "smartdocscan-scanner-source-index";

async function selectScannerSource(webTwain, forceSelection) {
  const savedSourceValue = window.localStorage.getItem(SCANNER_SOURCE_KEY);
  const savedSourceIndex = Number(savedSourceValue);
  const sourceCount = Number(webTwain.SourceCount) || 0;
  const canUseSavedSource = !forceSelection
    && savedSourceValue !== null
    && Number.isInteger(savedSourceIndex)
    && savedSourceIndex >= 0
    && (sourceCount === 0 || savedSourceIndex < sourceCount);

  if (canUseSavedSource) {
    try {
      if (typeof webTwain.SelectSourceByIndexAsync === "function") {
        await webTwain.SelectSourceByIndexAsync(savedSourceIndex);
      } else if (typeof webTwain.SelectSourceByIndex === "function") {
        const selected = webTwain.SelectSourceByIndex(savedSourceIndex);
        if (selected === false) {
          throw new Error("Saved scanner is unavailable.");
        }
      } else {
        throw new Error("Saved scanner selection is not supported.");
      }
      return;
    } catch {
      window.localStorage.removeItem(SCANNER_SOURCE_KEY);
    }
  }

  await showSourceSelector(webTwain);
  const selectedSourceIndex = findSelectedSourceIndex(webTwain);
  if (selectedSourceIndex >= 0) {
    window.localStorage.setItem(SCANNER_SOURCE_KEY, String(selectedSourceIndex));
  }
}

function showSourceSelector(webTwain) {
  if (typeof webTwain.SelectSourceAsync === "function") {
    return webTwain.SelectSourceAsync();
  }

  return new Promise((resolve, reject) => {
    webTwain.SelectSource(
      resolve,
      (_code, message) => reject(new Error(message || "Scanner source selection was cancelled."))
    );
  });
}

function findSelectedSourceIndex(webTwain) {
  const currentSourceName = String(webTwain.CurrentSourceName || "");
  const sourceCount = Number(webTwain.SourceCount) || 0;
  for (let index = 0; index < sourceCount; index += 1) {
    if (String(webTwain.GetSourceNameItems?.(index) || "") === currentSourceName) {
      return index;
    }
  }
  return Number.isInteger(webTwain.CurrentSourceIndex) ? webTwain.CurrentSourceIndex : -1;
}

function acquireFromSelectedSource(webTwain) {
  const settings = {
    IfShowUI: false,
    IfCloseSourceAfterAcquire: true,
    IfFeederEnabled: true,
    IfDuplexEnabled: true,
    PixelType: 0,
    Resolution: 300,
  };

  if (typeof webTwain.AcquireImageAsync === "function") {
    return webTwain.AcquireImageAsync(settings);
  }

  return new Promise((resolve, reject) => {
    try {
      webTwain.OpenSource();
      webTwain.IfShowUI = false;
      webTwain.IfDisableSourceAfterAcquire = true;
      webTwain.IfFeederEnabled = true;
      webTwain.IfDuplexEnabled = true;
      webTwain.PixelType = 0;
      webTwain.Resolution = 300;
      webTwain.AcquireImage(
        resolve,
        (errorCode, errorString) => reject(new Error(errorString || `Scanner failed (${errorCode}).`))
      );
    } catch (error) {
      reject(error);
    }
  });
}

function buildDocumentFileName(name, fallback, extension) {
  const base = (name || fallback).trim() || fallback;
  const withoutPath = base.split(/[\\/]/).pop() || fallback;
  const withoutExtension = withoutPath.replace(/\.[^.]+$/, "");
  return `${withoutExtension}.${extension}`;
}

function convertScansToBlob(webTwain, format) {
  const imageType = getDwtImageType(format);
  const indices = Array.from({ length: webTwain.HowManyImagesInBuffer }, (_value, index) => index);

  if (typeof webTwain.convertToBlob === "function") {
    return webTwain.convertToBlob(imageType, indices);
  }

  if (typeof webTwain.ConvertToBlob === "function") {
    return new Promise((resolve, reject) => {
      webTwain.ConvertToBlob(
        indices,
        imageType,
        (blob) => resolve(blob),
        (code, message) => reject(new Error(message || `Scanner document conversion failed (${code}).`))
      );
    });
  }

  return Promise.reject(new Error("This Dynamsoft scanner component cannot export reviewed pages for upload."));
}

function getDwtImageType(format) {
  const imageTypes = window.Dynamsoft?.DWT?.EnumDWT_ImageType || {};
  if (format === "tif") {
    return imageTypes.IT_TIF ?? 2;
  }
  return imageTypes.IT_PDF ?? 4;
}

function loadScript(src) {
  return new Promise((resolve, reject) => {
    if (document.querySelector(`script[src="${src}"]`)) {
      resolve();
      return;
    }
    const script = document.createElement("script");
    script.src = src;
    script.onload = resolve;
    script.onerror = reject;
    document.body.appendChild(script);
  });
}

function waitForWebTwain(dwt) {
  return new Promise((resolve) => {
    const started = Date.now();
    const timer = window.setInterval(() => {
      const webTwain = dwt.GetWebTwain?.("dwtcontrolContainer");
      if (webTwain || Date.now() - started > 10000) {
        window.clearInterval(timer);
        resolve(webTwain || null);
      }
    }, 100);
  });
}
