function openPalette() {
  const overlay = document.getElementById('palette-overlay');
  if (!overlay) return;
  overlay.classList.add('open');
  const input = overlay.querySelector('input');
  if (input) {
    input.value = '';
    filterPalette(input);
    input.focus();
  }
}

function closePalette() {
  const overlay = document.getElementById('palette-overlay');
  if (overlay) overlay.classList.remove('open');
}

function filterPalette(input) {
  const q = input.value.toLowerCase();
  const palette = input.closest('.palette');
  palette.querySelectorAll('.result').forEach((r) => {
    r.style.display = r.textContent.toLowerCase().includes(q) ? 'flex' : 'none';
  });
}

document.addEventListener('keydown', (e) => {
  if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
    e.preventDefault();
    openPalette();
  }
  if (e.key === 'Escape') {
    closePalette();
    document.querySelectorAll('.modal-overlay.open').forEach((m) => m.classList.remove('open'));
  }
});

document.addEventListener('DOMContentLoaded', () => {
  const cmdbox = document.querySelector('.cmdbox');
  if (cmdbox) cmdbox.addEventListener('click', openPalette);

  const overlay = document.getElementById('palette-overlay');
  if (overlay) {
    overlay.addEventListener('click', (e) => { if (e.target === overlay) closePalette(); });
    const input = overlay.querySelector('input');
    if (input) input.addEventListener('input', () => filterPalette(input));
  }

  document.querySelectorAll('[data-open-modal]').forEach((btn) => {
    btn.addEventListener('click', () => {
      const modal = document.getElementById(btn.dataset.openModal);
      if (modal) modal.classList.add('open');
    });
  });

  document.querySelectorAll('[data-close-modal]').forEach((btn) => {
    btn.addEventListener('click', () => {
      const modal = btn.closest('.modal-overlay');
      if (modal) modal.classList.remove('open');
    });
  });

  document.querySelectorAll('.modal-overlay').forEach((modal) => {
    modal.addEventListener('click', (e) => { if (e.target === modal) modal.classList.remove('open'); });
  });

  document.querySelectorAll('table.data tr.row').forEach((row) => {
    row.addEventListener('click', (e) => {
      if (e.target.tagName === 'INPUT') return;
      row.classList.toggle('selected');
      const cb = row.querySelector('input[type=checkbox]');
      if (cb) cb.checked = row.classList.contains('selected');
      updateSelectionCount();
    });
  });

  updateSelectionCount();
});

function updateSelectionCount() {
  const bar = document.querySelector('.action-bar');
  if (!bar) return;
  const n = document.querySelectorAll('table.data tr.row.selected').length;
  const counter = bar.querySelector('.count');
  if (counter) counter.textContent = n + ' selected';
  bar.style.display = n > 0 ? 'flex' : 'none';
}
