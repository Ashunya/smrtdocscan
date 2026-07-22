import { ScanLine, Settings2, Trash2, Upload, XCircle } from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";
import { listCategories, uploadDocument } from "../api/client";

export function ScannerManager({ companyId, patient, initialCategoryId, onNotice, onSaved }) {
  const webTwainRef = useRef(null);
  const productKeyRef = useRef("");
  const lastPageCountRef = useRef(0);
  const [categories, setCategories] = useState([]);
  const [categoryId, setCategoryId] = useState("");
  const [ready, setReady] = useState(false);
  const [pageCount, setPageCount] = useState(0);
  const [documentName, setDocumentName] = useState("");
  const [dateOfService, setDateOfService] = useState("");
  const [saving, setSaving] = useState(false);
  const [acquiring, setAcquiring] = useState(false);

  const formattedCategoryOptions = useMemo(() => {
    const roots = categories.filter((c) => !c.parentId);
    const subGroups = [];
    roots.forEach((parent) => {
      const children = categories.filter((c) => c.parentId === parent.categoryId);
      if (children.length > 0) {
        subGroups.push({
          type: "group",
          label: parent.categoryName,
          options: children
        });
      } else {
        subGroups.push({
          type: "option",
          categoryId: parent.categoryId,
          categoryName: parent.categoryName
        });
      }
    });
    categories.forEach((c) => {
      if (c.parentId && !categories.some((p) => p.categoryId === c.parentId)) {
        subGroups.push({
          type: "option",
          categoryId: c.categoryId,
          categoryName: c.categoryName
        });
      }
    });
    return subGroups;
  }, [categories]);

  useEffect(() => {
    let ignore = false;
    listCategories({ companyId })
      .then((data) => {
        if (!ignore) {
          setCategories(data);
          setCategoryId((currentCategoryId) => selectAvailableCategory(data, initialCategoryId || currentCategoryId, companyId));
        }
      })
      .catch((error) => !ignore && onNotice({ type: "error", text: error.message }));
    return () => {
      ignore = true;
    };
  }, [companyId, initialCategoryId, onNotice]);

  useEffect(() => {
    loadScript("/Resources/dynamsoft.webtwain.config.js")
      .then(() => applyDynamsoftProductKey())
      .then(() => loadScript("/Resources/dynamsoft.webtwain.initiate.js"))
      .then(() => initializeViewer())
      .catch(() => onNotice({ type: "error", text: "Dynamsoft resources could not be loaded." }));
  }, [onNotice]);

  useEffect(() => {
    if (!ready || !patient?.patientId) {
      return;
    }

    clearScannerBuffer();
    setDocumentName("");
    setDateOfService("");
  }, [patient?.patientId, ready]);

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

  function changeCategory(nextCategoryId) {
    setCategoryId(nextCategoryId);
    if (nextCategoryId) {
      window.localStorage.setItem(getCategoryPreferenceKey(companyId), String(nextCategoryId));
    }
  }

  async function uploadScannedDocument(format) {
    if (!categoryId) {
      onNotice({ type: "error", text: "Choose a category first." });
      return;
    }

    const webTwain = getWebTwain();
    if (!webTwain || webTwain.HowManyImagesInBuffer === 0) {
      onNotice({ type: "error", text: "No scanned pages are available." });
      return;
    }

    const stamp = new Date().toISOString().replace(/[-:.TZ]/g, "");
    const isTiff = format === "tif";
    const currentPageCount = Number(webTwain.HowManyImagesInBuffer) || 0;
    const selectedCategoryId = Number(categoryId);
    const uploadName = buildDocumentFileName(documentName, `ScanImage_${stamp}`, isTiff ? "tif" : "pdf");

    setSaving(true);
    try {
      const blob = await convertScansToBlob(webTwain, isTiff ? "tif" : "pdf");
      const file = new File([blob], uploadName, { type: blob.type || (isTiff ? "image/tiff" : "application/pdf") });
      await uploadDocument({
        companyId,
        patientId: patient.patientId,
        categoryId: selectedCategoryId,
        documentName: uploadName,
        dateOfService,
        pages: currentPageCount,
        uploadedBy: "Scanner",
        file,
      });
      clearScannerBuffer();
      setDocumentName("");
      setDateOfService("");
      onNotice({ type: "success", text: `Scanned document saved as ${isTiff ? "TIF" : "PDF"}.` });
      onSaved?.();
    } catch (error) {
      onNotice({ type: "error", text: error?.message || "Scan upload failed." });
    } finally {
      setSaving(false);
    }
  }

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
          <p>{patient ? `Scanning for ${patient.lastName}, ${patient.firstName}` : "Select a patient from Find Patient first."}</p>
        </div>
      </div>
      <div className="scanner-toolbar">
        <div className="scanner-fields">
          <label>
            Category
            <select value={categoryId} onChange={(event) => changeCategory(event.target.value)} disabled={categories.length === 0 || saving || acquiring}>
              {categories.length === 0 ? (
                <option value="">No categories</option>
              ) : (
                <>
                  <option value="">Select Category...</option>
                  {formattedCategoryOptions.map((item, idx) => {
                    if (item.type === "group") {
                      return (
                        <optgroup key={idx} label={item.label}>
                          {item.options.map((opt) => (
                            <option key={opt.categoryId} value={opt.categoryId}>
                              {opt.categoryName}
                            </option>
                          ))}
                        </optgroup>
                      );
                    }
                    return (
                      <option key={item.categoryId} value={item.categoryId}>
                        {item.categoryName}
                      </option>
                    );
                  })}
                </>
              )}
            </select>
          </label>
          <label>
            Document Name
            <input type="text" value={documentName} onChange={(event) => setDocumentName(event.target.value)} placeholder="Optional name" />
          </label>
          <label>
            Date of Service
            <input type="date" value={dateOfService} onChange={(event) => setDateOfService(event.target.value)} />
          </label>
        </div>
        <div className="scanner-actions">
          <div className="scanner-count-pill" aria-live="polite">
            {pageCount} {pageCount === 1 ? "page" : "pages"}
          </div>
          <button className="primary-button" type="button" onClick={acquireImage} disabled={!ready || !patient || !categoryId || saving || acquiring}>
            <ScanLine size={18} />
            {acquiring ? "Starting..." : "Scan"}
          </button>
          <button className="secondary-button" type="button" onClick={changeScanner} disabled={!ready || saving || acquiring}>
            <Settings2 size={18} />
            Change scanner
          </button>
          <button className="secondary-button danger-text" type="button" onClick={deleteCurrentPage} disabled={!ready || !patient || pageCount === 0 || saving}>
            <Trash2 size={18} />
            Delete Page
          </button>
          <button className="secondary-button" type="button" onClick={clearPages} disabled={!ready || !patient || pageCount === 0 || saving}>
            <XCircle size={18} />
            Clear
          </button>
          <button className="secondary-button" type="button" onClick={() => uploadScannedDocument("pdf")} disabled={!ready || !patient || pageCount === 0 || saving}>
            <Upload size={18} />
            {saving ? "Saving..." : "Save as PDF"}
          </button>
          <button className="secondary-button" type="button" onClick={() => uploadScannedDocument("tif")} disabled={!ready || !patient || pageCount === 0 || saving}>
            <Upload size={18} />
            {saving ? "Saving..." : "Save as TIF"}
          </button>
        </div>
      </div>
      <div className="scan-review-note">
        {pageCount > 0 ? `${pageCount} page(s) ready for review. Delete unwanted pages before saving.` : "Scan pages will appear below for review before saving."}
      </div>
      <div className="scanner-frame" id="dwtcontrolContainer" />
    </section>
  );
}

function getCategoryPreferenceKey(companyId) {
  return `smartdocscan-category:${companyId}`;
}

function selectAvailableCategory(categories, preferredCategoryId, companyId) {
  const availableIds = new Set(categories.map((category) => String(category.categoryId)));
  if (preferredCategoryId && availableIds.has(String(preferredCategoryId))) {
    return String(preferredCategoryId);
  }

  const savedCategoryId = window.localStorage.getItem(getCategoryPreferenceKey(companyId));
  if (savedCategoryId && availableIds.has(savedCategoryId)) {
    return savedCategoryId;
  }

  return categories[0]?.categoryId ? String(categories[0].categoryId) : "";
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
