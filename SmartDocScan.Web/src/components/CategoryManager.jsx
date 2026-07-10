import { Plus, Trash2 } from "lucide-react";
import { useEffect, useState } from "react";
import { createCategory, deleteCategory, listCategories } from "../api/client";

export function CategoryManager({ companyId, onNotice }) {
  const [categories, setCategories] = useState([]);
  const [categoryName, setCategoryName] = useState("");
  const [parentId, setParentId] = useState("");
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
      const category = await createCategory({
        companyId,
        categoryName,
        parentId: parentId ? Number(parentId) : null,
      });
      setCategories((current) => [...current, category]);
      setCategoryName("");
      setParentId("");
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

  const orderedCategories = [];
  categories.filter((c) => !c.parentId).forEach((parent) => {
    orderedCategories.push(parent);
    categories.filter((c) => c.parentId === parent.categoryId).forEach((child) => {
      orderedCategories.push(child);
    });
  });
  categories.forEach((c) => {
    if (c.parentId && !orderedCategories.find((item) => item.categoryId === c.categoryId)) {
      orderedCategories.push(c);
    }
  });

  return (
    <section className="panel">
      <div className="panel-header compact">
        <div>
          <h2>Categories</h2>
          <p>Manage document categories and subcategories for company {companyId}.</p>
        </div>
      </div>
      <form className="category-form" onSubmit={handleSubmit} style={{ gap: "15px", alignItems: "flex-end" }}>
        <label style={{ flex: 2 }}>
          Category Name
          <input value={categoryName} onChange={(event) => setCategoryName(event.target.value)} required />
        </label>
        <label style={{ flex: 2 }}>
          Parent Category (Optional)
          <select value={parentId} onChange={(event) => setParentId(event.target.value)}>
            <option value="">None (Top-Level Category)</option>
            {categories
              .filter((c) => !c.parentId)
              .map((c) => (
                <option key={c.categoryId} value={c.categoryId}>
                  {c.categoryName}
                </option>
              ))}
          </select>
        </label>
        <button className="primary-button" type="submit" disabled={saving} style={{ height: "42px" }}>
          <Plus size={18} />
          {saving ? "Saving..." : "Add Category"}
        </button>
      </form>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Category</th>
              <th>Type</th>
              <th>Access</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr><td colSpan="4">Loading categories...</td></tr>
            ) : categories.length === 0 ? (
              <tr><td colSpan="4">No categories found</td></tr>
            ) : orderedCategories.map((category) => (
              <tr key={category.categoryId}>
                <td style={{ paddingLeft: category.parentId ? "30px" : "12px", fontWeight: category.parentId ? "normal" : "600" }}>
                  {category.parentId ? `↳ ${category.categoryName}` : category.categoryName}
                </td>
                <td style={{ fontSize: "0.9em", color: "#666" }}>
                  {category.parentId ? `Subcategory of ${category.parentName || "Parent"}` : "Top-Level Category"}
                </td>
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
