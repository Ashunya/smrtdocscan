import { CheckCircle2, Trash2 } from "lucide-react";
import { useEffect, useState } from "react";
import { deleteCompany, listCompanies, saveCompany } from "../api/client";

const blankCompany = {
  companyName: "",
  owner: "",
  address: "",
  location: "",
  phone: "",
  barcode: false,
  inactive: false,
  microsoftTenantId: "",
  microsoftTenantName: "",
  microsoftTenantEnabled: true,
};

export function CompanyManager({ companyId, onCompanyChange, onNotice }) {
  const [companies, setCompanies] = useState([]);
  const [form, setForm] = useState(blankCompany);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let ignore = false;
    setLoading(true);
    listCompanies()
      .then((data) => {
        if (!ignore) {
          setCompanies(data);
        }
      })
      .catch((error) => {
        if (!ignore) {
          onNotice({ type: "error", text: error.message });
        }
      })
      .finally(() => {
        if (!ignore) {
          setLoading(false);
        }
      });
    return () => {
      ignore = true;
    };
  }, [onNotice]);

  function editCompany(company) {
    setForm(company);
  }

  function updateField(field, value) {
    setForm((current) => ({ ...current, [field]: value }));
  }

  async function handleSubmit(event) {
    event.preventDefault();
    setSaving(true);
    onNotice(null);
    try {
      const company = await saveCompany(form);
      setCompanies((current) => [company, ...current.filter((item) => item.companyId !== company.companyId)]);
      setForm(blankCompany);
      onNotice({ type: "success", text: "Company saved." });
    } catch (error) {
      onNotice({ type: "error", text: error.message });
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(company) {
    const ok = window.confirm(`Delete company ${company.companyName}?`);
    if (!ok) return;
    onNotice(null);
    try {
      await deleteCompany(company.companyId);
      setCompanies((current) => current.filter((item) => item.companyId !== company.companyId));
      onNotice({ type: "success", text: "Company deleted." });
    } catch (error) {
      onNotice({ type: "error", text: error.message });
    }
  }

  return (
    <section className="panel">
      <div className="panel-header compact">
        <div>
          <h2>Companies</h2>
          <p>Select the company context for patients, boxes, users, and documents.</p>
        </div>
      </div>

      <form className="company-form" onSubmit={handleSubmit}>
        <label>
          Company Name
          <input value={form.companyName || ""} onChange={(event) => updateField("companyName", event.target.value)} required />
        </label>
        <label>
          Owner
          <input value={form.owner || ""} onChange={(event) => updateField("owner", event.target.value)} />
        </label>
        <label>
          Address
          <input value={form.address || ""} onChange={(event) => updateField("address", event.target.value)} />
        </label>
        <label>
          Location
          <input value={form.location || ""} onChange={(event) => updateField("location", event.target.value)} />
        </label>
        <label>
          Phone
          <input value={form.phone || ""} onChange={(event) => updateField("phone", event.target.value)} />
        </label>
        <label className="check-row company-check">
          <input type="checkbox" checked={Boolean(form.barcode)} onChange={(event) => updateField("barcode", event.target.checked)} />
          Barcode
        </label>
        <label className="check-row company-check">
          <input type="checkbox" checked={Boolean(form.inactive)} onChange={(event) => updateField("inactive", event.target.checked)} />
          Inactive
        </label>
        <div className="form-section-title">Microsoft SSO Tenant</div>
        <label>
          Tenant ID
          <input
            value={form.microsoftTenantId || ""}
            onChange={(event) => updateField("microsoftTenantId", event.target.value)}
            placeholder="Customer Entra tenant GUID"
          />
        </label>
        <label>
          Tenant Name
          <input
            value={form.microsoftTenantName || ""}
            onChange={(event) => updateField("microsoftTenantName", event.target.value)}
            placeholder="Customer display name"
          />
        </label>
        <label className="check-row company-check">
          <input
            type="checkbox"
            checked={form.microsoftTenantEnabled !== false}
            onChange={(event) => updateField("microsoftTenantEnabled", event.target.checked)}
          />
          Enable Microsoft SSO
        </label>
        <div className="form-actions inline">
          <button className="primary-button" type="submit" disabled={saving}>{saving ? "Saving..." : "Save Company"}</button>
        </div>
      </form>

      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Company</th>
              <th>Owner</th>
              <th>Location</th>
              <th>Phone</th>
              <th>Microsoft Tenant</th>
              <th>Status</th>
              <th />
              <th />
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan="8">Loading companies...</td>
              </tr>
            ) : companies.length === 0 ? (
              <tr>
                <td colSpan="8">No companies found</td>
              </tr>
            ) : (
              companies.map((company) => (
                <tr key={company.companyId} onDoubleClick={() => editCompany(company)}>
                  <td>{company.companyName}</td>
                  <td>{company.owner || ""}</td>
                  <td>{company.location || company.address || ""}</td>
                  <td>{company.phone || ""}</td>
                  <td>{company.microsoftTenantId ? `${company.microsoftTenantName || "Microsoft"} (${company.microsoftTenantEnabled ? "Enabled" : "Disabled"})` : ""}</td>
                  <td>{company.inactive ? "Inactive" : "Active"}</td>
                  <td className="row-actions">
                    <button
                      className={company.companyId === companyId ? "secondary-button selected" : "secondary-button"}
                      type="button"
                      onClick={() => onCompanyChange(company.companyId)}
                    >
                      <CheckCircle2 size={16} />
                      {company.companyId === companyId ? "Selected" : "Select"}
                    </button>
                  </td>
                  <td className="row-actions">
                    <button className="icon-button danger" type="button" onClick={() => handleDelete(company)} aria-label="Delete company">
                      <Trash2 size={16} />
                    </button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </section>
  );
}
