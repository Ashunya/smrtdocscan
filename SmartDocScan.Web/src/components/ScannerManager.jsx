import { ScanLine, Upload } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { listCategories } from "../api/client";

export function ScannerManager({ companyId, patient, onNotice }) {
  const webTwainRef = useRef(null);
  const productKeyRef = useRef("");
  const [categories, setCategories] = useState([]);
  const [categoryId, setCategoryId] = useState("");
  const [ready, setReady] = useState(false);

  useEffect(() => {
    let ignore = false;
    listCategories({ companyId })
      .then((data) => {
        if (!ignore) {
          setCategories(data);
          setCategoryId(data[0]?.categoryId ? String(data[0].categoryId) : "");
        }
      })
      .catch((error) => !ignore && onNotice({ type: "error", text: error.message }));
    return () => {
      ignore = true;
    };
  }, [companyId, onNotice]);

  useEffect(() => {
    loadScript("/Resources/dynamsoft.webtwain.config.js")
      .then(() => loadScript("/Resources/dynamsoft.webtwain.initiate.js"))
      .then(() => initializeViewer())
      .catch(() => onNotice({ type: "error", text: "Dynamsoft resources could not be loaded." }));
  }, [onNotice]);

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
      setReady(true);
      return;
    }

    onNotice({ type: "error", text: "Dynamsoft viewer could not be initialized." });
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
    }
  }

  function registerScanEvents(webTwain) {
    if (webTwain.__smartDocScanEventsRegistered) {
      return;
    }

    webTwain.RegisterEvent?.("OnPostTransfer", () => {
      showViewer(webTwain);
      webTwain.Viewer?.gotoPage?.(webTwain.HowManyImagesInBuffer - 1);
      setReady(true);
      onNotice({ type: "success", text: `${webTwain.HowManyImagesInBuffer} scanned page(s) in buffer.` });
    });

    webTwain.RegisterEvent?.("OnPostAllTransfers", () => {
      showViewer(webTwain);
      webTwain.Viewer?.gotoPage?.(webTwain.HowManyImagesInBuffer - 1);
    });

    webTwain.__smartDocScanEventsRegistered = true;
  }

  function getWebTwain() {
    return webTwainRef.current || window.Dynamsoft?.DWT?.GetWebTwain?.("dwtcontrolContainer") || null;
  }

  function acquireImage() {
    const webTwain = getWebTwain();
    if (!webTwain) {
      onNotice({ type: "error", text: "Scanner control is not ready." });
      return;
    }

    const onFailure = (errorCode, errorString) => onNotice({ type: "error", text: errorString || `Scanner failed (${errorCode}).` });

    if (webTwain.SelectSourceAsync && webTwain.AcquireImageAsync) {
      webTwain.SelectSourceAsync()
        .then(() => webTwain.AcquireImageAsync({ IfCloseSourceAfterAcquire: true }))
        .then(() => {
          showViewer(webTwain);
          webTwain.Viewer?.gotoPage?.(Math.max(webTwain.HowManyImagesInBuffer - 1, 0));
        })
        .catch((error) => onNotice({ type: "error", text: error?.message || "Scanner acquisition failed." }));
      return;
    }

    webTwain.SelectSource(
      () => {
        webTwain.OpenSource();
        webTwain.IfDisableSourceAfterAcquire = true;
        webTwain.AcquireImage(() => showViewer(webTwain), onFailure);
      },
      () => onNotice({ type: "error", text: "Scanner source selection failed." })
    );
  }

  function uploadScannedDocument(format) {
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
    const uploadName = `ScanImage_${stamp}.${isTiff ? "tif" : "pdf"}`;
    const uploadUrl = new URL(`/api/documents/scan?Id=${companyId}&pid=${patient.patientId}&Cat_id=${categoryId}`, window.location.origin);
    webTwain.IfSSL = uploadUrl.protocol === "https:";
    webTwain.HTTPPort = uploadUrl.port ? Number(uploadUrl.port) : (webTwain.IfSSL ? 443 : 80);
    const uploadMethod = isTiff
      ? (webTwain.HTTPUploadAllThroughPostAsMultiPageTIFF || webTwain.HTTPUploadAllThroughPostAsTIFF)
      : webTwain.HTTPUploadAllThroughPostAsPDF;
    if (!uploadMethod) {
      onNotice({ type: "error", text: `Save as ${isTiff ? "TIF" : "PDF"} is not supported by this scanner component.` });
      return;
    }

    uploadMethod.call(
      webTwain,
      uploadUrl.hostname,
      `${uploadUrl.pathname}${uploadUrl.search}`,
      uploadName,
      () => onNotice({ type: "success", text: "Scanned document saved successfully." }),
      (_code, message) => {
        const msg = message || "";
        if (msg.includes("OK (200)") || msg.includes("OK (201)")) {
          onNotice({ type: "success", text: `Scanned document saved as ${isTiff ? "TIF" : "PDF"}.` });
        } else {
          onNotice({ type: "error", text: msg || "Scan upload failed." });
        }
      }
    );
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
        <label>
          Category
          <select value={categoryId} onChange={(event) => setCategoryId(event.target.value)}>
            {categories.map((category) => (
              <option key={category.categoryId} value={category.categoryId}>{category.categoryName}</option>
            ))}
          </select>
        </label>
        <button className="primary-button" type="button" onClick={acquireImage} disabled={!ready || !patient}>
          <ScanLine size={18} />
          Scan
        </button>
        <button className="secondary-button" type="button" onClick={() => uploadScannedDocument("pdf")} disabled={!ready || !patient}>
          <Upload size={18} />
          Save as PDF
        </button>
        <button className="secondary-button" type="button" onClick={() => uploadScannedDocument("tif")} disabled={!ready || !patient}>
          <Upload size={18} />
          Save as TIF
        </button>
      </div>
      <div className="scanner-frame" id="dwtcontrolContainer" />
    </section>
  );
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
