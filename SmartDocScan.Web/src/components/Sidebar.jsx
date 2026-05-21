import ArchiveOutlinedIcon from "@mui/icons-material/ArchiveOutlined";
import AssignmentOutlinedIcon from "@mui/icons-material/AssignmentOutlined";
import BarChartOutlinedIcon from "@mui/icons-material/BarChartOutlined";
import BusinessOutlinedIcon from "@mui/icons-material/BusinessOutlined";
import CategoryOutlinedIcon from "@mui/icons-material/CategoryOutlined";
import GroupOutlinedIcon from "@mui/icons-material/GroupOutlined";
import PersonAddAltOutlinedIcon from "@mui/icons-material/PersonAddAltOutlined";
import SearchOutlinedIcon from "@mui/icons-material/SearchOutlined";
import SettingsOutlinedIcon from "@mui/icons-material/SettingsOutlined";
import { Box, Divider, Drawer, List, ListItemButton, ListItemIcon, ListItemText, Toolbar } from "@mui/material";

export function Sidebar({ activeView, onNavigate, user, logoUrl, open = true }) {
  const items = [
    { id: "find", label: "Find Patient", icon: SearchOutlinedIcon },
    { id: "add", label: "Add Patient", icon: PersonAddAltOutlinedIcon, permission: "addPatients" },
    { id: "box", label: "Add Box", icon: ArchiveOutlinedIcon, permission: "box" },
    { id: "companies", label: "Companies", icon: BusinessOutlinedIcon, adminOnly: true },
    { id: "users", label: "Users", icon: GroupOutlinedIcon, permission: "addUsers" },
    { id: "categories", label: "Categories", icon: CategoryOutlinedIcon, permission: "addCategory" },
    { id: "reports", label: "Reports", icon: BarChartOutlinedIcon, permission: "report" },
    { id: "audit", label: "Audit Logs", icon: AssignmentOutlinedIcon, superOnly: true },
    { id: "settings", label: "Settings", icon: SettingsOutlinedIcon, superOnly: true },
  ].filter((item) => canShow(item, user));

  return (
    <Drawer className={`sidebar${open ? "" : " sidebar--closed"}`} variant="permanent" PaperProps={{ className: "sidebar-paper" }}>
      <Toolbar className="brand">
        <Box component="img" className="brand-logo" src={logoUrl || "/smartdocscan-logo.svg"} alt="Smart Doc Scan" />
      </Toolbar>
      <Divider className="sidebar-divider" />
      <List className="side-nav" component="nav" aria-label="Main navigation">
        {items.map((item) => {
          const Icon = item.icon;
          const isActive = activeView === item.id;
          return (
            <ListItemButton
              key={item.id}
              className="nav-item"
              selected={isActive}
              onClick={() => !item.disabled && onNavigate(item.id)}
              disabled={item.disabled}
            >
              <ListItemIcon className="nav-icon">
                <Icon fontSize="small" />
              </ListItemIcon>
              <ListItemText primary={item.label} primaryTypographyProps={{ fontWeight: 700, fontSize: 14 }} />
            </ListItemButton>
          );
        })}
      </List>
    </Drawer>
  );
}

function canShow(item, user) {
  if (!item.permission && !item.adminOnly && !item.superOnly) {
    return true;
  }
  if (item.superOnly) {
    return Boolean(user?.superUser);
  }
  if (user?.isAdmin || user?.superUser) {
    return true;
  }
  if (item.adminOnly) {
    return false;
  }
  return Boolean(user?.[item.permission]);
}
