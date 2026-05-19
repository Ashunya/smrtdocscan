import { useEffect, useState } from "react";
import { Save, X } from "lucide-react";

const emptyPatient = {
  companyId: 7,
  externalPatientId: "",
  firstName: "",
  lastName: "",
  dateOfBirth: "",
  gender: "",
  physician: "",
  box: "",
  ssn: "",
};

export function PatientForm({ companyId, patient, onSave, onCancel, saving }) {
  const [form, setForm] = useState(emptyPatient);

  useEffect(() => {
    setForm({
      companyId,
      externalPatientId: patient?.externalPatientId || "",
      firstName: patient?.firstName || "",
      lastName: patient?.lastName || "",
      dateOfBirth: patient?.dateOfBirth ? patient.dateOfBirth.slice(0, 10) : "",
      gender: patient?.gender || "",
      physician: patient?.physician || "",
      box: patient?.box || "",
      ssn: patient?.ssn || "",
    });
  }, [companyId, patient]);

  function updateField(field, value) {
    setForm((current) => ({ ...current, [field]: value }));
  }

  function submit(event) {
    event.preventDefault();
    onSave({
      ...form,
      companyId,
      dateOfBirth: form.dateOfBirth || null,
    });
  }

  return (
    <form className="panel patient-form" onSubmit={submit}>
      <div className="panel-header">
        <div>
          <h2>{patient ? "Edit Patient" : "Add Patient"}</h2>
          <p>Uses the existing SQL patient table.</p>
        </div>
        {patient && (
          <button className="icon-button" type="button" onClick={onCancel} aria-label="Cancel edit">
            <X size={18} />
          </button>
        )}
      </div>

      <div className="form-grid">
        <label>
          Last Name
          <input value={form.lastName} onChange={(e) => updateField("lastName", e.target.value)} required />
        </label>
        <label>
          First Name
          <input value={form.firstName} onChange={(e) => updateField("firstName", e.target.value)} required />
        </label>
        <label>
          Patient ID
          <input value={form.externalPatientId} onChange={(e) => updateField("externalPatientId", e.target.value)} />
        </label>
        <label>
          Date of Birth
          <input type="date" value={form.dateOfBirth} onChange={(e) => updateField("dateOfBirth", e.target.value)} />
        </label>
        <label>
          Gender
          <select value={form.gender} onChange={(e) => updateField("gender", e.target.value)}>
            <option value="">Select</option>
            <option value="M">Male</option>
            <option value="F">Female</option>
          </select>
        </label>
        <label>
          Physician
          <input value={form.physician} onChange={(e) => updateField("physician", e.target.value)} />
        </label>
        <label>
          Box
          <input value={form.box} onChange={(e) => updateField("box", e.target.value)} />
        </label>
        <label>
          SSN
          <input value={form.ssn} onChange={(e) => updateField("ssn", e.target.value)} />
        </label>
      </div>

      <div className="form-actions">
        <button className="primary-button" type="submit" disabled={saving}>
          <Save size={17} />
          {saving ? "Saving" : "Save Patient"}
        </button>
      </div>
    </form>
  );
}
