import { useEffect, useState } from "react";
import { getSecuritySettings, saveSecuritySettings } from "../api/client";

const blankSettings = {
  microsoft: {
    clientId: "",
    clientSecret: "",
    callbackPath: "/api/auth/microsoft/callback",
    hasClientSecret: false,
  },
  smtp: {
    host: "",
    port: "587",
    enableSsl: "true",
    from: "no-reply@ashunya.com",
    username: "",
    password: "",
    hasPassword: false,
  },
  branding: {
    logoDataUrl: "",
  },
};

export function SettingsManager({ onNotice, onBrandingChange }) {
  const [settings, setSettings] = useState(blankSettings);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let ignore = false;
    setLoading(true);
    getSecuritySettings()
      .then((data) => {
        if (!ignore) {
          setSettings({
            microsoft: { ...blankSettings.microsoft, ...(data.microsoft || {}) },
            smtp: { ...blankSettings.smtp, ...(data.smtp || {}) },
            branding: { ...blankSettings.branding, ...(data.branding || {}) },
          });
        }
      })
      .catch((error) => !ignore && onNotice({ type: "error", text: error.message }))
      .finally(() => !ignore && setLoading(false));
    return () => {
      ignore = true;
    };
  }, [onNotice]);

  function update(section, field, value) {
    setSettings((current) => ({
      ...current,
      [section]: {
        ...current[section],
        [field]: value,
      },
    }));
  }

  async function handleSubmit(event) {
    event.preventDefault();
    setSaving(true);
    onNotice(null);
    try {
      const result = await saveSecuritySettings(settings);
      setSettings((current) => ({
        microsoft: { ...current.microsoft, clientSecret: "", hasClientSecret: Boolean(current.microsoft.clientSecret || current.microsoft.hasClientSecret) },
        smtp: { ...current.smtp, password: "", hasPassword: Boolean(current.smtp.password || current.smtp.hasPassword) },
        branding: { ...current.branding },
      }));
      onBrandingChange?.(settings.branding);
      onNotice({ type: "success", text: result.message || "Settings saved." });
    } catch (error) {
      onNotice({ type: "error", text: error.message });
    } finally {
      setSaving(false);
    }
  }

  function handleLogoFile(file) {
    if (!file) {
      return;
    }

    const reader = new FileReader();
    reader.onload = () => {
      update("branding", "logoDataUrl", String(reader.result || ""));
    };
    reader.readAsDataURL(file);
  }

  return (
    <section className="panel">
      <div className="panel-header compact">
        <div>
          <h2>Settings</h2>
          <p>Configure global sign-in and email delivery settings.</p>
        </div>
      </div>

      {loading ? (
        <div className="panel-empty">Loading settings...</div>
      ) : (
        <form className="settings-form" onSubmit={handleSubmit}>
          <fieldset>
            <legend>Branding</legend>
            <div className="settings-logo-preview">
              <img src={settings.branding.logoDataUrl || "/smartdocscan-logo.svg"} alt="SmartDocScan logo preview" />
            </div>
            <label>
              App Logo
              <input type="file" accept="image/png,image/jpeg,image/svg+xml,image/webp" onChange={(event) => handleLogoFile(event.target.files?.[0])} />
            </label>
            <p className="field-help">This logo is used on the sign-in page and main navigation.</p>
          </fieldset>

          <fieldset>
            <legend>Microsoft SSO</legend>
            <label>
              Client ID
              <input value={settings.microsoft.clientId || ""} onChange={(event) => update("microsoft", "clientId", event.target.value)} />
            </label>
            <label>
              Client Secret
              <input
                type="password"
                value={settings.microsoft.clientSecret || ""}
                onChange={(event) => update("microsoft", "clientSecret", event.target.value)}
                placeholder={settings.microsoft.hasClientSecret ? "Saved. Enter a new secret to replace." : ""}
              />
            </label>
            <label>
              Callback Path
              <input value={settings.microsoft.callbackPath || ""} onChange={(event) => update("microsoft", "callbackPath", event.target.value)} />
            </label>
            <p className="field-help">Use a multitenant Entra app registration. Customer tenant IDs belong on each company record, not in this global SSO setup. Redirect URI should be https://your-domain/api/auth/microsoft/callback.</p>
          </fieldset>

          <fieldset>
            <legend>SMTP Email</legend>
            <label>
              Host
              <input value={settings.smtp.host || ""} onChange={(event) => update("smtp", "host", event.target.value)} placeholder="smtp.office365.com" />
            </label>
            <label>
              Port
              <input value={settings.smtp.port || ""} onChange={(event) => update("smtp", "port", event.target.value)} />
            </label>
            <label>
              Enable SSL
              <select value={settings.smtp.enableSsl || "true"} onChange={(event) => update("smtp", "enableSsl", event.target.value)}>
                <option value="true">True</option>
                <option value="false">False</option>
              </select>
            </label>
            <label>
              From Address
              <input value={settings.smtp.from || ""} onChange={(event) => update("smtp", "from", event.target.value)} />
            </label>
            <label>
              Username
              <input value={settings.smtp.username || ""} onChange={(event) => update("smtp", "username", event.target.value)} />
            </label>
            <label>
              Password
              <input
                type="password"
                value={settings.smtp.password || ""}
                onChange={(event) => update("smtp", "password", event.target.value)}
                placeholder={settings.smtp.hasPassword ? "Saved. Enter a new password to replace." : ""}
              />
            </label>
          </fieldset>

          <div className="form-actions">
            <button className="primary-button" type="submit" disabled={saving}>
              {saving ? "Saving..." : "Save Settings"}
            </button>
          </div>
        </form>
      )}
    </section>
  );
}
