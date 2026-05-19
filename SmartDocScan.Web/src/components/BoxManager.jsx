import { PackagePlus, Trash2 } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { createBox, deleteBox, listBoxes } from "../api/client";

const initialForm = {
  externalBoxId: "",
  boxName: "",
  aisle: "",
  section: "",
  row: "",
  column: "",
};

export function BoxManager({ companyId, onNotice }) {
  const [boxes, setBoxes] = useState([]);
  const [form, setForm] = useState(initialForm);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let ignore = false;
    setLoading(true);
    listBoxes({ companyId })
      .then((data) => {
        if (!ignore) {
          setBoxes(data);
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
  }, [companyId, onNotice]);

  const canSave = useMemo(() => Number(form.externalBoxId) > 0, [form.externalBoxId]);

  function updateField(field, value) {
    setForm((current) => ({ ...current, [field]: value }));
  }

  async function handleSubmit(event) {
    event.preventDefault();
    setSaving(true);
    onNotice(null);
    try {
      const box = await createBox({
        companyId,
        externalBoxId: Number(form.externalBoxId),
        boxName: form.boxName,
        aisle: form.aisle,
        section: form.section,
        row: form.row,
        column: form.column,
      });
      setBoxes((current) => [...current, box]);
      setForm(initialForm);
      onNotice({ type: "success", text: "Box added." });
    } catch (error) {
      onNotice({ type: "error", text: error.message });
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(box) {
    const ok = window.confirm(`Delete box ${box.externalBoxId}?`);
    if (!ok) return;
    onNotice(null);
    try {
      await deleteBox(box.boxId);
      setBoxes((current) => current.filter((item) => item.boxId !== box.boxId));
      onNotice({ type: "success", text: "Box deleted." });
    } catch (error) {
      onNotice({ type: "error", text: error.message });
    }
  }

  return (
    <section className="panel">
      <div className="panel-header compact">
        <div>
          <h2>Add Box</h2>
          <p>Create storage boxes and locations for the selected company.</p>
        </div>
      </div>

      <form className="box-form" onSubmit={handleSubmit}>
        <label>
          Box ID
          <input
            type="number"
            min="1"
            value={form.externalBoxId}
            onChange={(event) => updateField("externalBoxId", event.target.value)}
            required
          />
        </label>
        <label>
          Box Name
          <input value={form.boxName} onChange={(event) => updateField("boxName", event.target.value)} />
        </label>
        <label>
          Aisle
          <input value={form.aisle} onChange={(event) => updateField("aisle", event.target.value)} />
        </label>
        <label>
          Section
          <input value={form.section} onChange={(event) => updateField("section", event.target.value)} />
        </label>
        <label>
          Row
          <input value={form.row} onChange={(event) => updateField("row", event.target.value)} />
        </label>
        <label>
          Column
          <input value={form.column} onChange={(event) => updateField("column", event.target.value)} />
        </label>
        <div className="form-actions inline">
          <button className="primary-button" type="submit" disabled={!canSave || saving}>
            <PackagePlus size={18} />
            {saving ? "Saving..." : "Add Box"}
          </button>
        </div>
      </form>

      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Box ID</th>
              <th>Box Name</th>
              <th>Aisle</th>
              <th>Section</th>
              <th>Row</th>
              <th>Column</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan="7">Loading boxes...</td>
              </tr>
            ) : boxes.length === 0 ? (
              <tr>
                <td colSpan="7">No boxes found</td>
              </tr>
            ) : (
              boxes.map((box) => (
                <tr key={box.boxId}>
                  <td>{box.externalBoxId}</td>
                  <td>{box.boxName || ""}</td>
                  <td>{box.aisle || ""}</td>
                  <td>{box.section || ""}</td>
                  <td>{box.row || ""}</td>
                  <td>{box.column || ""}</td>
                  <td className="row-actions">
                    <button className="icon-button danger" type="button" onClick={() => handleDelete(box)} aria-label="Delete box">
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
