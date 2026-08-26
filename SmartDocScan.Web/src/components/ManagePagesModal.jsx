import React, { useState, useEffect } from 'react';
import { 
  Dialog, DialogTitle, DialogContent, DialogActions, 
  Button, Typography, CircularProgress, Box, IconButton, Checkbox
} from '@mui/material';
import { Layers, Trash2, ArrowLeft, ArrowRight, Save, X } from 'lucide-react';

export function ManagePagesModal({ open, onClose, document, onNotice, onSuccess }) {
  const [pages, setPages] = useState([]);
  const [loading, setLoading] = useState(false);
  const [selectedPages, setSelectedPages] = useState(new Set());
  const [draggedIdx, setDraggedIdx] = useState(null);

  useEffect(() => {
    if (open && document) {
      loadPages();
    }
  }, [open, document]);

  const loadPages = async () => {
    setLoading(true);
    try {
      const res = await fetch(/api/business-documents//pages, {
        headers: { 'Authorization': Bearer  }
      });
      if (!res.ok) throw new Error(await res.text());
      const data = await res.json();
      setPages(data);
      setSelectedPages(new Set());
    } catch (e) {
      onNotice({ type: 'error', text: 'Failed to load pages: ' + e.message });
    } finally {
      setLoading(false);
    }
  };

  const handleDragStart = (e, idx) => {
    setDraggedIdx(idx);
    e.dataTransfer.effectAllowed = 'move';
  };

  const handleDragOver = (e, idx) => {
    e.preventDefault();
    if (draggedIdx === null || draggedIdx === idx) return;
    
    // Swap
    const newPages = [...pages];
    const draggedPage = newPages[draggedIdx];
    newPages.splice(draggedIdx, 1);
    newPages.splice(idx, 0, draggedPage);
    
    setDraggedIdx(idx);
    setPages(newPages);
  };

  const handleDragEnd = () => {
    setDraggedIdx(null);
  };

  const toggleSelection = (idx) => {
    const next = new Set(selectedPages);
    if (next.has(idx)) next.delete(idx);
    else next.add(idx);
    setSelectedPages(next);
  };

  const handleSaveOrder = async () => {
    setLoading(true);
    try {
      const newOrder = pages.map(p => p.pageIndex);
      const res = await fetch(/api/business-documents//reorder, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': Bearer 
        },
        body: JSON.stringify(newOrder)
      });
      if (!res.ok) throw new Error(await res.text());
      onNotice({ type: 'success', text: 'Pages reordered successfully!' });
      onSuccess();
    } catch (e) {
      onNotice({ type: 'error', text: 'Failed to reorder: ' + e.message });
      setLoading(false);
    }
  };

  const handleExtract = async () => {
    if (selectedPages.size === 0) return;
    setLoading(true);
    try {
      const indicesToExtract = Array.from(selectedPages).map(idx => pages[idx].pageIndex);
      const res = await fetch(/api/business-documents//extract, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': Bearer 
        },
        body: JSON.stringify(indicesToExtract)
      });
      if (!res.ok) throw new Error(await res.text());
      onNotice({ type: 'success', text: 'Pages extracted to new document successfully!' });
      onSuccess();
    } catch (e) {
      onNotice({ type: 'error', text: 'Failed to extract: ' + e.message });
      setLoading(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="lg" fullWidth>
      <DialogTitle sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h6" sx={{ fontWeight: 'bold', display: 'flex', alignItems: 'center', gap: 1 }}>
          <Layers size={24} /> Manage Pages: {document?.documentName}
        </Typography>
        <IconButton onClick={onClose}><X /></IconButton>
      </DialogTitle>
      
      <DialogContent dividers sx={{ background: '#f8fafc', minHeight: '60vh' }}>
        {loading ? (
          <Box display="flex" justifyContent="center" p={4}><CircularProgress /></Box>
        ) : (
          <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(200px, 1fr))', gap: 3 }}>
            {pages.map((page, idx) => (
              <Box 
                key={page.pageIndex}
                draggable
                onDragStart={(e) => handleDragStart(e, idx)}
                onDragOver={(e) => handleDragOver(e, idx)}
                onDragEnd={handleDragEnd}
                onClick={() => toggleSelection(idx)}
                sx={{
                  background: 'white',
                  borderRadius: 2,
                  p: 1,
                  boxShadow: selectedPages.has(idx) ? '0 0 0 3px #3b82f6' : '0 1px 3px rgba(0,0,0,0.1)',
                  cursor: 'grab',
                  position: 'relative',
                  opacity: draggedIdx === idx ? 0.5 : 1,
                  transition: 'transform 0.2s',
                  '&:active': { cursor: 'grabbing' },
                  '&:hover': { transform: 'scale(1.02)' }
                }}
              >
                <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1 }}>
                  <Typography variant="caption" sx={{ fontWeight: 'bold', color: '#64748b' }}>
                    Page {idx + 1}
                  </Typography>
                  <Checkbox 
                    size="small" 
                    checked={selectedPages.has(idx)} 
                    onChange={() => toggleSelection(idx)} 
                    sx={{ p: 0 }}
                  />
                </Box>
                <Box sx={{ height: 250, display: 'flex', justifyContent: 'center', alignItems: 'center', background: '#f1f5f9', borderRadius: 1, overflow: 'hidden' }}>
                  <img 
                    src={page.thumbnailUrl} 
                    alt={Page } 
                    style={{ maxWidth: '100%', maxHeight: '100%', objectFit: 'contain' }} 
                  />
                </Box>
              </Box>
            ))}
          </Box>
        )}
      </DialogContent>
      
      <DialogActions sx={{ p: 2, display: 'flex', justifyContent: 'space-between' }}>
        <Box>
          <Button 
            variant="outlined" 
            color="primary" 
            startIcon={<Save size={18} />}
            onClick={handleSaveOrder}
            disabled={loading || pages.length === 0}
          >
            Save New Order
          </Button>
        </Box>
        <Box sx={{ display: 'flex', gap: 2 }}>
          <Button onClick={onClose} disabled={loading}>Cancel</Button>
          <Button 
            variant="contained" 
            color="secondary" 
            startIcon={<Layers size={18} />}
            disabled={loading || selectedPages.size === 0 || selectedPages.size === pages.length}
            onClick={handleExtract}
          >
            Extract Selected Pages
          </Button>
        </Box>
      </DialogActions>
    </Dialog>
  );
}
