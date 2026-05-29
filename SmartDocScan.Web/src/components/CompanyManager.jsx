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
  tenants: [],
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
    let tenants = company.tenants || [];
    if (tenants.length === 0 && company.microsoftTenantId) {
      tenants = [
        {
          tenantId: company.microsoftTenantId,
          tenantName: company.microsoftTenantName || "",
          enabled: company.microsoftTenantEnabled !== false,
        }
      ];
    }
    setForm({
      ...company,
      tenants,
    });
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
        <div className="form-section-title">Microsoft SSO Tenants</div>
        <div className="tenants-list-container" style={{ marginBottom: "15px" }}>
          {(form.tenants || []).map((tenant, index) => (
            <div key={index} className="tenant-row" style={{ display: "flex", gap: "10px", alignItems: "center", marginBottom: "10px" }}>
              <input
                style={{ flex: 3 }}
                value={tenant.tenantId || ""}
                onChange={(event) => {
                  const updated = [...(form.tenants || [])];
                  updated[index] = { ...updated[index], tenantId: event.target.value };
                  updateField("tenants", updated);
                }}
                placeholder="Entra tenant GUID"
                required
              />
              <input
                style={{ flex: 3 }}
                value={tenant.tenantName || ""}
                onChange={(event) => {
                  const updated = [...(form.tenants || [])];
                  updated[index] = { ...updated[index], tenantName: event.target.value };
                  updateField("tenants", updated);
                }}
                placeholder="Tenant display name"
              />
              <label style={{ display: "flex", alignItems: "center", gap: "6px", whiteSpace: "nowrap", margin: 0, fontSize: "0.95em", userSelect: "none" }}>
                <input
                  type="checkbox"
                  style={{ width: "auto", margin: 0 }}
                  checked={tenant.enabled !== false}
                  onChange={(event) => {
                    const updated = [...(form.tenants || [])];
                    updated[index] = { ...updated[index], enabled: event.target.checked };
                    updateField("tenants", updated);
                  }}
                />
                Enabled
              </label>
              <button
                type="button"
                className="icon-button danger"
                style={{ padding: "8px", minWidth: "auto", height: "auto" }}
                onClick={() => {
                  const updated = (form.tenants || []).filter((_, i) => i !== index);
                  updateField("tenants", updated);
                }}
                title="Remove Tenant"
              >
                <Trash2 size={16} />
              </button>
            </div>
          ))}
          <button
            type="button"
            className="secondary-button"
            style={{ padding: "6px 12px", fontSize: "0.9em" }}
            onClick={() => {
              const updated = [...(form.tenants || []), { tenantId: "", tenantName: "", enabled: true }];
              updateField("tenants", updated);
            }}
          >
            Add Microsoft Tenant ID
          </button>
        </div>
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
                  <td>
                    {company.tenants && company.tenants.length > 0 ? (
                      <div style={{ display: "flex", flexDirection: "column", gap: "4px" }}>
                        {company.tenants.map((t, idx) => (
                          <div key={idx} style={{ fontSize: "0.85em", opacity: t.enabled ? 1 : 0.6, lineHeight: "1.2" }}>
                            • {t.tenantName || "Microsoft"} ({t.tenantId.substring(0, 8)}...) {t.enabled ? "" : "(Disabled)"}
                          </div>
                        ))}
                      </div>
                    ) : company.microsoftTenantId ? (
                      `${company.microsoftTenantName || "Microsoft"} (${company.microsoftTenantEnabled ? "Enabled" : "Disabled"})`
                    ) : (
                      ""
                    )}
                  </td>
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
