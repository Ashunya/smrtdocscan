import { Search } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { getScannedPagesReport, listUsers } from "../api/client";

const numberFormat = new Intl.NumberFormat("en-US");

export function ScannedPagesReport({ companies = [], onNotice }) {
  const [filters, setFilters] = useState({
    companyId: "",
    userName: "",
    fromDate: "",
    toDate: "",
    groupBy: "user",
  });
  const [rows, setRows] = useState([]);
  const [groupedByDay, setGroupedByDay] = useState(false);
  const [userOptions, setUserOptions] = useState([]);
  const [loading, setLoading] = useState(false);
  const [hasRun, setHasRun] = useState(false);

  useEffect(() => {
    if (!filters.companyId) {
      setUserOptions([]);
      return;
    }
    let ignore = false;
    listUsers({ companyId: Number(filters.companyId) })
      .then((users) => {
        if (!ignore) {
          setUserOptions(users.map((user) => user.username).filter(Boolean).sort((a, b) => a.localeCompare(b)));
        }
      })
      .catch(() => {
        if (!ignore) {
          setUserOptions([]);
        }
      });
    return () => {
      ignore = true;
    };
  }, [filters.companyId]);

  const totals = useMemo(() => ({
    documents: rows.reduce((sum, row) => sum + row.documents, 0),
    pages: rows.reduce((sum, row) => sum + row.pages, 0),
    users: new Set(rows.map((row) => `${row.companyId}|${row.userName.toLowerCase()}`)).size,
  }), [rows]);

  function updateFilter(name, value) {
    setFilters((current) => ({ ...current, [name]: value }));
  }

  async function runReport(event) {
    event.preventDefault();
    if (filters.fromDate && filters.toDate && filters.fromDate > filters.toDate) {
      onNotice({ type: "error", text: "From date must be on or before To date." });
      return;
    }
    setLoading(true);
    onNotice(null);
    try {
      const groupBy = filters.groupBy;
      setRows(await getScannedPagesReport({
        companyId: filters.companyId ? Number(filters.companyId) : undefined,
        userName: filters.userName.trim(),
        fromDate: filters.fromDate,
        toDate: filters.toDate,
        groupBy,
      }));
      setGroupedByDay(groupBy === "day");
      setHasRun(true);
    } catch (error) {
      onNotice({ type: "error", text: error.message });
    } finally {
      setLoading(false);
    }
  }

  const columnCount = groupedByDay ? 5 : 4;

  return (
    <section className="panel">
      <div className="panel-header compact">
        <div>
          <h2>Scanned Pages Report</h2>
          <p>Page counts by company and user for scanned and uploaded documents. Deleted documents are excluded.</p>
        </div>
      </div>

      <form className="audit-filter-form page-report-form" onSubmit={runReport}>
        <label>
          Company
          <select value={filters.companyId} onChange={(event) => updateFilter("companyId", event.target.value)}>
            <option value="">All companies</option>
            {companies.map((company) => (
              <option key={company.companyId} value={company.companyId}>{company.companyName || `Company ${company.companyId}`}</option>
            ))}
          </select>
        </label>
        <label>
          User
          <input
            type="search"
            placeholder="All users"
            list="page-report-users"
            value={filters.userName}
            onChange={(event) => updateFilter("userName", event.target.value)}
          />
          <datalist id="page-report-users">
            {userOptions.map((username) => <option key={username} value={username} />)}
          </datalist>
        </label>
        <label>
          From
          <input type="date" value={filters.fromDate} onChange={(event) => updateFilter("fromDate", event.target.value)} />
        </label>
        <label>
          To
          <input type="date" value={filters.toDate} onChange={(event) => updateFilter("toDate", event.target.value)} />
        </label>
        <label>
          Group by
          <select value={filters.groupBy} onChange={(event) => updateFilter("groupBy", event.target.value)}>
            <option value="user">Company and user</option>
            <option value="day">Company, user, and day</option>
          </select>
        </label>
        <button className="primary-button" type="submit" disabled={loading}>
          <Search size={18} />
          {loading ? "Loading..." : "Run Report"}
        </button>
      </form>

      {hasRun && rows.length > 0 && (
        <div className="page-report-totals" aria-live="polite">
          <span><strong>{numberFormat.format(totals.pages)}</strong> pages</span>
          <span><strong>{numberFormat.format(totals.documents)}</strong> documents</span>
          <span><strong>{numberFormat.format(totals.users)}</strong> users</span>
        </div>
      )}

      <div className="table-wrap audit-table-wrap">
        <table>
          <thead>
            <tr>
              {groupedByDay && <th>Date</th>}
              <th>Company</th>
              <th>User</th>
              <th className="numeric-cell">Documents</th>
              <th className="numeric-cell">Pages</th>
            </tr>
          </thead>
          <tbody>
            {rows.length === 0 ? (
              <tr>
                <td colSpan={columnCount}>
                  {loading ? "Loading report..." : hasRun ? "No documents match these filters" : "Choose filters and run the report"}
                </td>
              </tr>
            ) : rows.map((row) => (
              <tr key={`${row.day || ""}|${row.companyId}|${row.userName}`}>
                {groupedByDay && <td>{formatDay(row.day)}</td>}
                <td>{row.companyName || `Company ${row.companyId}`}</td>
                <td>{row.userName}</td>
                <td className="numeric-cell">{numberFormat.format(row.documents)}</td>
                <td className="numeric-cell">{numberFormat.format(row.pages)}</td>
              </tr>
            ))}
          </tbody>
          {rows.length > 0 && (
            <tfoot>
              <tr>
                <th colSpan={columnCount - 2}>Total</th>
                <th className="numeric-cell">{numberFormat.format(totals.documents)}</th>
                <th className="numeric-cell">{numberFormat.format(totals.pages)}</th>
              </tr>
            </tfoot>
          )}
        </table>
      </div>
    </section>
  );
}

function formatDay(value) {
  if (!value) {
    return "";
  }
  const [year, month, day] = String(value).slice(0, 10).split("-");
  return `${month}/${day}/${year}`;
}
