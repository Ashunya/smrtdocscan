import { useEffect, useMemo, useRef, useState } from "react";
import { Building2, Key, LogOut, Menu as MenuIcon, Moon, Sun } from "lucide-react";
import { AppBar, Avatar, Box, CssBaseline, Dialog, DialogActions, DialogContent, DialogTitle, Divider, IconButton, Menu as MuiMenu, MenuItem, Stack, ThemeProvider, Toolbar, Typography, createTheme } from "@mui/material";
import { changePassword, createPatient, deletePatient, getBrandingSettings, getCurrentUser, listCompanies, logout, searchPatients, updatePatient } from "./api/client";
import { BoxManager } from "./components/BoxManager";
import { BarcodeManager } from "./components/BarcodeManager";
import { CategoryManager } from "./components/CategoryManager";
import { CompanyManager } from "./components/CompanyManager";
import { DocumentManager } from "./components/DocumentManager";
import { PatientForm } from "./components/PatientForm";
import { PatientSearch } from "./components/PatientSearch";
import { Sidebar } from "./components/Sidebar";
import { SignIn } from "./components/SignIn";
import { UserManager } from "./components/UserManager";
import { ReportManager } from "./components/ReportManager";
import { ScannerManager } from "./components/ScannerManager";
import { SettingsManager } from "./components/SettingsManager";

const DEFAULT_COMPANY_ID = Number(import.meta.env.VITE_DEFAULT_COMPANY_ID || 7);

function createSmartDocTheme(mode) {
  const isDark = mode === "dark";
  return createTheme({
    palette: {
      mode,
      primary: { main: "#c1692a", dark: "#a05522", light: "#d4884f", contrastText: "#ffffff" },
      secondary: { main: isDark ? "#d4884f" : "#a05522" },
      background: {
        default: isDark ? "#0b1525" : "#f2ede8",
        paper: isDark ? "#0f1e38" : "#ffffff",
      },
    },
    shape: { borderRadius: 6 },
    typography: {
      fontFamily: 'Inter, "Segoe UI", Arial, sans-serif',
      h1: { fontWeight: 700 },
      h2: { fontWeight: 700 },
      h3: { fontWeight: 700 },
      button: { fontWeight: 700, textTransform: "none" },
    },
    components: {
      MuiButton: { styleOverrides: { root: { borderRadius: 6 } } },
      MuiPaper: { styleOverrides: { root: { backgroundImage: "none" } } },
      MuiAppBar: {
        styleOverrides: {
          root: { backgroundImage: "none", backdropFilter: "blur(16px)" },
        },
      },
    },
  });
}

export default function App() {
  const [activeView, setActiveView] = useState("find");
  const [companyId, setCompanyId] = useState(DEFAULT_COMPANY_ID);
  const [search, setSearch] = useState("");
  const [patients, setPatients] = useState([]);
  const [selectedPatient, setSelectedPatient] = useState(null);
  const [documentPatient, setDocumentPatient] = useState(null);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState(null);
  const [companies, setCompanies] = useState([]);
  const [colorMode, setColorMode] = useState(() => window.localStorage.getItem("smartdocscan-theme") || "light");
  const [logoUrl, setLogoUrl] = useState("/smartdocscan-logo.svg");
  const [userMenuAnchor, setUserMenuAnchor] = useState(null);
  const [cpOpen, setCpOpen] = useState(false);
  const [cpForm, setCpForm] = useState({ current: "", next: "", confirm: "" });
  const [cpError, setCpError] = useState("");
  const [cpSaving, setCpSaving] = useState(false);
  const [currentUser, setCurrentUser] = useState(null);
  const [authLoading, setAuthLoading] = useState(true);
  const [sidebarOpen, setSidebarOpen] = useState(true);
  const isHistoryPopRef = useRef(false);

  const debouncedSearch = useDebouncedValue(search, 300);
  const muiTheme = useMemo(() => createSmartDocTheme(colorMode), [colorMode]);
  const activeCompanyName = useMemo(
    () => companies.find(c => c.companyId === companyId)?.companyName || null,
    [companies, companyId]
  );

  useEffect(() => {
    let ignore = false;
    getBrandingSettings()
      .then((branding) => {
        if (!ignore && branding.logoDataUrl) {
          setLogoUrl(branding.logoDataUrl);
        }
      })
      .catch(() => {
        if (!ignore) {
          setLogoUrl("/smartdocscan-logo.svg");
        }
      });
    getCurrentUser()
      .then((result) => {
        if (!ignore && result.authenticated && result.user) {
          setCurrentUser(result.user);
          setCompanyId(result.user.companyId || DEFAULT_COMPANY_ID);
          window.localStorage.setItem("smartdocscan-user", JSON.stringify(result.user));
        }
      })
      .catch(() => {
        if (!ignore) {
          setCurrentUser(null);
          window.localStorage.removeItem("smartdocscan-user");
        }
      })
      .finally(() => {
        if (!ignore) {
          setAuthLoading(false);
        }
      });
    return () => {
      ignore = true;
    };
  }, []);

  useEffect(() => {
    if (!currentUser) {
      return;
    }
    let ignore = false;
    setLoading(true);
    searchPatients({ companyId, search: debouncedSearch })
      .then((data) => {
        if (!ignore) {
          setPatients(data);
        }
      })
      .catch((error) => {
        if (!ignore) {
          setNotice({ type: "error", text: error.message });
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
  }, [companyId, debouncedSearch, currentUser]);

  useEffect(() => {
    if (!currentUser) {
      return;
    }
    let ignore = false;
    listCompanies()
      .then((data) => {
        if (!ignore) {
          setCompanies(data);
        }
      })
      .catch(() => {
        if (!ignore) {
          setCompanies([]);
        }
      });
    return () => {
      ignore = true;
    };
  }, [currentUser]);

  useEffect(() => {
    window.localStorage.setItem("smartdocscan-theme", colorMode);
    document.documentElement.dataset.theme = colorMode;
  }, [colorMode]);

  useEffect(() => {
    if (!window.history.state?.smartdocscan) {
      window.history.replaceState({ smartdocscan: true, activeView }, "", window.location.href);
    }

    const handlePopState = (event) => {
      isHistoryPopRef.current = true;
      setActiveView(event.state?.smartdocscan ? event.state.activeView || "find" : "find");
    };

    window.addEventListener("popstate", handlePopState);
    return () => window.removeEventListener("popstate", handlePopState);
  }, []);

  useEffect(() => {
    if (!currentUser) {
      return;
    }

    if (isHistoryPopRef.current) {
      isHistoryPopRef.current = false;
      return;
    }

    if (window.history.state?.activeView !== activeView) {
      window.history.pushState({ smartdocscan: true, activeView }, "", window.location.href);
    }
  }, [activeView, currentUser]);

  async function savePatient(form) {
    setSaving(true);
    setNotice(null);
    try {
      if (selectedPatient) {
        await updatePatient(selectedPatient.patientId, form);
        setNotice({ type: "success", text: "Patient updated." });
      } else {
        await createPatient(form);
        setNotice({ type: "success", text: "Patient added." });
      }
      setSelectedPatient(null);
      setActiveView("find");
      setPatients(await searchPatients({ companyId, search }));
    } catch (error) {
      setNotice({ type: "error", text: error.message });
    } finally {
      setSaving(false);
    }
  }

  function editPatient(patient) {
    setSelectedPatient(patient);
    setActiveView("add");
  }

  function viewDocuments(patient) {
    setDocumentPatient(patient);
    setActiveView("documents");
  }

  async function removePatient(patient) {
    const ok = window.confirm(`Delete patient ${patient.lastName}, ${patient.firstName}?`);
    if (!ok) return;
    setNotice(null);
    try {
      await deletePatient(patient.patientId);
      setPatients((current) => current.filter((item) => item.patientId !== patient.patientId));
      setNotice({ type: "success", text: "Patient deleted." });
    } catch (error) {
      setNotice({ type: "error", text: error.message });
    }
  }

  function handleSignedIn(user) {
    setCurrentUser(user);
    setCompanyId(user.companyId || DEFAULT_COMPANY_ID);
    window.localStorage.setItem("smartdocscan-user", JSON.stringify(user));
  }

  async function handleSignOut() {
    try {
      await logout();
    } catch {
      // Session may already be expired; clear local state either way.
    }
    setCurrentUser(null);
    window.localStorage.removeItem("smartdocscan-user");
  }

  function openUserMenu(event) {
    setUserMenuAnchor(event.currentTarget);
  }

  function closeUserMenu() {
    setUserMenuAnchor(null);
  }

  function openChangePassword() {
    setCpForm({ current: "", next: "", confirm: "" });
    setCpError("");
    setCpOpen(true);
    closeUserMenu();
  }

  async function handleChangePassword() {
    if (!cpForm.next) {
      setCpError("New password is required.");
      return;
    }
    if (cpForm.next !== cpForm.confirm) {
      setCpError("New passwords do not match.");
      return;
    }
    setCpSaving(true);
    setCpError("");
    try {
      await changePassword({ username: currentUser.username, currentPassword: cpForm.current, newPassword: cpForm.next });
      setCpOpen(false);
      setNotice({ type: "success", text: "Password changed successfully." });
    } catch (error) {
      setCpError(error.message || "Failed to change password.");
    } finally {
      setCpSaving(false);
    }
  }

  const title = useMemo(() => {
    if (activeView === "add") {
      return "Patient Entry";
    }
    if (activeView === "box") {
      return "Box Directory";
    }
    if (activeView === "documents") {
      return "Patient Documents";
    }
    if (activeView === "companies") {
      return "Company Selection";
    }
    if (activeView === "users") {
      return "User Management";
    }
    if (activeView === "categories") {
      return "Category Management";
    }
    if (activeView === "reports") {
      return "Reports";
    }
    if (activeView === "scan") {
      return "Scan Document";
    }
    if (activeView === "barcode") {
      return "Barcode Generator";
    }
    if (activeView === "settings") {
      return "Settings";
    }
    return "Patient Directory";
  }, [activeView]);

  return (
    authLoading ? (
      <main className="signin-page">
        <section className="signin-panel">
          <h1>Loading</h1>
          <p>Checking your SmartDocScan session.</p>
        </section>
      </main>
    ) :
    currentUser ? (
    <ThemeProvider theme={muiTheme}>
      <CssBaseline />
      <Box className="app-shell">
      <Sidebar activeView={activeView} user={currentUser} logoUrl={logoUrl} open={sidebarOpen} onNavigate={(view) => {
        if (view === "add") {
          setSelectedPatient(null);
        }
        setActiveView(view);
      }} />
      <Box component="main" className="workspace">
        <AppBar className="topbar" position="sticky" color="inherit" elevation={0}>
          <Toolbar sx={{ minHeight: "72px !important", display: "flex", justifyContent: "space-between", alignItems: "center", px: "20px !important", width: "100%" }}>
            <IconButton className="menu-button" type="button" aria-label="Toggle sidebar" onClick={() => setSidebarOpen(o => !o)}>
              <MenuIcon size={20} />
            </IconButton>
            <Stack direction="row" spacing={1.5} alignItems="center">
              {activeCompanyName && (
                <Typography className="topbar-company-name" variant="body2">
                  {activeCompanyName}
                </Typography>
              )}
              <IconButton className="user-avatar-button" onClick={openUserMenu} aria-label="User menu" sx={{ p: "3px" }}>
                <Avatar sx={{ width: 34, height: 34, bgcolor: "primary.main", fontSize: 14, fontWeight: 700 }}>
                  {(currentUser.name || currentUser.username || "U").slice(0, 1).toUpperCase()}
                </Avatar>
              </IconButton>
            </Stack>
          </Toolbar>
        </AppBar>

        <MuiMenu
          anchorEl={userMenuAnchor}
          open={Boolean(userMenuAnchor)}
          onClose={closeUserMenu}
          transformOrigin={{ horizontal: "right", vertical: "top" }}
          anchorOrigin={{ horizontal: "right", vertical: "bottom" }}
          PaperProps={{ className: "user-menu-paper" }}
        >
          <div className="user-menu-header">
            <Avatar sx={{ width: 38, height: 38, bgcolor: "primary.main", fontSize: 15, fontWeight: 700 }}>
              {(currentUser.name || currentUser.username || "U").slice(0, 1).toUpperCase()}
            </Avatar>
            <div>
              <Typography variant="subtitle2" sx={{ fontWeight: 700, lineHeight: 1.3 }}>
                {currentUser.name || currentUser.username}
              </Typography>
              <Typography variant="caption" sx={{ opacity: 0.65 }}>
                {companies.find(c => c.companyId === companyId)?.companyName || `Company ${companyId}`}
              </Typography>
            </div>
          </div>
          <Divider />
          {(currentUser.isAdmin || currentUser.superUser) && (
            <MenuItem className="user-menu-item" onClick={() => { setActiveView("companies"); closeUserMenu(); }}>
              <Building2 size={16} />
              Switch Company
            </MenuItem>
          )}
          <MenuItem className="user-menu-item" onClick={() => { setColorMode(m => m === "dark" ? "light" : "dark"); closeUserMenu(); }}>
            {colorMode === "dark" ? <Sun size={16} /> : <Moon size={16} />}
            {colorMode === "dark" ? "Light Mode" : "Dark Mode"}
          </MenuItem>
          <MenuItem className="user-menu-item" onClick={openChangePassword}>
            <Key size={16} />
            Change Password
          </MenuItem>
          <Divider />
          <MenuItem className="user-menu-item user-menu-signout" onClick={handleSignOut}>
            <LogOut size={16} />
            Sign Out
          </MenuItem>
        </MuiMenu>

        <Dialog open={cpOpen} onClose={() => setCpOpen(false)} maxWidth="xs" fullWidth PaperProps={{ className: "cp-dialog" }}>
          <DialogTitle className="cp-dialog-title">Change Password</DialogTitle>
          <DialogContent className="cp-dialog-body">
            {cpError && <div className="notice error" style={{ margin: "0 0 14px", maxWidth: "none" }}>{cpError}</div>}
            <div className="cp-form">
              <label>Current Password<input type="password" value={cpForm.current} onChange={e => setCpForm(f => ({ ...f, current: e.target.value }))} autoComplete="current-password" /></label>
              <label>New Password<input type="password" value={cpForm.next} onChange={e => setCpForm(f => ({ ...f, next: e.target.value }))} autoComplete="new-password" /></label>
              <label>Confirm New Password<input type="password" value={cpForm.confirm} onChange={e => setCpForm(f => ({ ...f, confirm: e.target.value }))} autoComplete="new-password" /></label>
            </div>
          </DialogContent>
          <DialogActions className="cp-dialog-actions">
            <button className="secondary-button" type="button" onClick={() => setCpOpen(false)}>Cancel</button>
            <button className="primary-button" type="button" disabled={cpSaving} onClick={handleChangePassword}>
              {cpSaving ? "Saving..." : "Change Password"}
            </button>
          </DialogActions>
        </Dialog>

        <section className="page-heading">
          <h1>{title}</h1>
        </section>

        {notice && <div className={`notice ${notice.type}`}>{notice.text}</div>}

        {activeView === "find" ? (
          <PatientSearch
            patients={patients}
            search={search}
            onSearchChange={setSearch}
            loading={loading}
            onEdit={editPatient}
            onDocuments={viewDocuments}
            onDelete={removePatient}
            user={currentUser}
          />
        ) : activeView === "box" ? (
          <BoxManager companyId={companyId} onNotice={setNotice} />
        ) : activeView === "companies" ? (
          <CompanyManager
            companyId={companyId}
            onNotice={setNotice}
            onCompanyChange={(nextCompanyId) => {
              setCompanyId(nextCompanyId);
              setNotice({ type: "success", text: "Company selected." });
              setActiveView("find");
            }}
          />
        ) : activeView === "users" ? (
          <UserManager companyId={companyId} onNotice={setNotice} />
        ) : activeView === "categories" ? (
          <CategoryManager companyId={companyId} onNotice={setNotice} />
        ) : activeView === "reports" ? (
          <ReportManager companyId={companyId} onNotice={setNotice} />
        ) : activeView === "settings" && currentUser.superUser ? (
          <SettingsManager onNotice={setNotice} onBrandingChange={(branding) => setLogoUrl(branding.logoDataUrl || "/smartdocscan-logo.svg")} />
        ) : activeView === "scan" ? (
          <ScannerManager companyId={companyId} patient={documentPatient} onNotice={setNotice} onSaved={() => setActiveView("documents")} />
        ) : activeView === "barcode" && documentPatient ? (
          <BarcodeManager companyId={companyId} patient={documentPatient} onNotice={setNotice} onBack={() => setActiveView("documents")} />
        ) : activeView === "documents" && documentPatient ? (
          <DocumentManager
            companyId={companyId}
            patient={documentPatient}
            user={currentUser}
            onScan={() => setActiveView("scan")}
            onBarcode={() => setActiveView("barcode")}
            onNotice={setNotice}
            onBack={() => setActiveView("find")}
          />
        ) : (
          <PatientForm
            companyId={companyId}
            patient={selectedPatient}
            onSave={savePatient}
            onCancel={() => {
              setSelectedPatient(null);
              setActiveView("find");
            }}
            saving={saving}
          />
        )}
      </Box>
    </Box>
    </ThemeProvider>
    ) : (
      <SignIn onSignedIn={handleSignedIn} logoUrl={logoUrl} />
    )
  );
}

function useDebouncedValue(value, delay) {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const timer = window.setTimeout(() => setDebounced(value), delay);
    return () => window.clearTimeout(timer);
  }, [value, delay]);

  return debounced;
}
