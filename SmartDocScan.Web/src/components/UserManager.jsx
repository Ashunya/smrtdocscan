import { Save, Trash2 } from "lucide-react";
import { useEffect, useState } from "react";
import { deleteUser, listUsers, saveUser } from "../api/client";

const permissionFields = [
  ["uploadDocument", "Upload"],
  ["scanDocument", "Scan"],
  ["deleteDocument", "Delete"],
  ["printDocument", "Print"],
  ["downloadDocument", "Download"],
  ["addCategory", "Categories"],
  ["addUsers", "Users"],
  ["addPatients", "Patients"],
  ["box", "Boxes"],
  ["report", "Reports"],
  ["superUser", "Super User"],
  ["isAdmin", "Admin"],
  ["disabled", "Disabled"],
];

const blankUser = {
  username: "",
  name: "",
  password: "",
  confirmPassword: "",
};

export function UserManager({ companyId, onNotice }) {
  const [users, setUsers] = useState([]);
  const [form, setForm] = useState(blankUser);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let ignore = false;
    setLoading(true);
    listUsers({ companyId })
      .then((data) => {
        if (!ignore) {
          setUsers(data);
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

  function editUser(user) {
    setForm({ ...user, password: "", confirmPassword: "" });
  }

  function updateField(field, value) {
    setForm((current) => ({ ...current, [field]: value }));
  }

  async function handleSubmit(event) {
    event.preventDefault();
    const editingExisting = users.some((user) => user.username === form.username);
    if (!editingExisting && !form.password) {
      onNotice({ type: "error", text: "Password is required for new users." });
      return;
    }
    if (form.password || form.confirmPassword) {
      if (form.password !== form.confirmPassword) {
        onNotice({ type: "error", text: "Passwords do not match." });
        return;
      }
    }

    setSaving(true);
    onNotice(null);
    try {
      const payload = { ...form, companyId };
      if (editingExisting && !payload.password) {
        delete payload.password;
        delete payload.confirmPassword;
      }
      const saved = await saveUser(payload);
      setUsers((current) => [saved, ...current.filter((user) => user.username !== saved.username)]);
      setForm(blankUser);
      onNotice({ type: "success", text: "User saved." });
    } catch (error) {
      onNotice({ type: "error", text: error.message });
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(user) {
    const ok = window.confirm(`Delete user ${user.username}?`);
    if (!ok) return;
    onNotice(null);
    try {
      await deleteUser(user.username);
      setUsers((current) => current.filter((item) => item.username !== user.username));
      onNotice({ type: "success", text: "User deleted." });
    } catch (error) {
      onNotice({ type: "error", text: error.message });
    }
  }

  return (
    <section className="panel">
      <div className="panel-header compact">
        <div>
          <h2>Users</h2>
          <p>Manage users and permissions for company {companyId}.</p>
        </div>
      </div>

      <form className="user-form" onSubmit={handleSubmit}>
        <label>
          Username
          <input value={form.username || ""} onChange={(event) => updateField("username", event.target.value)} required />
        </label>
        <label>
          Name
          <input value={form.name || ""} onChange={(event) => updateField("name", event.target.value)} required />
        </label>
        <label>
          Password
          <input
            type="password"
            value={form.password || ""}
            onChange={(event) => updateField("password", event.target.value)}
            placeholder={users.some((user) => user.username === form.username) ? "Leave blank to keep current password" : ""}
            autoComplete="new-password"
          />
        </label>
        <label>
          Confirm Password
          <input
            type="password"
            value={form.confirmPassword || ""}
            onChange={(event) => updateField("confirmPassword", event.target.value)}
            autoComplete="new-password"
          />
        </label>

        <div className="permission-grid">
          {permissionFields.map(([field, label]) => (
            <label className="check-row" key={field}>
              <input
                type="checkbox"
                checked={Boolean(form[field])}
                onChange={(event) => updateField(field, event.target.checked)}
              />
              {label}
            </label>
          ))}
        </div>

        <div className="form-actions inline">
          <button className="primary-button" type="submit" disabled={saving}>
            <Save size={18} />
            {saving ? "Saving..." : "Save User"}
          </button>
        </div>
      </form>

      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Username</th>
              <th>Name</th>
              <th>Permissions</th>
              <th>Status</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan="5">Loading users...</td>
              </tr>
            ) : users.length === 0 ? (
              <tr>
                <td colSpan="5">No users found</td>
              </tr>
            ) : (
              users.map((user) => (
                <tr key={user.username} onClick={() => editUser(user)} className="clickable-row">
                  <td>{user.username}</td>
                  <td>{user.name}</td>
                  <td>{summarizePermissions(user)}</td>
                  <td>{user.disabled ? "Disabled" : "Active"}</td>
                  <td className="row-actions">
                    <button className="icon-button danger" type="button" onClick={(event) => { event.stopPropagation(); handleDelete(user); }} aria-label="Delete user">
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

function summarizePermissions(user) {
  const enabled = permissionFields
    .filter(([field]) => field !== "disabled" && user[field])
    .map(([, label]) => label);
  return enabled.length ? enabled.join(", ") : "None";
}
