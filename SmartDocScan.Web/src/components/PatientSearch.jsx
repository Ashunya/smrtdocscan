import { Edit2, FileText, Search, Trash2 } from "lucide-react";

export function PatientSearch({ patients, search, onSearchChange, loading, onEdit, onDocuments, onDelete, user }) {
  const canDelete = Boolean(user?.deleteDocument || user?.isAdmin || user?.superUser);
  const canEdit = Boolean(user?.addPatients || user?.isAdmin || user?.superUser);

  return (
    <section className="panel patient-search">
      <div className="panel-header">
        <div>
          <h2>Find Patient</h2>
          <p>Search by patient ID, name, or internal record ID.</p>
        </div>
        <label className="search-box">
          <Search size={18} />
          <input
            value={search}
            onChange={(e) => onSearchChange(e.target.value)}
            placeholder="Search patients"
            aria-label="Search patients"
          />
        </label>
      </div>

      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Last Name</th>
              <th>First Name</th>
              <th>Date of Birth</th>
              <th>Patient ID</th>
              <th>Last Upload</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan="6">Loading...</td>
              </tr>
            ) : patients.length === 0 ? (
              <tr>
                <td colSpan="6">No matching records found</td>
              </tr>
            ) : (
              patients.map((patient) => (
                <tr className="clickable-row" key={patient.patientId} onClick={() => onDocuments(patient)}>
                  <td>{patient.lastName}</td>
                  <td>{patient.firstName}</td>
                  <td>{formatDate(patient.dateOfBirth)}</td>
                  <td>{patient.externalPatientId}</td>
                  <td>{formatDateTime(patient.lastDocumentDate)}</td>
                  <td className="row-actions">
                    <button className="icon-button" type="button" onClick={(event) => { event.stopPropagation(); onDocuments(patient); }} aria-label="View documents">
                      <FileText size={16} />
                    </button>
                    {canEdit && (
                      <button className="icon-button" type="button" onClick={(event) => { event.stopPropagation(); onEdit(patient); }} aria-label="Edit patient">
                        <Edit2 size={16} />
                      </button>
                    )}
                    {canDelete && (
                      <button className="icon-button danger" type="button" onClick={(event) => { event.stopPropagation(); onDelete(patient); }} aria-label="Delete patient">
                        <Trash2 size={16} />
                      </button>
                    )}
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

function formatDateTime(value) {
  if (!value) {
    return "";
  }
  return new Date(value).toLocaleString();
}

function formatDate(value) {
  if (!value) {
    return "";
  }
  return new Date(value).toLocaleDateString();
}
