import { Plus, Trash2 } from "lucide-react";
import { useEffect, useState } from "react";
import { createCategory, deleteCategory, listCategories } from "../api/client";

export function CategoryManager({ companyId, onNotice }) {
  const [categories, setCategories] = useState([]);
  const [categoryName, setCategoryName] = useState("");
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let ignore = false;
    setLoading(true);
    listCategories({ companyId })
      .then((data) => !ignore && setCategories(data))
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
      const category = await createCategory({ companyId, categoryName });
      setCategories((current) => [...current, category]);
      setCategoryName("");
      onNotice({ type: "success", text: "Category added." });
    } catch (error) {
      onNotice({ type: "error", text: error.message });
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(category) {
    const ok = window.confirm(`Delete category ${category.categoryName}?`);
    if (!ok) return;
    onNotice(null);
    try {
      await deleteCategory(category.categoryId);
      setCategories((current) => current.filter((item) => item.categoryId !== category.categoryId));
      onNotice({ type: "success", text: "Category deleted." });
    } catch (error) {
      onNotice({ type: "error", text: error.message });
    }
  }

  return (
    <section className="panel">
      <div className="panel-header compact">
        <div>
          <h2>Categories</h2>
          <p>Manage document categories for company {companyId}.</p>
        </div>
      </div>
      <form className="category-form" onSubmit={handleSubmit}>
        <label>
          Category Name
          <input value={categoryName} onChange={(event) => setCategoryName(event.target.value)} required />
        </label>
        <button className="primary-button" type="submit" disabled={saving}>
          <Plus size={18} />
          {saving ? "Saving..." : "Add Category"}
        </button>
      </form>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Category</th>
              <th>Access</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr><td colSpan="3">Loading categories...</td></tr>
            ) : categories.length === 0 ? (
              <tr><td colSpan="3">No categories found</td></tr>
            ) : categories.map((category) => (
              <tr key={category.categoryId}>
                <td>{category.categoryName}</td>
                <td>{category.access || ""}</td>
                <td className="row-actions">
                  <button className="icon-button danger" type="button" onClick={() => handleDelete(category)} aria-label="Delete category">
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
