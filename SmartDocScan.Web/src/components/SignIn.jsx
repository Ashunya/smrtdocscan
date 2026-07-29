import { Lock, LogIn, User } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { getMicrosoftSignInUrl, login } from "../api/client";

export function SignIn({ onSignedIn, logoUrl }) {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [passwordSettling, setPasswordSettling] = useState(false);
  const usernameInputRef = useRef(null);
  const passwordInputRef = useRef(null);
  const passwordSettleUntilRef = useRef(0);
  const passwordSettleTimerRef = useRef(null);

  useEffect(() => {
    return () => {
      if (passwordSettleTimerRef.current) {
        window.clearTimeout(passwordSettleTimerRef.current);
      }
    };
  }, []);

  async function handleSubmit(event) {
    event.preventDefault();
    setLoading(true);
    setError("");
    try {
      await waitForPasswordPasteToSettle(passwordSettleUntilRef);
      const result = await login({
        username: usernameInputRef.current?.value ?? username,
        password: passwordInputRef.current?.value ?? password,
      });
      onSignedIn(result.user);
    } catch {
      setError("Invalid username or password.");
    } finally {
      setLoading(false);
    }
  }

  function handlePasswordInput(event) {
    setPassword(event.target.value);
    markPasswordAsSettling();
  }

  function markPasswordAsSettling() {
    passwordSettleUntilRef.current = Date.now() + 350;
    setPasswordSettling(true);

    if (passwordSettleTimerRef.current) {
      window.clearTimeout(passwordSettleTimerRef.current);
    }

    passwordSettleTimerRef.current = window.setTimeout(() => {
      if (Date.now() >= passwordSettleUntilRef.current) {
        setPasswordSettling(false);
      }
    }, 375);
  }

  return (
    <main className="signin-page">
      <section className="signin-card">
        <div className="signin-story">
          <img className="signin-logo" src={logoUrl || "/smartdocscan-logo.svg"} alt="SmartDocScan" />
          <p className="signin-eyebrow">Secure Document Workflow</p>
          <h1>SmartDocScan</h1>
          <p className="signin-copy">Scan, upload, and retrieve patient documents from one secure workspace built for day-to-day operations.</p>
          <ul className="signin-points">
            <li><span>▦</span>Centralized document capture</li>
            <li><span>▣</span>Protected patient record access</li>
            <li><span>⌕</span>Faster search and retrieval</li>
          </ul>
        </div>
        <div className="signin-panel">
          <h2>Sign In</h2>
          <p>Use the username and password assigned by your administrator.</p>
          {error && <div className="notice error signin-error">{error}</div>}
          <form onSubmit={handleSubmit}>
            <label className="signin-input">
              <User size={18} />
              <input ref={usernameInputRef} name="username" value={username} onChange={(event) => setUsername(event.target.value)} placeholder="User Name" autoComplete="username" required />
            </label>
            <label className="signin-input">
              <Lock size={18} />
              <input ref={passwordInputRef} name="password" type="password" value={password} onChange={handlePasswordInput} onPaste={markPasswordAsSettling} placeholder="Password" autoComplete="current-password" required />
            </label>
            <label className="signin-remember">
              <input type="checkbox" />
              Remember me
            </label>
            <button className="primary-button signin-button" type="submit" disabled={loading || passwordSettling}>
              <LogIn size={18} />
              {loading ? "Signing in..." : passwordSettling ? "Reading password..." : "Login"}
            </button>
          </form>
          <a className="signin-sso-button" href={getMicrosoftSignInUrl("/")}>
            <MicrosoftLogo />
            Sign in with Microsoft
          </a>
          <p className="signin-help">Need help signing in? Contact your company administrator.</p>
          <p className="signin-copyright">Copyright © 2026 <span>Ashunya</span>. All Rights Reserved.</p>
        </div>
      </section>
    </main>
  );
}

async function waitForPasswordPasteToSettle(passwordSettleUntilRef) {
  while (Date.now() < passwordSettleUntilRef.current) {
    await new Promise((resolve) => window.setTimeout(resolve, Math.min(passwordSettleUntilRef.current - Date.now(), 75)));
  }
}

function MicrosoftLogo() {
  return (
    <span className="microsoft-logo" aria-hidden="true">
      <span />
      <span />
      <span />
      <span />
    </span>
  );
}
