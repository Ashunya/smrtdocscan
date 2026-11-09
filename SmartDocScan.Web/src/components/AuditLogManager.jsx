import { Search } from "lucide-react";
import { useEffect, useState } from "react";
import { getAuditLogs } from "../api/client";

const outcomeOptions = ["", "success", "failure"];

export function AuditLogManager({ companyId, companies = [], onNotice }) {
  const [filters, setFilters] = useState({
    companyId: String(companyId || ""),
    actor: "",
    action: "",
    outcome: "",
    fromDate: "",
    toDate: "",
  });
  const [logs, setLogs] = useState([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    setFilters((current) => ({ ...current, companyId: String(companyId || "") }));
  }, [companyId]);

  useEffect(() => {
    loadLogs();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function loadLogs(event) {
    event?.preventDefault();
    setLoading(true);
    onNotice(null);
    try {
      setLogs(await getAuditLogs({
        companyId: filters.companyId ? Number(filters.companyId) : undefined,
        actor: filters.actor,
        action: filters.action,
        outcome: filters.outcome,
        fromDate: filters.fromDate,
        toDate: filters.toDate,
      }));
    } catch (error) {
      onNotice({ type: "error", text: error.message });
    } finally {
      setLoading(false);
    }
  }

  function updateFilter(name, value) {
    setFilters((current) => ({ ...current, [name]: value }));
  }

  return (
    <section className="panel">
      <div className="panel-header compact">
        <div>
          <h2>Audit Logs</h2>
          <p>Review security-sensitive activity across SmartDocScan.</p>
        </div>
      </div>

      <form className="audit-filter-form" onSubmit={loadLogs}>
        <label>
          Company
          <select value={filters.companyId} onChange={(event) => updateFilter("companyId", event.target.value)}>
            <option value="">All companies</option>
            {companies.map((company) => (
              <option key={company.companyId} value={String(company.companyId)}>{company.companyName || `Company ${company.companyId}`}</option>
            ))}
          </select>
        </label>
        <label>
          Actor
          <input type="search" placeholder="Username" value={filters.actor} onChange={(event) => updateFilter("actor", event.target.value)} />
        </label>
        <label>
          Action
          <input type="search" placeholder="patient.update" value={filters.action} onChange={(event) => updateFilter("action", event.target.value)} />
        </label>
        <label>
          Outcome
          <select value={filters.outcome} onChange={(event) => updateFilter("outcome", event.target.value)}>
            {outcomeOptions.map((option) => (
              <option key={option || "all"} value={option}>{option || "All"}</option>
            ))}
          </select>
        </label>
        <label>
          From
          <input type="date" value={filters.fromDate} onChange={(event) => updateFilter("fromDate", event.target.value)} />
        </label>
        <label>
          To
          <input type="date" value={filters.toDate} onChange={(event) => updateFilter("toDate", event.target.value)} />
        </label>
        <button className="primary-button" type="submit" disabled={loading}>
          <Search size={18} />
          {loading ? "Loading..." : "Search"}
        </button>
      </form>

      <div className="table-wrap audit-table-wrap">
        <table>
          <thead>
            <tr>
              <th>Created (Pacific)</th>
              <th>Action</th>
              <th>Outcome</th>
              <th>Actor</th>
              <th>Company</th>
              <th>Target</th>
              <th>IP Address</th>
              <th>Details</th>
            </tr>
          </thead>
          <tbody>
            {logs.length === 0 ? (
              <tr><td colSpan="8">{loading ? "Loading audit logs..." : "No audit logs found"}</td></tr>
            ) : logs.map((log) => (
              <tr key={log.auditId}>
                <td>{formatDate(log.createdOn)}</td>
                <td>{log.action}</td>
                <td><span className={`audit-outcome ${log.outcome === "success" ? "success" : "failure"}`}>{log.outcome}</span></td>
                <td>{log.actor || ""}</td>
                <td>{log.companyId ? companies.find((company) => company.companyId === log.companyId)?.companyName || log.companyId : ""}</td>
                <td>{[log.targetType, log.targetId].filter(Boolean).join(" / ")}</td>
                <td>{log.ipAddress || ""}</td>
                <td>{log.details || ""}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function formatDate(value) {
  if (!value) {
    return "";
  }

  const utcValue = typeof value === "string" && !/[zZ]|[+-]\d{2}:\d{2}$/.test(value)
    ? `${value}Z`
    : value;

  return new Intl.DateTimeFormat("en-US", {
    timeZone: "America/Los_Angeles",
    year: "numeric",
    month: "numeric",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
    second: "2-digit",
    timeZoneName: "short",
  }).format(new Date(utcValue));
}
