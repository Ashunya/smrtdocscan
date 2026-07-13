import { Plus, Trash2, Edit2, X, Check } from "lucide-react";
import { useEffect, useState } from "react";
import { listLocations, saveLocation, deleteLocation } from "../api/client";

export function LocationManager({ companyId, onNotice }) {
  const [locations, setLocations] = useState([]);
  const [locationName, setLocationName] = useState("");
  const [locationCode, setLocationCode] = useState("");
  const [address, setAddress] = useState("");
  const [phone, setPhone] = useState("");
  const [inactive, setInactive] = useState(false);
  const [editingId, setEditingId] = useState(null);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let ignore = false;
    setLoading(true);
    listLocations({ companyId })
      .then((data) => !ignore && setLocations(data))
      .catch((error) => !ignore && onNotice({ type: "error", text: error.message }))
      .finally(() => !ignore && setLoading(false));
    return () => {
      ignore = true;
    };
  }, [companyId, onNotice]);

  async function handleSubmit(event) {
    event.preventDefault();
    setSaving(true);
    onNotice(null);
    try {
      const location = await saveLocation({
        locationId: editingId,
        companyId,
        locationName,
        locationCode,
        address,
        phone,
        inactive,
      });

      if (editingId) {
        setLocations((current) => current.map((item) => (item.locationId === editingId ? location : item)));
        onNotice({ type: "success", text: "Location updated." });
      } else {
        setLocations((current) => [...current, location]);
        onNotice({ type: "success", text: "Location added." });
      }

      resetForm();
    } catch (error) {
      onNotice({ type: "error", text: error.message });
    } finally {
      setSaving(false);
    }
  }

  function startEdit(location) {
    setEditingId(location.locationId);
    setLocationName(location.locationName);
    setLocationCode(location.locationCode || "");
    setAddress(location.address || "");
    setPhone(location.phone || "");
    setInactive(location.inactive);
  }

  function resetForm() {
    setEditingId(null);
    setLocationName("");
    setLocationCode("");
    setAddress("");
    setPhone("");
    setInactive(false);
  }

  async function handleDelete(location) {
    const ok = window.confirm(`Delete location ${location.locationName}?`);
    if (!ok) return;
    onNotice(null);
    try {
      await deleteLocation(location.locationId, companyId);
      setLocations((current) => current.filter((item) => item.locationId !== location.locationId));
      onNotice({ type: "success", text: "Location deleted." });
    } catch (error) {
      onNotice({ type: "error", text: error.message });
    }
  }

  return (
    <section className="panel">
      <div className="panel-header compact">
        <div>
          <h2>Dialysis Centers / Locations</h2>
          <p>Manage operating centers for company {companyId} to support segregated folders.</p>
        </div>
      </div>
      <form className="location-form" onSubmit={handleSubmit} style={{ gap: "15px", display: "flex", flexDirection: "column" }}>
        <div style={{ display: "flex", gap: "15px", flexWrap: "wrap" }}>
          <label style={{ flex: "1 1 200px" }}>
            Location Name
            <input value={locationName} onChange={(event) => setLocationName(event.target.value)} required />
          </label>
          <label style={{ flex: "1 1 120px" }}>
            Location Code / ID
            <input value={locationCode} onChange={(event) => setLocationCode(event.target.value)} placeholder="e.g. CTR-01" />
          </label>
          <label style={{ flex: "1 1 150px" }}>
            Phone Number
            <input value={phone} onChange={(event) => setPhone(event.target.value)} placeholder="e.g. 555-0199" />
          </label>
        </div>
        <div style={{ display: "flex", gap: "15px", flexWrap: "wrap" }}>
          <label style={{ flex: "2 1 300px" }}>
            Address
            <input value={address} onChange={(event) => setAddress(event.target.value)} placeholder="e.g. 100 Main St, City, ST" />
          </label>
          <label style={{ flex: "1 1 100px", display: "flex", flexDirection: "row", alignItems: "center", gap: "10px", marginTop: "24px" }}>
            <input type="checkbox" checked={inactive} onChange={(event) => setInactive(event.target.checked)} />
            Inactive
          </label>
        </div>
        <div style={{ display: "flex", gap: "10px", justifyContent: "flex-end" }}>
          {editingId && (
            <button className="secondary-button" type="button" onClick={resetForm}>
              <X size={16} /> Cancel
            </button>
          )}
          <button className="primary-button" type="submit" disabled={saving}>
            {editingId ? <Check size={18} /> : <Plus size={18} />}
            {saving ? "Saving..." : editingId ? "Save Changes" : "Add Location"}
          </button>
        </div>
      </form>

      <div className="table-wrap" style={{ marginTop: "20px" }}>
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Code</th>
              <th>Address</th>
              <th>Phone</th>
              <th>Status</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr><td colSpan="6">Loading locations...</td></tr>
            ) : locations.length === 0 ? (
              <tr><td colSpan="6">No locations configured yet.</td></tr>
            ) : locations.map((location) => (
              <tr key={location.locationId} style={{ opacity: location.inactive ? 0.6 : 1 }}>
                <td style={{ fontWeight: 600 }}>{location.locationName}</td>
                <td>{location.locationCode || ""}</td>
                <td>{location.address || ""}</td>
                <td>{location.phone || ""}</td>
                <td>
                  <span style={{
                    padding: "3px 8px",
                    borderRadius: "4px",
                    fontSize: "0.85em",
                    fontWeight: "600",
                    background: location.inactive ? "#f8d7da" : "#d1e7dd",
                    color: location.inactive ? "#842029" : "#0f5132"
                  }}>
                    {location.inactive ? "Inactive" : "Active"}
                  </span>
                </td>
                <td className="row-actions">
                  <button className="icon-button" type="button" onClick={() => startEdit(location)} aria-label="Edit location">
                    <Edit2 size={16} />
                  </button>
                  <button className="icon-button danger" type="button" onClick={() => handleDelete(location)} aria-label="Delete location">
                    <Trash2 size={16} />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
