import JsBarcode from "jsbarcode";
import { ArrowLeft, Printer } from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";
import { listCategories } from "../api/client";

export function BarcodeManager({ companyId, patient, onBack, onNotice }) {
  const svgRef = useRef(null);
  const [categories, setCategories] = useState([]);
  const [categoryId, setCategoryId] = useState("");

  const patientName = useMemo(() => {
    if (!patient) {
      return "";
    }
    return [patient.lastName, patient.firstName].filter(Boolean).join(", ");
  }, [patient]);

  const selectedCategory = categories.find((category) => String(category.categoryId) === String(categoryId));
  const patientDisplayId = patient?.externalPatientId || String(patient?.patientId || "");
  const barcodeValue = patient?.patientId && categoryId ? `${patient.patientId}-${categoryId}` : "";

  useEffect(() => {
    let ignore = false;
    listCategories({ companyId })
      .then((data) => {
        if (!ignore) {
          setCategories(data);
          setCategoryId(data[0]?.categoryId ? String(data[0].categoryId) : "");
        }
      })
      .catch((error) => !ignore && onNotice?.({ type: "error", text: error.message }));
    return () => {
      ignore = true;
    };
  }, [companyId, onNotice]);

  useEffect(() => {
    if (!svgRef.current || !barcodeValue) {
      return;
    }

    JsBarcode(svgRef.current, barcodeValue, {
      format: "CODE128",
      displayValue: false,
      height: 58,
      margin: 0,
      width: 2,
    });
  }, [barcodeValue]);

  return (
    <section className="panel">
      <div className="panel-header compact">
        <div>
          <h2>Barcodes</h2>
        </div>
        <button className="secondary-button" type="button" onClick={onBack}>
          <ArrowLeft size={17} />
          Back
        </button>
      </div>

      <div className="barcode-toolbar">
        <label className="barcode-category-row">
          <span>Category :</span>
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
        <button className="primary-button barcode-print-button" type="button" onClick={() => window.print()} disabled={!barcodeValue}>
          <Printer size={18} />
          Print
        </button>
      </div>

      <div className="barcode-workspace">
        <div className="barcode-page-preview">
          {barcodeValue ? (
            <div className="barcode-sheet">
              <div className="barcode-topline">E-ICEBLUE</div>
              <div className="barcode-value">{barcodeValue}</div>
              <svg className="barcode-code" ref={svgRef} />
              <pre className="barcode-details">{`Patient ID : ${patientDisplayId}
Patient Name : ${patientName}
Category : ${selectedCategory?.categoryName || ""}`}</pre>
            </div>
          ) : (
            <p className="empty-state">Choose a category to generate a barcode.</p>
          )}
        </div>
      </div>
    </section>
  );
}
