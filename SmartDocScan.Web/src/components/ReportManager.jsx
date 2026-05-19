import { Search } from "lucide-react";
import { useState } from "react";
import { getDocumentReport } from "../api/client";

export function ReportManager({ companyId, onNotice }) {
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [documents, setDocuments] = useState([]);
  const [loading, setLoading] = useState(false);

  async function runReport(event) {
    event.preventDefault();
    setLoading(true);
    onNotice(null);
    try {
      setDocuments(await getDocumentReport({ companyId, fromDate, toDate }));
    } catch (error) {
      onNotice({ type: "error", text: error.message });
    } finally {
      setLoading(false);
    }
  }

  return (
    <section className="panel">
      <div className="panel-header compact">
        <div>
          <h2>Document Report</h2>
          <p>Review uploaded document counts and pages by date.</p>
        </div>
      </div>
      <form className="report-form" onSubmit={runReport}>
        <label>
          From
          <input type="date" value={fromDate} onChange={(event) => setFromDate(event.target.value)} />
        </label>
        <label>
          To
          <input type="date" value={toDate} onChange={(event) => setToDate(event.target.value)} />
        </label>
        <button className="primary-button" type="submit" disabled={loading}>
          <Search size={18} />
          {loading ? "Loading..." : "Run Report"}
        </button>
      </form>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Document</th>
              <th>Category</th>
              <th>Pages</th>
              <th>Uploaded</th>
              <th>Patient Record</th>
            </tr>
          </thead>
          <tbody>
            {documents.length === 0 ? (
              <tr><td colSpan="5">{loading ? "Loading report..." : "No report data"}</td></tr>
            ) : documents.map((document) => (
              <tr key={document.documentId}>
                <td>{document.documentName}</td>
                <td>{document.categoryName || ""}</td>
                <td>{document.numberOfPages}</td>
                <td>{new Date(document.date).toLocaleString()}</td>
                <td>{document.patientId}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
